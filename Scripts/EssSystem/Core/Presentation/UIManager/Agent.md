# UIManager 指南

## 概述

`Presentation/UIManager`（`[Manager(5)]`）—— 框架唯一的 UI 实体管理中心。

| | 类 | 角色 |
|---|---|---|
| Manager | `UIManager` | MonoBehaviour 单例：UGUI Canvas + EventSystem 自动建立、Event 接口入口 |
| Service | `UIService` | 纯 C# 单例：UIEntity 缓存 + UIComponent 树持久化 + 创建/销毁递归 |

**架构强约束**（违反 → Anti-Patterns §B8 / §B9 / §A7）：

- 业务模块**只构造 `UIComponent` 数据（DAO）**；UI 实体的实例化**只能**走 `UIManager`
- 业务模块**禁止** `using UnityEngine.UI` 自建 `Canvas` / `Image` / `Button` / `Text` / 布局组件
- 业务模块**禁止**自建 `Entity/` 子目录；所有 `UIEntity` 子类只在 `Presentation/UIManager/Entity/`
- 跨模块查 UI 走 `EVT_GET_UI_GAMEOBJECT` 返 `GameObject`（中立类型），**不返**` UIEntity`

## 文件结构

```
Presentation/UIManager/
├── UIManager.cs       Manager（Canvas/EventSystem 建立 + 7 个 [Event] 入口）
├── UIService.cs       Service（缓存 + 持久化 + 创建/销毁递归）
├── Dao/               UIComponent 数据类（业务侧用）
│   ├── UIComponent.cs（基类）
│   ├── UIType.cs      Adjustable.cs
│   └── CommonComponents/  UIPanelComponent / UIButtonComponent / UITextComponent / UIBarComponent
├── Entity/            UIEntity 运行时（MonoBehaviour，框架内部）
│   ├── UIEntity.cs（基类）  UIEntityFactory.cs
│   └── CommonEntity/  UIPanelEntity / UIButtonEntity / UITextEntity / UIBarEntity
└── Editor/UIManagerEditor.cs
```

## 数据流

```
业务侧                          UIManager / UIService                       场景
                              ────────────────────────                  ────────
new UIPanelComponent(...)
.AddChild(...)
       │
       ▼ TriggerEventMethod(EVT_REGISTER_ENTITY, [id, component])
                              UIManager.RegisterUIEntity
                                  │
                                  ▼ UIService.RegisterUIEntity
                                  │   ├─ BeginBatch
                                  │   ├─ StoreComponentTreeRecursive  ──→ DataService(UIComponents.json)
                                  │   ├─ CreateEntityRecursive        ──→ Canvas/UICanvas/...
                                  │   └─ _uiEntityCache[id] = entity
                                  ▼
                              ResultCode.Ok(daoId)
```

## Event API

> 共 7 个：4 个命令（注册/获取/销毁/热重载）+ 3 个查询/广播响应。所有事件都用 bare-string 跨模块调用（§4.1）。

### 命令类

#### `UIManager.EVT_REGISTER_ENTITY` — 注册 UI 实体（创建）
- **常量**: `UIManager.EVT_REGISTER_ENTITY` = `"RegisterUIEntity"`
- **参数**: `[string daoId, UIComponent component]`
- **返回**: `ResultCode.Ok(daoId)` / `ResultCode.Fail("参数无效")` / `ResultCode.Fail("UIComponent 缺失")` / `ResultCode.Fail("创建 UIEntity 失败")`
- **副作用**:
  - `UIComponent` 树写入 `UIComponents` 持久化分类（一次性 batch flush）
  - 在 Canvas 下递归创建 `UIEntity` GameObject 树
  - `_uiEntityCache[daoId] = entity`
- **示例**:
  ```csharp
  var panel = new UIPanelComponent("inv", "背包").SetSize(680, 560);
  panel.AddChild(new UIButtonComponent("inv_close", "关闭", "×").SetSize(36, 36));
  EventProcessor.Instance.TriggerEventMethod(
      "RegisterUIEntity", new List<object> { panel.Id, panel });
  ```

#### `UIManager.EVT_UNREGISTER_ENTITY` — 注销并销毁 UI 实体
- **常量**: `UIManager.EVT_UNREGISTER_ENTITY` = `"UnregisterUIEntity"`
- **参数**: `[string daoId]`
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail("参数无效")`
- **副作用**: `Object.Destroy(entity.gameObject)` + 从 `_uiEntityCache` 移除。**持久化的 UIComponent 数据保留**，下次重启可恢复
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "UnregisterUIEntity", new List<object> { "inv" });
  ```

