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

> 共 12 个：4 BGM 控制 + 5 SFX 播放（1 自定义 + 4 内置便捷）+ 3 音量设置。**全部为命令**，调用方"发后不管"，**返回 `null`**。

### 命令类（BGM 控制）

#### `AudioManager.EVT_PLAY_BGM` — 播放背景音乐
- **常量**: `AudioManager.EVT_PLAY_BGM` = `"PlayBGM"`
- **参数**: `[string path, bool fade?=true]`
- **返回**: `null`
- **副作用**: 通过 `ResourceManager`（bare-string `"GetAudioClip"`）加载 AudioClip → `AudioService.SetCurrentBGMPath` 持久化 → 切换到 BGM AudioSource 淡入播放
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "PlayBGM", new List<object> { "Sound/BGM_Forest" });
  ```

#### `AudioManager.EVT_STOP_BGM` — 停止背景音乐
- **常量**: `AudioManager.EVT_STOP_BGM` = `"StopBGM"`
- **参数**: `[bool fade?=true]`
- **返回**: `null`
- **副作用**: BGM AudioSource 停止；可选淡出

#### `AudioManager.EVT_PAUSE_BGM` — 暂停背景音乐
- **常量**: `AudioManager.EVT_PAUSE_BGM` = `"PauseBGM"`
- **参数**: `[]`
- **返回**: `null`
- **副作用**: BGM AudioSource `Pause()`

#### `AudioManager.EVT_RESUME_BGM` — 继续背景音乐
- **常量**: `AudioManager.EVT_RESUME_BGM` = `"ResumeBGM"`
- **参数**: `[]`
- **返回**: `null`
- **副作用**: BGM AudioSource `UnPause()`

### 命令类（SFX 播放）

#### `AudioManager.EVT_PLAY_SFX` — 播放自定义音效
- **常量**: `AudioManager.EVT_PLAY_SFX` = `"PlaySFX"`
- **参数**: `[string path, float volumeScale?=1f]`
- **返回**: `null`
- **副作用**: 通过 `ResourceManager` 加载 AudioClip → 从 SFX 对象池取空闲 AudioSource → `PlayOneShot(clip, volumeScale)`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "PlaySFX", new List<object> { "Sound/MyCustom", 0.8f });
  ```

#### `AudioManager.EVT_PLAY_DAMAGE_SFX` — 播放受伤音效（便捷）
- **常量**: `AudioManager.EVT_PLAY_DAMAGE_SFX` = `"PlayDamageSFX"`
- **参数**: `[]`
- **返回**: `null`
- **副作用**: 等价于 `PlaySFX("Sound/Bump")`

#### `AudioManager.EVT_PLAY_ATTACK_SFX` — 播放攻击音效（便捷）
- **常量**: `AudioManager.EVT_PLAY_ATTACK_SFX` = `"PlayAttackSFX"`
- **参数**: `[]`
- **返回**: `null`
- **副作用**: 从 `Sound/Sword1`/`Sword2`/`Sword3` 中**随机**挑一个播放

#### `AudioManager.EVT_PLAY_UI_SFX` — 播放 UI 操作音效（便捷）
- **常量**: `AudioManager.EVT_PLAY_UI_SFX` = `"PlayUISFX"`
- **参数**: `[]`
- **返回**: `null`
- **副作用**: 等价于 `PlaySFX("Sound/Bubble")`

#### `AudioManager.EVT_PLAY_ITEM_USE_SFX` — 播放物品使用音效（便捷）
- **常量**: `AudioManager.EVT_PLAY_ITEM_USE_SFX` = `"PlayItemUseSFX"`
- **参数**: `[]`
- **返回**: `null`
- **副作用**: 等价于 `PlaySFX("Sound/AppleUse")`

### 命令类（位置循环音 / 环境音）

> 适用场景：营火、流水、机械、风车等持续环境音。**业务侧不要自己 `AddComponent<AudioSource>`** —— 走以下两个事件，由 AudioManager 持有 `AudioSource` 注册表，自动按 `AudioListener`（主相机）距离做线性衰减，并随 `SFXVolume` 同步音量。

#### `AudioManager.EVT_PLAY_POSITIONAL_LOOP_SFX` — 在指定 Transform 挂 3D 循环音
- **常量**: `AudioManager.EVT_PLAY_POSITIONAL_LOOP_SFX` = `"PlayPositionalLoopSFX"`
- **参数**: `[string clipPath, Transform anchor, float minDistance?=1.5, float maxDistance?=12, float volumeScale?=1]`
  - `clipPath`：走 `ResourceManager.GetAudioClip` 加载（命中缓存）
  - `anchor`：音源挂载节点；anchor.gameObject 销毁时音源同时销毁
  - `minDistance` / `maxDistance`：Linear rolloff 范围（< minDistance 满音量，> maxDistance 完全静音）
  - `volumeScale`：相对 `SFXVolume` 的倍率
