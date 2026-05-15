# AudioManager 模块指南

## 概述

`EssSystem.Core.EssManagers.Foundation.AudioManager` 提供 BGM 和 SFX 音频管理。

- **AudioManager** (`[Manager(3)]`) — MonoBehaviour 单例，SFX 对象池 + BGM 淡入淡出
- **AudioService** — 纯 C# 单例，持久化音量设置和音效路径映射

音频资源通过 `ResourceManager`（bare-string `"GetAudioClip"`，§4.1）加载，进入缓存体系。

## 文件结构

```
Foundation/AudioManager/
├── AudioManager.cs   — Manager（MonoBehaviour 单例）
├── AudioService.cs   — Service（纯 C# 单例，持久化）
└── Agent.md          — 本文件
```

## 优先级

`[Manager(3)]` — 在 ResourceManager(0) 之后、UIManager(5) 之前。
由 `AbstractGameManager.EnsureBaseManagers` 自动挂载。

## Event API

### `EVT_PLAY_BGM` — 播放背景音乐（命令）
- **常量**: `AudioManager.EVT_PLAY_BGM` = `"PlayBGM"`
- **参数**: `[string path, bool fade?(默认 true)]`
- **返回**: `null`
- **副作用**: 通过 ResourceManager 加载 AudioClip，淡入播放

### `EVT_STOP_BGM` — 停止背景音乐（命令）
- **常量**: `AudioManager.EVT_STOP_BGM` = `"StopBGM"`
- **参数**: `[bool fade?(默认 true)]`
- **返回**: `null`

### `EVT_PAUSE_BGM` — 暂停背景音乐（命令）
- **常量**: `AudioManager.EVT_PAUSE_BGM` = `"PauseBGM"`
- **参数**: 无
- **返回**: `null`

### `EVT_RESUME_BGM` — 继续背景音乐（命令）
- **常量**: `AudioManager.EVT_RESUME_BGM` = `"ResumeBGM"`
- **参数**: 无
- **返回**: `null`

### `EVT_PLAY_SFX` — 播放自定义音效（命令）
- **常量**: `AudioManager.EVT_PLAY_SFX` = `"PlaySFX"`
- **参数**: `[string path, float volumeScale?(默认 1f)]`
- **返回**: `null`
- **副作用**: 通过 ResourceManager 加载 AudioClip，从 SFX 池取 AudioSource 播放

### `EVT_SET_MASTER_VOLUME` — 设置主音量（命令）
- **常量**: `AudioManager.EVT_SET_MASTER_VOLUME` = `"SetMasterVolume"`
- **参数**: `[float volume]`（0~1）
- **返回**: `null`
- **副作用**: 设置 `AudioListener.volume`

### `EVT_SET_BGM_VOLUME` — 设置 BGM 音量（命令）
- **常量**: `AudioManager.EVT_SET_BGM_VOLUME` = `"SetBGMVolume"`
- **参数**: `[float volume]`（0~1）
- **返回**: `null`

### `EVT_SET_SFX_VOLUME` — 设置 SFX 音量（命令）
- **常量**: `AudioManager.EVT_SET_SFX_VOLUME` = `"SetSFXVolume"`
- **参数**: `[float volume]`（0~1）
- **返回**: `null`

### `EVT_PLAY_DAMAGE_SFX` — 播放受伤音效（便捷命令）
- **常量**: `AudioManager.EVT_PLAY_DAMAGE_SFX` = `"PlayDamageSFX"`
- **参数**: 无
- **返回**: `null`
- **副作用**: 播放 `Sound/Bump`

### `EVT_PLAY_ATTACK_SFX` — 播放攻击音效（便捷命令）
- **常量**: `AudioManager.EVT_PLAY_ATTACK_SFX` = `"PlayAttackSFX"`
- **参数**: 无
- **返回**: `null`
- **副作用**: 随机播放 `Sound/Sword1` / `Sword2` / `Sword3`

### `EVT_PLAY_UI_SFX` — 播放 UI 操作音效（便捷命令）
- **常量**: `AudioManager.EVT_PLAY_UI_SFX` = `"PlayUISFX"`
- **参数**: 无
- **返回**: `null`
- **副作用**: 播放 `Sound/Bubble`

### `EVT_PLAY_ITEM_USE_SFX` — 播放物品使用音效（便捷命令）
- **常量**: `AudioManager.EVT_PLAY_ITEM_USE_SFX` = `"PlayItemUseSFX"`
- **参数**: 无
- **返回**: `null`
- **副作用**: 播放 `Sound/AppleUse`

## 跨模块调用示例

```csharp
// §4.1 跨模块消费方用 bare-string
EventProcessor.Instance?.TriggerEventMethod("PlayAttackSFX", null);
EventProcessor.Instance?.TriggerEventMethod("PlaySFX", new List<object> { "Sound/MyCustom", 0.8f });
EventProcessor.Instance?.TriggerEventMethod("PlayBGM", new List<object> { "Sound/BGM_Forest" });
```

## AudioService 持久化

| 分类 | 键 | 说明 |
|---|---|---|
| `Settings` | `MasterVolume` | 主音量 (float) |
| `Settings` | `BGMVolume` | BGM 音量 (float) |
| `Settings` | `SFXVolume` | SFX 音量 (float) |
| `Settings` | `BGMPath` | 当前 BGM 路径 (string) |
| `Resources` | `SFXPath_{name}` | 运行时音效路径映射（**Transient，不持久化**） |
