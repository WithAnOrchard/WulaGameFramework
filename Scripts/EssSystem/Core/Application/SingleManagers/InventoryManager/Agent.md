# InventoryManager 指南
## 概述
`InventoryManager`（`[Manager(10)]`，薄门面）+ `InventoryService`（业务逻辑 + 持久化）提供完整的背包系统：
- 多容器（玩家背包、箱子等）
- 物品模板 + 链式 API（`InventoryItem`）
- 堆叠 / 权重上限 / 槽位锁定 / 移动/拆堆
- 配置驱动 UI（`InventoryConfig` + `SlotConfig` + 通用 `UIPanelSpec` / `UIButtonSpec` / `UITextSpec`）
- 通过 Event 调用 UIManager，**完全解耦**
- **3 个内置默认容器**（`Initialize` 时自动注册，仅持久化缺失时）：
  - `player` (configId `PlayerBackPack`) — 30 格主背包，居中显示
  - `hotbar` (configId `Hotbar`) — 9 格快捷栏，**启动后自动打开常驻底部**
  - `equipment` (configId `PlayerEquipment`) — 5 格装备栏（头盔/盔甲/护腿/鞋子/背包），**与 `player` 联动开关**，挂在玩家背包右侧
- **快捷栏键盘**：监听 `KeyCode.Alpha1 ~ Alpha9`，按下时广播 `EVT_HOTBAR_USE` 携带 `[invId, slotIndex, item]`，业务层（如 EquipmentManager）订阅处理
## 文件结构
```
InventoryManager/
├── InventoryManager.cs            薄门面：生命周期、默认注册、EVT_OPEN_UI / EVT_CLOSE_UI、UI 缓存、可拾取定义注册/生成
├── InventoryService.cs            业务核心 + 持久化 + 7 个 [Event] handler
├── Agent.md                       本文档
├── Editor/
│   └── InventoryManagerEditor.cs  Inspector 自定义绘制
├── Runtime/
│   └── PickableItem.cs            场景可拾取物 MonoBehaviour（玩家触发器进入 → 填背包）
├── UI/
│   ├── InventoryUIBuilder.cs      纯静态 UI 构建/绑定/拖拽挂载（BuildPanelTree / ApplyItemToSlot 等）
│   ├── InventoryUIRefs.cs         SlotUIRefs / DescUIRefs 引用集合
│   └── InventorySlotDragHandler.cs slot 拖拽实现（IBeginDrag/IDrag/IEndDrag/IDropHandler）
└── Dao/
    ├── Inventory.cs                   Inventory + InventorySlot
    ├── Item.cs                        InventoryItem + InventoryItemType
    ├── InventoryResult.cs             Add/Remove/Move 统一返回结构
    ├── PickableItemDefinition.cs      可拾取物定义（sprite/template/collider）
    └── UIConfig/                      背包独有 UI Config（通用部分见 UIManager/Dao/Specs/*）
        ├── InventoryConfig.cs         容器主配置：聚合 UIPanelSpec / UIButtonSpec / UITextSpec
        ├── SlotConfig.cs              槽位网格布局（背包独有）
        └── DescriptionPanelConfig.cs  描述子面板（背包独有复合，内含 UIPanelSpec / UIIconSpec / 3×UITextSpec）
```
> 通用 UI 配置（面板/按钮/文本/图标）已上提到 `Core/Presentation/UIManager/Dao/Specs/`，供所有业务模块复用：
> `UIPanelSpec` / `UIButtonSpec` / `UITextSpec` / `UIIconSpec`，每个都自带 `CreateComponent(id, name)` 工厂方法直接生成对应 `UI*Component`。
## 数据分类（持久化）
| 常量 | 用途 |
|---|---|
| `InventoryService.CAT_INVENTORIES` = `"Inventories"` | 所有容器实例 |
| `InventoryService.CAT_TEMPLATES`   = `"Items"` | 物品模板（`InventoryItem`） |
| `InventoryService.CAT_CONFIGS`     = `"Configs"` | UI 配置 |
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **InventoryManager Event**.

