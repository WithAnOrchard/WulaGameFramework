# AudioClipService

**Service** (`Service<AudioClipService>`)

## 职责

- 异步加载 AudioClip 资源
- 缓存 AudioClip 对象

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_AUDIO_CLIP_ASYNC` | `GetAudioClipAsync` | 异步获取 AudioClip，参数 `[string path]` → `Ok(AudioClip)` |
| `EVT_LOAD_AUDIO_CLIP_ASYNC` | `LoadAudioClipAsync` | 异步加载 AudioClip，参数 `[string path]` → `Ok(AudioClip)` |

## 文件结构

```
Services/Audio/
├── AudioClipService.cs
├── Agent.md
```
