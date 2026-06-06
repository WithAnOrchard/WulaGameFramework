# CameraManager 指南
## 概述
`Presentation/CameraManager`（`[Manager(4)]`）—— 主相机引用、跟随、震屏、缩放、世界↔屏幕坐标转换。
| | 类 | 角色 |
|---|---|---|
| Manager | `CameraManager` | MonoBehaviour 单例：每帧 LateUpdate 驱动跟随/震屏/缩放，10 个 [Event] 入口 |
| Service | `CameraService` | 纯 C# 单例：相机偏好持久化（平滑时间、震屏倍率、默认缩放） |
跨模块用法：UI 模块查 `Camera.main` → 走 `EVT_GET_MAIN_CAMERA`；战斗系统受击震屏 → 走 `EVT_SHAKE`；剧情/技能系统切镜头 → `EVT_FOLLOW_TARGET` + `EVT_SET_ZOOM`。
## 文件结构
```
Presentation/CameraManager/
├── CameraManager.cs   Manager（MonoBehaviour 单例 + Inspector + 10 个 [Event]）
├── CameraService.cs   Service（持久化偏好）
└── Agent.md           本文档
```
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **CameraManager Event**.

- `CameraManager.EVT_FOLLOW_TARGET`
- `CameraManager.EVT_GET_MAIN_CAMERA`
- `CameraManager.EVT_LOOK_AT`
- `CameraManager.EVT_SCREEN_TO_WORLD`
- `CameraManager.EVT_SET_POSITION`
- `CameraManager.EVT_SET_ZOOM`
- `CameraManager.EVT_SHAKE`
- `CameraManager.EVT_STOP_FOLLOW`
- `CameraManager.EVT_WORLD_TO_SCREEN`

## Inspector 字段
| 字段 | 默认 | 说明 |
|---|---|---|
| `_mainCamera` | （空） | 主相机引用；为空启动时自动取 `Camera.main` |
| `_followSmoothTime` | `0.15` | 跟随平滑时间（秒）；越小越紧、0 = 瞬间锁定 |
| `_followOffset` | `(0,0,0)` | 默认跟随偏移；`FOLLOW_TARGET` 未传 offset 时用本值 |
| `_shakeIntensityMultiplier` | `1.0` | 震屏强度全局倍率（0~2）；0 = 关闭震屏（晕动症辅助） |
## CameraService 持久化
| 分类 | 键 | 类型 | 默认 | 说明 |
|---|---|---|---|---|
| `Settings` | `FollowSmoothTime` | float | `0.15` | 跟随平滑时间 |
| `Settings` | `ShakeIntensityMultiplier` | float | `1.0` | 震屏倍率（晕动症辅助） |
| `Settings` | `DefaultOrthoSize` | float | `5.0` | 默认正交尺寸 |
| `Settings` | `DefaultFieldOfView` | float | `60.0` | 默认透视 FOV |
## 跨模块调用示例
## 注意事项
- **跨模块只走 bare-string**（§4.1）；返 `Camera` / `Vector2` / `Vector3` 等 Unity 中立类型（§A7）
- **跟随与震屏会叠加**：震屏在 `_shakeBasePosition`（跟随插值结果）之上加偏移；停止跟随后震屏在当前位置之上加偏移
- **震屏强度倍率持久化在 `CameraService`**，无障碍设置中允许玩家关闭（晕动症）
- **多相机场景**：当前 Manager 只管一个主相机；多相机切换可在 Inspector 注入或调 `SetMainCamera`（C# API，本期未暴露 Event；按需扩展）
- 业务模块**禁止**自己持有 `Camera.main` 长期引用 —— 走 `EVT_GET_MAIN_CAMERA`，主相机切换时一致