- `InventoryManager.EVT_CLOSE_UI`
- `InventoryManager.EVT_HOTBAR_USE`
- `InventoryManager.EVT_OPEN_UI`
- `InventoryManager.EVT_REGISTER_ITEM`
- `InventoryManager.EVT_REGISTER_PICKABLE_ITEM`
- `InventoryManager.EVT_SPAWN_PICKABLE_ITEM`
- `InventoryService.EVT_ADD`
- `InventoryService.EVT_CHANGED`
- `InventoryService.EVT_CREATE`
- `InventoryService.EVT_DELETE`
- `InventoryService.EVT_MOVE`
- `InventoryService.EVT_QUERY`
- `InventoryService.EVT_COUNT_ITEM`
- `InventoryService.EVT_REMOVE`
- `UIManager.EVT_REGISTER_ENTITY`

## 批量操作（BeginBatch）
`AddItem` / `RemoveItem` / `MoveItem` 每次内部都会 `SetData(CAT_INVENTORIES, ...)` 触发同步写盘。**连续多次操作时**强烈建议用 `Service<T>.BeginBatch()` 包起来，把 N 次 fsync 合并成 1 次：
典型场景：
- NPC 一次性给一袋物品（n 个 AddItem）
- 战斗结算掉落（batch add）
- 存档迁移 / 测试 fixture（程序化构造大背包）
`BeginBatch` 支持嵌套；`Application.quitting` 时 DataService 兜底 flush。详见 `EssManagers/Manager/Agent.md` 的「批量写盘」章节。
## Slot 信息显示
`BuildPanelTree` 为每个 slot 生成三层 UI：
| 子节点 ID 后缀 | 内容 | 位置 |
|---|---|---|
| `_Slot_{i}` | 背景按钮（点击可触发描述更新） | 整槽 |
| `_Slot_{i}_NameText` | 物品 `Name`（空槽留空） | 上半居中 |
| `_Slot_{i}_StackText` | `当前/最大`（仅 `MaxStack > 1` 显示） | 下半居中 |
打开 UI 时会读取 `InventoryService.GetInventory(id)` 当前状态填充。
**自动刷新**：`InventoryManager.OnInventoryChanged` 监听 `InventoryService.EVT_CHANGED`，
对**当前已打开 UI** 的容器原地更新所有 slot 的 NameText / StackText（不重建 GameObject，
不影响描述面板已选中状态）。`OpenInventoryUI` 把每个 slot 的两个 `UITextComponent` 引用
缓存进 `_slotTextRefs[inventoryId]`，`CloseInventoryUI` 时清除。其他容器的 `EVT_CHANGED`
事件不会触发额外开销。
## 描述子面板
通过 `InventoryConfig` 开启：
行为：
- 描述面板作为主 panel 的子节点，`Offset` 是相对主面板左下角的位置（与所有子组件一致）
- 点击任意 slot：若有物品 → 显示 `Name\n\nDescription`；空槽 → 恢复 `EmptyPlaceholder`
- 关闭即销毁，重新打开重建
`ShowDescription = false`（默认 / Chest 配置）时不创建描述面板，slot 点击不产生额外副作用。
## 物品模板注册示例
## 与 UIManager 的解耦关系
InventoryManager 通过 Event 调 UIManager，**不直接 `using`** 业务方法：
```
InventoryManager.OpenInventoryUI()
  → EventProcessor.TriggerEventMethod(UIManager.EVT_REGISTER_ENTITY, ...)
       → UIManager.RegisterUIEntity()
            → UIService.RegisterUIEntity()
```
UIManager 的常量名引用是允许的（编译期依赖换 IDE 跳转，可接受）。如要完全解耦，未来可把 UI 相关常量移到 `Core/Event/CoreEvents.cs`。
## 注意事项
- 物品模板 (`InventoryItem`) 必须 `[Serializable]` 才能持久化
- `InventoryResult` 是 `readonly struct`（值类型），跨 Event 传输时已被装箱到 `List<object>`
- `EVT_CHANGED` 内**不要**调可能再次修改背包的代码（避免无限循环）
- 调试菜单：右键 InventoryManager 组件 → `[ContextMenu]` 列表
