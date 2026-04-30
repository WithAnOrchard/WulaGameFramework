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
├── InventoryManager.cs            薄门面 + 调试菜单 + UI 打开/关闭（含广播 EVT_OPEN_UI / EVT_CLOSE_UI）
├── InventoryService.cs            业务核心 + 持久化 + 7 个 [Event] handler
├── Agent.md                       本文档
├── Editor/
│   └── InventoryManagerEditor.cs  Inspector 自定义绘制
└── Dao/
    ├── Inventory.cs               Inventory + InventorySlot
    ├── Item.cs                    InventoryItem + InventoryItemType
    └── UIConfig/                  UI 配置组（容器/面板/槽位/按钮）
        ├── InventoryConfig.cs
        ├── PanelConfig.cs
        ├── SlotConfig.cs
        └── ButtonConfig.cs
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

## 物品模板注册示例

```csharp
InventoryService.Instance.RegisterTemplate(
    new InventoryItem("potion_heal")
        .WithName("治疗药水")
        .WithType(InventoryItemType.Consumable)
        .WithWeight(0.5f)
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
