# EffectsManager 指南

## 概述

`Presentation/EffectsManager`（`[Manager(6)]`）—— 视觉特效池化播放 + 屏幕闪光叠加。

| | 类 | 角色 |
|---|---|---|
| Manager | `EffectsManager` | MonoBehaviour 单例：VFX 对象池 + 屏幕闪光 overlay Canvas + 6 个 [Event] 入口 |
| Service | `EffectsService` | 纯 C# 单例：`vfxId → prefabPath` 映射持久化 |

业务消费典型路径：注册一次 `EVT_REGISTER_VFX("explosion", "VFX/Explosion")` → 战斗系统每次爆炸 `EVT_PLAY_VFX("explosion", pos, null, 1.5f)` → 1.5 秒后自动回收到池。

## 文件结构

```
Presentation/EffectsManager/
├── EffectsManager.cs   Manager（VFX 池 + 闪光 overlay + 6 个 [Event]）
├── EffectsService.cs   Service（vfxId → prefabPath 持久化）
└── Agent.md            本文档
```

## 数据流

```
业务侧                                 EffectsManager / EffectsService                   场景
                                     ─────────────────────────────                  ────────
TriggerEventMethod(EVT_REGISTER_VFX,
   ["explosion", "VFX/Explosion"])
       │
       ▼                              EffectsManager.RegisterVFX
                                          └─ EffectsService.SetRegistration  ──→ Registrations.json

TriggerEventMethod(EVT_PLAY_VFX,
   ["explosion", worldPos, null, 1.5f])
       │
       ▼                              EffectsManager.PlayVFX
                                          ├─ ResolvePrefab
                                          │     ├─ 缓存命中 → 复用
                                          │     └─ 缓存未命中：bare-string "GetPrefab" 加载  ──→ ResourceManager
                                          ├─ TakeFromPoolOrInstantiate                       ──→ 池/Instantiate
                                          ├─ SetActive(true) + 摆位置
                                          └─ AutoStopAfter(1.5s) 协程
                                                 └─ ReturnToPoolOrDestroy                  ──→ 池/Destroy
       ▼
Ok(instanceId="explosion#42")
```

## Event API

> 共 5 个命令 + 1 个查询/控制（StopAll）。跨模块走 bare-string（§4.1）。

### 命令类（VFX 注册）

#### `EffectsManager.EVT_REGISTER_VFX` — 注册 VFX 资源映射
- **常量**: `EffectsManager.EVT_REGISTER_VFX` = `"RegisterVFX"`
- **参数**: `[string vfxId, string prefabPath]`
- **返回**: `Ok(vfxId)` / `Fail`
- **副作用**: 写入运行时缓存 + `EffectsService` 持久化（下次启动自动恢复）；prefab 不立即加载，**首次 `EVT_PLAY_VFX` 时才走 ResourceManager 懒加载**
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod("RegisterVFX",
      new List<object> { "explosion", "VFX/Explosion" });
  EventProcessor.Instance.TriggerEventMethod("RegisterVFX",
      new List<object> { "hit_blood", "VFX/HitBlood" });
  ```

#### `EffectsManager.EVT_UNREGISTER_VFX` — 移除 VFX 注册
- **常量**: `EffectsManager.EVT_UNREGISTER_VFX` = `"UnregisterVFX"`
- **参数**: `[string vfxId]`
- **返回**: `Ok(vfxId)` / `Fail`
- **副作用**: 移除注册表项；**不影响**已经在场景中播放的实例

### 命令类（播放 / 停止）

#### `EffectsManager.EVT_PLAY_VFX` — 播放 VFX
- **常量**: `EffectsManager.EVT_PLAY_VFX` = `"PlayVFX"`
- **参数**: `[string vfxId, Vector3 worldPos, Quaternion? rotation, float? autoDestroy]`
- **返回**: `Ok(string instanceId)` / `Fail`
- **副作用**:
  - prefab 未缓存 → 走 `ResourceManager` 加载（bare-string `"GetPrefab"`）
  - 启用对象池：从池中复用闲置实例；池空则 `Instantiate`
  - 摆放至 `worldPos`，附加可选 rotation
  - `autoDestroy > 0` → 启动协程在该秒数后自动回收
- **`instanceId` 形态**: `{vfxId}#{seq}`，如 `"explosion#42"`；用于后续 `EVT_STOP_VFX`
- **示例**:
  ```csharp
  // 在伤害判定时播爆炸 1.5 秒后自动回收
  var r = EventProcessor.Instance.TriggerEventMethod("PlayVFX",
      new List<object> { "explosion", hit.point, null, 1.5f });
  if (ResultCode.IsOk(r)) {
      var instId = r[1] as string;
      // 可记下 instId，紧急停止用 EVT_STOP_VFX
  }
  ```