- **返回**: `Ok(string handleId)` / `Fail(msg)`，handleId 用于后续 Stop
- **副作用**:
  - 在 `anchor` 上 `AddComponent<AudioSource>` 并 `Play()`，配置 `spatialBlend=1` + `Linear rolloff` + `dopplerLevel=0`
  - 把 (handleId, src, volumeScale) 写入 `_positionalSources` 注册表
  - 后续 `EVT_SET_SFX_VOLUME` 会自动遍历注册表刷新 volume
- **示例**:
  ```csharp
  var r = EventProcessor.Instance.TriggerEventMethod(
      "PlayPositionalLoopSFX",
      new List<object> { "Sound/feuer", transform, 1.5f, 12f, 1f });
  string handle = ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as string : null;
  ```

#### `AudioManager.EVT_STOP_POSITIONAL_SFX` — 停止位置循环音
- **常量**: `AudioManager.EVT_STOP_POSITIONAL_SFX` = `"StopPositionalSFX"`
- **参数**: `[string handleId]`（来自 `EVT_PLAY_POSITIONAL_LOOP_SFX` 的返回）
- **返回**: `Ok(handleId)` / `Fail("handleId 未注册: ...")`
- **副作用**: `Stop()` + `Destroy(AudioSource)` + 移出注册表。anchor.gameObject 已被 Unity 销毁时本调用安全（注册表 entry 仍清理）
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "StopPositionalSFX", new List<object> { handle });
  ```

### 命令类（音量设置）

> 三档音量都会写入 `AudioService` 持久化分类 `Settings`，下次启动自动恢复。

#### `AudioManager.EVT_SET_MASTER_VOLUME` — 设置主音量
- **常量**: `AudioManager.EVT_SET_MASTER_VOLUME` = `"SetMasterVolume"`
- **参数**: `[float volume]`（0~1）
- **返回**: `null`
- **副作用**: 设置 `AudioListener.volume` + 持久化

#### `AudioManager.EVT_SET_BGM_VOLUME` — 设置 BGM 音量
- **常量**: `AudioManager.EVT_SET_BGM_VOLUME` = `"SetBGMVolume"`
- **参数**: `[float volume]`（0~1）
- **返回**: `null`
- **副作用**: BGM AudioSource volume + 持久化

#### `AudioManager.EVT_SET_SFX_VOLUME` — 设置 SFX 音量
- **常量**: `AudioManager.EVT_SET_SFX_VOLUME` = `"SetSFXVolume"`
- **参数**: `[float volume]`（0~1）
- **返回**: `null`
- **副作用**: SFX 池所有 AudioSource volume + 持久化

## AudioService 持久化

| 分类 | 键 | 类型 | 说明 |
|---|---|---|---|
| `Settings` | `MasterVolume` | float | 主音量（0~1） |
| `Settings` | `BGMVolume` | float | BGM 音量 |
| `Settings` | `SFXVolume` | float | SFX 音量 |
| `Settings` | `BGMPath` | string | 当前 BGM 路径（重启后自动恢复播放） |
| `Resources` | `SFXPath_{name}` | string | 运行时 SFX 路径映射（**Transient**，不持久化） |

## 跨模块调用示例

```csharp
// §4.1 跨模块消费方一律 bare-string
EventProcessor.Instance?.TriggerEventMethod("PlayAttackSFX", null);
EventProcessor.Instance?.TriggerEventMethod("PlaySFX",
    new List<object> { "Sound/MyCustom", 0.8f });
EventProcessor.Instance?.TriggerEventMethod("PlayBGM",
    new List<object> { "Sound/BGM_Forest" });
EventProcessor.Instance?.TriggerEventMethod("SetMasterVolume",
    new List<object> { 0.5f });
```

## 注意事项

- 音频文件需放在 `Resources/Sound/` 或其它 `Resources/` 子目录；`AudioManager` 通过 `ResourceManager` 加载，不直接 `Resources.Load`（Anti-Patterns §B4）
- SFX 池由 `AudioManager` 内部管理，避免每次 `PlayOneShot` 都 new `AudioSource`
- 业务模块**禁止**直接 `AudioSource.Play` —— 走事件让 `AudioManager` 统一调度音量、池、淡入淡出
- 跨模块调用一律 bare-string；不要为读 `EVT_*` 常量而 `using EssSystem.Core.Presentation.AudioManager`（Anti-Patterns §A2）
