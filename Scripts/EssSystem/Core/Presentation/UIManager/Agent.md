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
│   ├── CommonComponents/  UIPanelComponent / UIButtonComponent / UITextComponent / UIBarComponent（运行时 DAO）
│   └── Specs/             UIPanelSpec / UIButtonSpec / UITextSpec / UIIconSpec
│                          （[Serializable] 纯数据 + With* 链式 + CreateComponent 工厂，业务模块持久化/复用首选）
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

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **UIManager Event**.

- `UIManager.EVT_ADD_WINDOW_BEHAVIOR`
- `UIManager.EVT_DAO_PROPERTY_CHANGED`
- `UIManager.EVT_GET_CANVAS_TRANSFORM`
- `UIManager.EVT_GET_ENTITY`
- `UIManager.EVT_GET_UI_GAMEOBJECT`
- `UIManager.EVT_HOT_RELOAD`
- `UIManager.EVT_REGISTER_ENTITY`
- `UIManager.EVT_UNREGISTER_ENTITY`

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
