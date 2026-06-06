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

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **EffectsManager Event**.

- `EffectsManager.EVT_PLAY_VFX`
- `EffectsManager.EVT_REGISTER_VFX`
- `EffectsManager.EVT_SCREEN_FLASH`
- `EffectsManager.EVT_STOP_ALL_VFX`
- `EffectsManager.EVT_STOP_VFX`
- `EffectsManager.EVT_UNREGISTER_VFX`

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
## 注意事项
- **跨模块只走 bare-string**（§4.1）；返 `string`（instanceId）等中立类型，不返自定义 `ActiveVfx` 结构（§A7）
- **prefab 懒加载**：`EVT_REGISTER_VFX` 不立即触发 ResourceManager —— 启动期注册大量 VFX 不会阻塞；首次 `EVT_PLAY_VFX` 时按需加载
- **对象池注意点**：
  - 实例化的 VFX 应在自身的 ParticleSystem `Stop on All Clear` 等设置上配合 autoDestroy 时长，避免回池后还在播放
  - 如果 VFX 有自带尾迹（TrailRenderer），归池前应禁用尾迹组件，否则切换位置会拉出错误尾迹
- **屏幕闪光独立 Canvas**：`EnsureFlashCanvas` 按需建一次，挂在 EffectsManager 自身 GameObject 下；不污染业务 UI 树
- **业务模块禁止**自己 `Instantiate(vfxPrefab)` —— 会绕过对象池，引发 GC 抖动
