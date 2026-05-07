# InventoryManager 指南

## 概述

`InventoryManager`（`[Manager(10)]`，薄门面）+ `InventoryService`（业务逻辑 + 持久化）提供完整的背包系统：

- 多容器（玩家背包、箱子等）
- 物品模板 + 链式 API（`InventoryItem`）
- 堆叠 / 权重上限 / 槽位锁定 / 移动/拆堆
- 配置驱动 UI（`InventoryConfig` + `PanelConfig` + `SlotConfig` + `ButtonConfig`）
- 通过 Event 调用 UIManager，**完全解耦**

## 文件结构

```
InventoryManager/
├── InventoryManager.cs            薄门面：生命周期、默认注册、EVT_OPEN_UI / EVT_CLOSE_UI、缓存
├── InventoryService.cs            业务核心 + 持久化 + 7 个 [Event] handler
├── Agent.md                       本文档
├── Editor/
│   └── InventoryManagerEditor.cs  Inspector 自定义绘制
├── UI/
│   ├── InventoryUIBuilder.cs      纯静态 UI 构建/绑定/拖拽挂载（BuildPanelTree / ApplyItemToSlot 等）
│   ├── InventoryUIRefs.cs         SlotUIRefs / DescUIRefs 引用集合
│   └── InventorySlotDragHandler.cs slot 拖拽实现（IBeginDrag/IDrag/IEndDrag/IDropHandler）
└── Dao/
    ├── Inventory.cs               Inventory + InventorySlot
    ├── Item.cs                    InventoryItem + InventoryItemType
    └── UIConfig/                  UI 配置组
        ├── InventoryConfig.cs         容器主配置（含 ShowTitle / ShowDescription 开关）
        ├── PanelConfig.cs             主面板尺寸/位置/背景
        ├── SlotConfig.cs              槽位布局/背景
        ├── ButtonConfig.cs            通用按钮配置（关闭按钮等）
        ├── TitleConfig.cs             容器标题文本
        ├── DescriptionPanelConfig.cs  描述子面板：背景 + 4 个子组件
        └── DescriptionElementConfig.cs DescriptionIconConfig / DescriptionTextElementConfig
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `InventoryService.CAT_INVENTORIES` = `"Inventories"` | 所有容器实例 |
| `InventoryService.CAT_TEMPLATES`   = `"Items"` | 物品模板（`InventoryItem`） |
| `InventoryService.CAT_CONFIGS`     = `"Configs"` | UI 配置 |

## Event API

> 共 11 个：2 个 Manager 命令 + 7 个 Service 命令/查询 + 2 个 Service 广播。

### 命令类（调用方主动触发，期望返回结果）

#### `InventoryManager.EVT_OPEN_UI` — 打开背包 UI
- **常量**: `InventoryManager.EVT_OPEN_UI` = `"OpenInventoryUI"`
- **参数**: `[string inventoryId, string configId?]`（configId 缺省时用 inventoryId）
- **返回**: `ResultCode.Ok(inventoryId)` / `ResultCode.Fail(msg)`
- **副作用**: 调 `UIManager.EVT_REGISTER_ENTITY` 创建 UI 实体；UI 打开后广播 `InventoryService.EVT_OPEN_UI`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      InventoryManager.EVT_OPEN_UI,
      new List<object> { "player", "PlayerBackPack" });
  ```

#### `InventoryManager.EVT_CLOSE_UI` — 关闭背包 UI
- **常量**: `InventoryManager.EVT_CLOSE_UI` = `"CloseInventoryUI"`
- **参数**: `[string inventoryId]`
- **返回**: `ResultCode.Ok(inventoryId)` / `ResultCode.Fail(msg)`
- **副作用**: 调 `UIManager.EVT_UNREGISTER_ENTITY`；广播 `InventoryService.EVT_CLOSE_UI`

#### `InventoryService.EVT_CREATE` — 创建容器
- **常量**: `InventoryService.EVT_CREATE` = `"InventoryCreate"`
- **参数**: `[string id, string name, int maxSlots]`
- **返回**: `ResultCode.Ok(Inventory)` / `ResultCode.Fail(msg)`
- **副作用**: 写入 `CAT_INVENTORIES` 持久化分类
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      InventoryService.EVT_CREATE,
      new List<object> { "chest", "箱子", 20 });
  ```

#### `InventoryService.EVT_DELETE` — 删除容器
- **常量**: `InventoryService.EVT_DELETE` = `"InventoryDelete"`
- **参数**: `[string id]`
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail(msg)`

#### `InventoryService.EVT_ADD` — 添加物品
- **常量**: `InventoryService.EVT_ADD` = `"InventoryAdd"`
- **参数**: `[string inventoryId, object itemIdOrItem, int amount]`
  - `itemIdOrItem` 可以是 `string`（模板 id）或 `InventoryItem` 实例
