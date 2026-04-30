# UIManager 指南

## 概述

`UIManager`（`[Manager(5)]`）+ `UIService` 是框架唯一的 UI 实体管理中心。

**架构规则**：
- 其他模块**禁止**自建 `Entity/` 文件夹
- 业务模块只构造 `UIComponent` 数据（Dao），UI 表现层统一交给 UIManager
- UI 实体（`UIEntity`，MonoBehaviour）实例化由 `UIService` + `UIEntityFactory` 完成

## Event API

### `EVT_REGISTER_ENTITY` — 注册 UI 实体
- **常量**: `UIManager.EVT_REGISTER_ENTITY` = `"RegisterUIEntity"`
- **参数**: `[string daoId, UIComponent component]`
- **返回**: `ResultCode.Ok(UIEntity)` / `ResultCode.Fail("参数无效")`
- **副作用**: 在 UI Canvas 下递归创建 GameObject 树；component 树存入 `UIComponents` 分类（持久化）
- **示例**:
  ```csharp
  var panel = new UIPanelComponent("inv", "背包").SetSize(680, 560);
  EventProcessor.Instance.TriggerEventMethod(
      UIManager.EVT_REGISTER_ENTITY,
      new List<object> { "inv", panel });
  ```

### `EVT_GET_ENTITY` — 获取已注册的 UI 实体
- **常量**: `UIManager.EVT_GET_ENTITY` = `"GetUIEntity"`
- **参数**: `[string daoId]`
- **返回**: `ResultCode.Ok(UIEntity)` / `ResultCode.Fail("未找到实体")`
- **副作用**: 无（纯查询）
- **典型用法**: 探测 UI 是否已打开
- **示例**:
  ```csharp
  var r = EventProcessor.Instance.TriggerEventMethod(
      UIManager.EVT_GET_ENTITY, new List<object> { "inv" });
  bool isOpen = ResultCode.IsOk(r);
  ```

### `EVT_UNREGISTER_ENTITY` — 注销并销毁 UI 实体
- **常量**: `UIManager.EVT_UNREGISTER_ENTITY` = `"UnregisterUIEntity"`
- **参数**: `[string daoId]`
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail("参数无效")`
- **副作用**: 销毁对应 GameObject，从 `_uiEntityCache` 移除；持久化的 UIComponents 数据**不会**删除
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      UIManager.EVT_UNREGISTER_ENTITY, new List<object> { "inv" });
  ```

### `EVT_HOT_RELOAD` — 热重载 UI 配置
- **常量**: `UIManager.EVT_HOT_RELOAD` = `"HotReloadUIConfigs"`
- **参数**: `[]`（无）
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail("热重载失败")`
- **副作用**: 重新读 `{persistentDataPath}/ServiceData/UIService/UIComponents.json`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      UIManager.EVT_HOT_RELOAD, new List<object>());
  ```

## 调用方式

### 跨模块调用（推荐）

```csharp
EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_REGISTER_ENTITY,
    new List<object> { "player_panel", panelComponent });

var r = EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_GET_ENTITY,
    new List<object> { "player_panel" });
if (ResultCode.IsOk(r)) { var entity = r[1] as UIEntity; }
```

### 内部调用（UIManager 自己用）

```csharp
var canvas = UIManager.Instance.GetCanvasTransform();
var entity = UIService.Instance.RegisterUIEntity(daoId, component, canvas);
```

外部模块**不要**这样做——破坏解耦。

## UIComponent 体系

业务模块通过 `UIComponent`（`UIPanelComponent` / `UIButtonComponent` / ...）描述 UI 树：

```csharp
var panel = new UIPanelComponent("inv", "背包")
    .SetPosition(960, 540).SetSize(680, 560)
    .SetBackgroundColor(...).SetVisible(true);

panel.AddChild(new UIButtonComponent("inv_close", "关闭", "×")
    .SetPosition(640, 520).SetSize(36, 36));

EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_REGISTER_ENTITY,
    new List<object> { panel.Id, panel });
```

`UIService.RegisterUIEntity` 内部递归：
1. `StoreComponentTreeRecursive` — 把 UIComponent 树存到 `UIComponents` 分类（用于热重载）
2. `CreateEntityRecursive` — 用 `UIEntityFactory` 实例化 GameObject 树

## Canvas 管理

UIManager 持有 `_uiCanvas`：
- 没有则自动创建（1920×1080 ScaleWithScreenSize，自带 `GraphicRaycaster`）
- `GetCanvasTransform()` 暴露给 UIService

## EventSystem 管理

`Initialize()` 末尾调用 `EnsureEventSystem()`：
- 检查 `EventSystem.current`，不存在则建一个 GameObject 加 `EventSystem` + `StandaloneInputModule`
- **没有它，整个场景的 UGUI 按钮都收不到点击** —— UIManager 自动兜底，业务模块无需关心
- 若项目改用 new Input System，需把 `StandaloneInputModule` 替换为 `InputSystemUIInputModule`

## 内存缓存

`_uiEntityCache: Dictionary<string, UIEntity>` 存储 daoId → 实体引用。`UnregisterUIEntity` 销毁后从缓存移除。

## 热重载

```csharp
EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_HOT_RELOAD, new List<object>());
```

→ `UIService.HotReloadConfigs()` → `LoadData()` 重新读磁盘 JSON。

## 注意事项

- `UIComponent.Id` 必须唯一，`RegisterUIEntity` 用它作 daoId
- 持久化只保存 `UIComponent` 数据，`UIEntity`（GameObject）运行时重建
- `UIEntity` 是 MonoBehaviour，在 `OnDestroy` 中应调用 `UIService.Instance.UnregisterUIEntity` 清理缓存