#### `EffectsManager.EVT_STOP_VFX` — 停止单个 VFX 实例
- **常量**: `EffectsManager.EVT_STOP_VFX` = `"StopVFX"`
- **参数**: `[string instanceId]`
- **返回**: `Ok(instanceId)` / `Fail`
- **副作用**: 取消 autoDestroy 协程；启用对象池则归还到池（SetActive false），否则 Destroy

#### `EffectsManager.EVT_STOP_ALL_VFX` — 停止所有 VFX
- **常量**: `EffectsManager.EVT_STOP_ALL_VFX` = `"StopAllVFX"`
- **参数**: `[]`
- **返回**: `Ok()`
- **典型用途**: 场景切换 / 玩家死亡时清场

### 命令类（屏幕闪光）

#### `EffectsManager.EVT_SCREEN_FLASH` — 屏幕闪光
- **常量**: `EffectsManager.EVT_SCREEN_FLASH` = `"PlayScreenFlash"`
- **参数**: `[Color color, float duration?=0.15f]`
- **返回**: `Ok()` / `Fail`
- **副作用**: 按需建独立 overlay Canvas（`sortingOrder = 32000`，**不依赖 UIManager**）；播放 0 → 峰值 → 0 的三角波（峰值在 30% 时刻）。使用 `Time.unscaledDeltaTime`，**暂停时也能闪**
- **典型用途**:
  ```csharp
  // 受伤红闪
  EventProcessor.Instance.TriggerEventMethod("PlayScreenFlash",
      new List<object> { new Color(1f, 0f, 0f, 0.6f), 0.2f });
  // 闪电亮场
  EventProcessor.Instance.TriggerEventMethod("PlayScreenFlash",
      new List<object> { new Color(1f, 1f, 1f, 0.9f), 0.08f });
  ```

## Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_enablePool` | `true` | 启用对象池：相同 vfxId 复用实例，避免反复 Instantiate / Destroy |
| `_maxPoolSizePerKey` | `8` | 每个 vfxId 的池上限；超出时新归还的实例直接 Destroy |
| `_flashCanvasSortingOrder` | `32000` | 屏幕闪光 Canvas 的 `sortingOrder`，需高于业务 UI |

## EffectsService 持久化

| 分类 | 键 | 类型 | 说明 |
|---|---|---|---|
| `Registrations` | `{vfxId}` | string | Prefab 路径（如 `"VFX/Explosion"`），通过 ResourceManager 加载 |

## 跨模块调用示例

```csharp
// 1) 启动期注册（一次性，可在 SceneInitializer 中跑）
EventProcessor.Instance.TriggerEventMethod("RegisterVFX",
    new List<object> { "explosion", "VFX/Explosion" });
EventProcessor.Instance.TriggerEventMethod("RegisterVFX",
    new List<object> { "hit_blood", "VFX/HitBlood" });

// 2) 战斗：受击血液飞溅 0.6 秒后自动回收
EventProcessor.Instance.TriggerEventMethod("PlayVFX",
    new List<object> { "hit_blood", hit.point, null, 0.6f });

// 3) 玩家受伤：红屏 + 震屏（与 CameraManager 协作）
EventProcessor.Instance.TriggerEventMethod("PlayScreenFlash",
    new List<object> { new Color(1f, 0f, 0f, 0.55f), 0.2f });
EventProcessor.Instance.TriggerEventMethod("ShakeCamera",
    new List<object> { 0.15f, 0.25f });

// 4) 场景切换：清场
EventProcessor.Instance.TriggerEventMethod("StopAllVFX", null);
```

## 注意事项

- **跨模块只走 bare-string**（§4.1）；返 `string`（instanceId）等中立类型，不返自定义 `ActiveVfx` 结构（§A7）
- **prefab 懒加载**：`EVT_REGISTER_VFX` 不立即触发 ResourceManager —— 启动期注册大量 VFX 不会阻塞；首次 `EVT_PLAY_VFX` 时按需加载
- **对象池注意点**：
  - 实例化的 VFX 应在自身的 ParticleSystem `Stop on All Clear` 等设置上配合 autoDestroy 时长，避免回池后还在播放
  - 如果 VFX 有自带尾迹（TrailRenderer），归池前应禁用尾迹组件，否则切换位置会拉出错误尾迹
- **屏幕闪光独立 Canvas**：`EnsureFlashCanvas` 按需建一次，挂在 EffectsManager 自身 GameObject 下；不污染业务 UI 树
- **业务模块禁止**自己 `Instantiate(vfxPrefab)` —— 会绕过对象池，引发 GC 抖动
