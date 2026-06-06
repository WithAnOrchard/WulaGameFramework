# AudioManager 指南
## 概述
`Presentation/AudioManager`（`[Manager(3)]`）—— BGM + SFX 音频管理。
| | 类 | 角色 |
|---|---|---|
| Manager | `AudioManager` | MonoBehaviour 单例，SFX 对象池 + BGM 淡入淡出 |
| Service | `AudioService` | 纯 C# 单例，持久化音量设置 + 运行时 SFX 路径映射 |
音频资源通过 `ResourceManager` 加载（bare-string `"GetAudioClip"`，§4.1），进入统一缓存。
## 文件结构
```
Presentation/AudioManager/
├── AudioManager.cs   Manager（MonoBehaviour 单例 + Inspector + SFX 池）
├── AudioService.cs   Service（持久化音量 + SFX 路径映射）
└── Agent.md          本文档
```
## 优先级 / 启动
`[Manager(3)]` —— 介于 `ResourceManager(0)` 之后、`UIManager(5)` 之前。由 `AbstractGameManager.EnsureBaseManagers` 自动挂载。
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **AudioManager Event**.

- `AudioManager.EVT_PAUSE_BGM`
- `AudioManager.EVT_PLAY_ATTACK_SFX`
- `AudioManager.EVT_PLAY_BGM`
- `AudioManager.EVT_PLAY_DAMAGE_SFX`
- `AudioManager.EVT_PLAY_ITEM_USE_SFX`
- `AudioManager.EVT_PLAY_POSITIONAL_LOOP_SFX`
- `AudioManager.EVT_PLAY_SFX`
- `AudioManager.EVT_PLAY_UI_SFX`
- `AudioManager.EVT_RESUME_BGM`
- `AudioManager.EVT_SET_BGM_VOLUME`
- `AudioManager.EVT_SET_MASTER_VOLUME`
- `AudioManager.EVT_SET_SFX_VOLUME`
- `AudioManager.EVT_STOP_BGM`
- `AudioManager.EVT_STOP_POSITIONAL_SFX`

## AudioService 持久化
| 分类 | 键 | 类型 | 说明 |
|---|---|---|---|
| `Settings` | `MasterVolume` | float | 主音量（0~1） |
| `Settings` | `BGMVolume` | float | BGM 音量 |
| `Settings` | `SFXVolume` | float | SFX 音量 |
| `Settings` | `BGMPath` | string | 当前 BGM 路径（重启后自动恢复播放） |
| `Resources` | `SFXPath_{name}` | string | 运行时 SFX 路径映射（**Transient**，不持久化） |
## 跨模块调用示例
## 注意事项
- 音频文件需放在 `Resources/Sound/` 或其它 `Resources/` 子目录；`AudioManager` 通过 `ResourceManager` 加载，不直接 `Resources.Load`（Anti-Patterns §B4）
- SFX 池由 `AudioManager` 内部管理，避免每次 `PlayOneShot` 都 new `AudioSource`
- 业务模块**禁止**直接 `AudioSource.Play` —— 走事件让 `AudioManager` 统一调度音量、池、淡入淡出
- 跨模块调用一律 bare-string；不要为读 `EVT_*` 常量而 `using EssSystem.Core.Presentation.AudioManager`（Anti-Patterns §A2）