#### `UIManager.EVT_HOT_RELOAD` — 热重载 UI 配置
- **常量**: `UIManager.EVT_HOT_RELOAD` = `"HotReloadUIConfigs"`
- **参数**: `[]`
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail("热重载失败")`
- **副作用**: 调 `UIService.LoadData()` 重读 `{persistentDataPath}/ServiceData/UIService/UIComponents.json`。已注册的 UIEntity 不会自动刷新——需重新 `EVT_REGISTER_ENTITY`
- **示例**: `EventProcessor.Instance.TriggerEventMethod("HotReloadUIConfigs", new List<object>());`

### 查询类

#### `UIManager.EVT_GET_ENTITY` — 获取 UIEntity 引用
- **常量**: `UIManager.EVT_GET_ENTITY` = `"GetUIEntity"`
- **参数**: `[string daoId]`
- **返回**: `ResultCode.Ok(UIEntity)` / `ResultCode.Fail("未找到实体")`
- **副作用**: 无（纯查缓存）
- **典型用途**: 探测 UI 是否已打开
- ⚠ 返回的是 `UIEntity` 类型（模块私有）；跨模块调用方应改用 `EVT_GET_UI_GAMEOBJECT`，返回中立 `GameObject`

#### `UIManager.EVT_GET_UI_GAMEOBJECT` — 按 daoId 取 GameObject（中立类型）
- **常量**: `UIManager.EVT_GET_UI_GAMEOBJECT` = `"GetUIGameObject"`
- **参数**: `[string daoId]`
- **返回**: `ResultCode.Ok(GameObject)` / `ResultCode.Fail($"UI GameObject 不存在: {daoId}")`
- **副作用**: 无
- **用途**: 跨模块需要某 UI 元素的 GameObject（拖拽 handler、可见性判断、临时 reparent 等）。返回 Unity 原生 `GameObject`，**调用方无需 `using UIManager.Entity`**
- **示例**:
  ```csharp
  var r = EventProcessor.Instance.TriggerEventMethod(
      "GetUIGameObject", new List<object> { "slot_1" });
  if (ResultCode.IsOk(r)) { var go = r[1] as GameObject; ... }
  ```

#### `UIManager.EVT_GET_CANVAS_TRANSFORM` — 取 UI Canvas 根 Transform
- **常量**: `UIManager.EVT_GET_CANVAS_TRANSFORM` = `"GetUICanvasTransform"`
- **参数**: `[]`
- **返回**: `ResultCode.Ok(Transform)` / `ResultCode.Fail("Canvas 未初始化")`
- **副作用**: 无
- **用途**: 外部读取 Canvas 逻辑尺寸 / 临时挂某个 UI 元素，避免 `using UIManager`

### 广播响应

#### `UIManager.EVT_DAO_PROPERTY_CHANGED` — UIComponent 属性变更转发
- **常量**: `UIManager.EVT_DAO_PROPERTY_CHANGED` = `"UIDaoPropertyChanged"`
- **参数**: `[string daoId, string propName, object value]`
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail("参数无效")` / `ResultCode.Fail("daoId 为空")`
- **副作用**: 查 `_uiEntityCache[daoId]` 转发到 `UIEntity.OnDaoPropertyChanged(propName, value)`
- **触发方**: `UIComponent.NotifyEntityPropertyChanged`（DAO 层不依赖 UIManager 类型，通过事件解耦广播）

## Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_uiCanvas` | （空） | 已有 Canvas 时拖入；否则启动时自动创建 `UICanvas` GameObject |
| `_referenceResolution` | `(1920, 1080)` | `CanvasScaler.referenceResolution` |
| `_matchWidthOrHeight` | `0.5` | `CanvasScaler.matchWidthOrHeight`，0=按宽 / 1=按高 / 0.5=折中 |

## Canvas / EventSystem 自动建立

`Initialize()` 时按需建立两件事：

1. **Canvas**：缺失则建 `UICanvas` GameObject，挂 `Canvas`(ScreenSpaceOverlay) + `CanvasScaler`(ScaleWithScreenSize) + `GraphicRaycaster`。
2. **EventSystem**：缺失则建带 `StandaloneInputModule` 的 GameObject。**没有 EventSystem 整个场景的 UGUI 按钮都收不到点击** —— UIManager 在这里自动兜底，业务模块无需关心。
   > 若项目改用 new Input System，需把 `StandaloneInputModule` 替换为 `InputSystemUIInputModule`。

## 文本清晰度（超采样方案）

框架仅用 uGUI `Text`（LegacyRuntime.ttf），**未引入 TextMeshPro**（参 Anti-Patterns §B10）。需高 DPI 文字时业务侧用整数倍超采样：

```csharp
component.SetFontSize(component.FontSize * 2)
         .SetSize(component.Size * 2)
         .SetScale(0.5f, 0.5f);
```

`Canvas.pixelPerfect = false`（与非整数 Scale 冲突会让超采样后的边缘出现毛刺）。

## 持久化

```
{persistentDataPath}/ServiceData/UIService/UIComponents.json
```

只保存 `UIComponent` DAO 树（纯 C# 数据）；`UIEntity` GameObject 运行时重建。

## 注意事项

- `UIComponent.Id` 必须唯一，`RegisterUIEntity` 用它作 daoId
- 跨模块**只走 bare-string**（§4.1）；不要为读 `EVT_*` 常量而 `using EssSystem.Core.Presentation.UIManager`（Anti-Patterns §A2）
- 跨模块返回值**只用 `GameObject` / `Transform` 等 Unity 中立类型**，不返 `UIEntity`（Anti-Patterns §A7）
- `UIEntity` 是 MonoBehaviour；它的 `OnDestroy` 应调 `UIService.Instance.UnregisterUIEntity(daoId)` 清理缓存（已在基类实现）
- 业务模块**禁止** `gameObject.AddComponent<Canvas/Button/Text/...>`（Anti-Patterns §B9）；只构造 `UIComponent` DAO，让 `UIEntityFactory` 实例化