- **返回**: `ResultCode.Ok(InventoryResult)` / `ResultCode.Fail(msg)`（`InventoryResult` 含 `Success/Amount/Remaining`）
- **副作用**: 修改 `CAT_INVENTORIES`；广播 `EVT_CHANGED`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      InventoryService.EVT_ADD,
      new List<object> { "player", "potion_heal", 5 });
  ```

#### `InventoryService.EVT_REMOVE` — 移除物品
- **常量**: `InventoryService.EVT_REMOVE` = `"InventoryRemove"`
- **参数**: `[string inventoryId, string itemId, int amount]`
- **返回**: `ResultCode.Ok(InventoryResult)` / `ResultCode.Fail(msg)`
- **副作用**: 修改 `CAT_INVENTORIES`；广播 `EVT_CHANGED`

#### `InventoryService.EVT_MOVE` — 移动物品
- **常量**: `InventoryService.EVT_MOVE` = `"InventoryMove"`
- **参数**: `[string inventoryId, int fromSlot, int toSlot, int amount]`
- **返回**: `ResultCode.Ok(InventoryResult)` / `ResultCode.Fail(msg)`
- **副作用**: 修改槽位状态；广播 `EVT_CHANGED`

#### `InventoryService.EVT_QUERY` — 查询容器
- **常量**: `InventoryService.EVT_QUERY` = `"InventoryQuery"`
- **参数**: `[string inventoryId]`
- **返回**: `ResultCode.Ok(Inventory)` / `ResultCode.Fail(msg)`
- **副作用**: 无（纯查询）

### 广播类（用 `[EventListener]` 订阅，无返回值期望）

#### `InventoryService.EVT_CHANGED` — 背包内容变化
- **常量**: `InventoryService.EVT_CHANGED` = `"InventoryChanged"`
- **触发条件**: 任何 ADD/REMOVE/MOVE 操作成功后
- **参数**: `[string inventoryId, string op, string itemId, int amount]`
  - `op` ∈ `{"add", "remove", "move"}`
- **典型订阅**: UI 刷新槽位显示
- **示例**:
  ```csharp
  [EventListener(InventoryService.EVT_CHANGED)]
  public List<object> OnInventoryChanged(string evt, List<object> args)
  {
      var inventoryId = args[0] as string;
      var op = args[1] as string;
      // 刷新 UI...
      return null;
  }
  ```

#### `InventoryService.EVT_OPEN_UI` — UI 已打开（广播）
- **常量**: `InventoryService.EVT_OPEN_UI` = `"OnOpenInventoryUI"`
- **触发条件**: `InventoryManager.EVT_OPEN_UI` 命令成功执行后
- **参数**: `[string inventoryId]`
- ⚠️ **注意**: 与 `InventoryManager.EVT_OPEN_UI`（命令，`"OpenInventoryUI"`）**不同**

#### `InventoryService.EVT_CLOSE_UI` — UI 已关闭（广播）
- **常量**: `InventoryService.EVT_CLOSE_UI` = `"OnCloseInventoryUI"`
- **触发条件**: `InventoryManager.EVT_CLOSE_UI` 命令成功执行后
- **参数**: `[string inventoryId]`

## 命令 vs 广播 对照表

| 区分维度 | `InventoryManager.EVT_OPEN_UI`（命令） | `InventoryService.EVT_OPEN_UI`（广播） |
|---|---|---|
| 字符串值 | `"OpenInventoryUI"` | `"OnOpenInventoryUI"` |
| 触发者 | 调用方主动调 `TriggerEventMethod` | Service 在命令完成后调 `TriggerEvent` |
| 注解 | `[Event]` | `EventProcessor.Instance.TriggerEvent` 直接发出 |
| 期望响应 | 返回成功/失败结果 | 无（订阅者按需响应） |
| 接入方式 | `TriggerEventMethod(...)` | `[EventListener("OnOpenInventoryUI")]` |

## 批量操作（BeginBatch）

`AddItem` / `RemoveItem` / `MoveItem` 每次内部都会 `SetData(CAT_INVENTORIES, ...)` 触发同步写盘。**连续多次操作时**强烈建议用 `Service<T>.BeginBatch()` 包起来，把 N 次 fsync 合并成 1 次：

```csharp
using (InventoryService.Instance.BeginBatch())
{
    InventoryService.Instance.AddItem("player", apple, 5);
    InventoryService.Instance.AddItem("player", sword, 1);
    InventoryService.Instance.AddItem("player", potion, 3);
    InventoryService.Instance.MoveItem("player", 0, 5);
}   // Dispose 时一次性 flush 所有 dirty categories
```

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

```csharp
new InventoryConfig("PlayerBackPack", "玩家背包")
    .WithShowDescription(true)
    .WithDescriptionPanelConfig(new DescriptionPanelConfig(240f, 220f)
        .WithOffset(710f, 150f)             // 相对主面板左下角
        .WithBackgroundColor(new Color(0.05f, 0.05f, 0.08f, 0.92f))
        .WithTextPadding(14f, 14f)
        .WithFontSize(14)
        .WithTextColor(Color.white)
        .WithEmptyPlaceholder("（点击物品查看描述）"));
```

行为：
- 描述面板作为主 panel 的子节点，`Offset` 是相对主面板左下角的位置（与所有子组件一致）
- 点击任意 slot：若有物品 → 显示 `Name\n\nDescription`；空槽 → 恢复 `EmptyPlaceholder`
- 关闭即销毁，重新打开重建

`ShowDescription = false`（默认 / Chest 配置）时不创建描述面板，slot 点击不产生额外副作用。

## 物品模板注册示例

```csharp
InventoryService.Instance.RegisterTemplate(
    new InventoryItem("potion_heal")
        .WithName("治疗药水")
        .WithType(InventoryItemType.Consumable)
        .WithIcon("Sprites/Items/PotionHeal")   // 通过 ResourceManager 解析的 sprite id
        .WithValue(25)
        .WithMaxStack(99));
```

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
