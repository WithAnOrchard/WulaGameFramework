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

> 共 9 个命令 + 1 个查询。命令返 `Ok()` / `Fail`；查询返 `Ok(value)`。跨模块走 bare-string（§4.1）。

### 查询类

#### `CameraManager.EVT_GET_MAIN_CAMERA` — 取主相机引用
- **常量**: `CameraManager.EVT_GET_MAIN_CAMERA` = `"GetMainCamera"`
- **参数**: `[]`
- **返回**: `Ok(Camera)` / `Fail("主相机不存在")`
- **副作用**: 无；优先返 Inspector 注入的 `_mainCamera`，否则回落 `Camera.main`
- **示例**:
  ```csharp
  var r = EventProcessor.Instance.TriggerEventMethod("GetMainCamera", null);
  if (ResultCode.IsOk(r)) { var cam = r[1] as Camera; }
  ```

#### `CameraManager.EVT_WORLD_TO_SCREEN` — 世界 → 屏幕坐标
- **常量**: `CameraManager.EVT_WORLD_TO_SCREEN` = `"WorldToScreenPoint"`
- **参数**: `[Vector3 worldPos]`
- **返回**: `Ok(Vector2 screen)` / `Fail`
- **典型用途**: UI 锚点跟随世界中实体（血条、伤害飘字）

#### `CameraManager.EVT_SCREEN_TO_WORLD` — 屏幕 → 世界坐标
- **常量**: `CameraManager.EVT_SCREEN_TO_WORLD` = `"ScreenToWorldPoint"`
- **参数**: `[Vector2 screenPos, float zDistance?=10]`
- **返回**: `Ok(Vector3 world)` / `Fail`
- **典型用途**: 鼠标点击位置 → 世界坐标（地图建造、技能瞄准）

### 命令类（跟随）

#### `CameraManager.EVT_FOLLOW_TARGET` — 设置跟随目标
- **常量**: `CameraManager.EVT_FOLLOW_TARGET` = `"FollowCameraTarget"`
- **参数**: `[Transform target, Vector3 offset?]`
- **返回**: `Ok()` / `Fail`
- **副作用**: 每帧 LateUpdate 用 `Vector3.SmoothDamp` 平滑插值到 `target.position + offset`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "FollowCameraTarget",
      new List<object> { player.transform, new Vector3(0, 2, -10) });
  ```

#### `CameraManager.EVT_STOP_FOLLOW` — 停止跟随
- **常量**: `CameraManager.EVT_STOP_FOLLOW` = `"StopCameraFollow"`
- **参数**: `[]`
- **返回**: `Ok()`
- **副作用**: 清空跟随目标；相机停在当前位置

### 命令类（震屏）

#### `CameraManager.EVT_SHAKE` — 触发震屏
- **常量**: `CameraManager.EVT_SHAKE` = `"ShakeCamera"`
- **参数**: `[float amplitude, float duration, int frequency?=20]`
- **返回**: `Ok()` / `Fail`
- **副作用**: 在 XY 平面叠加 PerlinNoise 偏移；强度随时间线性衰减；最终偏移 = `amplitude × t × _shakeIntensityMultiplier`
- **示例**: 受击 0.3 秒小震 → `[0.15f, 0.3f]`；爆炸 0.6 秒大震 → `[0.5f, 0.6f, 30]`

### 命令类（缩放 / 位置）

#### `CameraManager.EVT_SET_ZOOM` — 设置缩放
- **常量**: `CameraManager.EVT_SET_ZOOM` = `"SetCameraZoom"`
- **参数**: `[float value, float duration?=0]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `duration > 0` → 线性插值；`= 0` → 立即设置。正交相机改 `orthographicSize`；透视相机改 `fieldOfView`（自动 clamp 1~179）

#### `CameraManager.EVT_SET_POSITION` — 瞬间设置相机位置
- **常量**: `CameraManager.EVT_SET_POSITION` = `"SetCameraPosition"`
- **参数**: `[Vector3 worldPos]`
- **返回**: `Ok()` / `Fail`
- **副作用**: 直接设 `transform.position`；清零跟随插值速度

#### `CameraManager.EVT_LOOK_AT` — 瞬间相机朝向某点
- **常量**: `CameraManager.EVT_LOOK_AT` = `"LookCameraAt"`
- **参数**: `[Vector3 worldPoint]`
- **返回**: `Ok()` / `Fail`
- **副作用**: `Camera.transform.LookAt(worldPoint)`

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

```csharp
// 1) 屏幕 UI 跟随实体血条
var r = EventProcessor.Instance.TriggerEventMethod("WorldToScreenPoint",
    new List<object> { enemy.transform.position + Vector3.up * 1.5f });
if (ResultCode.IsOk(r)) hpBar.position = (Vector2)r[1];

// 2) 战斗系统受击震屏
EventProcessor.Instance.TriggerEventMethod("ShakeCamera",
    new List<object> { 0.15f, 0.25f });

// 3) 剧情切到目标
EventProcessor.Instance.TriggerEventMethod("FollowCameraTarget",
    new List<object> { npc.transform, new Vector3(0, 2, -8) });
EventProcessor.Instance.TriggerEventMethod("SetCameraZoom",
    new List<object> { 35f, 1.0f });
```

## 注意事项

- **跨模块只走 bare-string**（§4.1）；返 `Camera` / `Vector2` / `Vector3` 等 Unity 中立类型（§A7）
- **跟随与震屏会叠加**：震屏在 `_shakeBasePosition`（跟随插值结果）之上加偏移；停止跟随后震屏在当前位置之上加偏移
- **震屏强度倍率持久化在 `CameraService`**，无障碍设置中允许玩家关闭（晕动症）
- **多相机场景**：当前 Manager 只管一个主相机；多相机切换可在 Inspector 注入或调 `SetMainCamera`（C# API，本期未暴露 Event；按需扩展）
- 业务模块**禁止**自己持有 `Camera.main` 长期引用 —— 走 `EVT_GET_MAIN_CAMERA`，主相机切换时一致
