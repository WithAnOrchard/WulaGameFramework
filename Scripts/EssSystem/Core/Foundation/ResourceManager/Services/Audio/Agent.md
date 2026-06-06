# AudioClipService
**Service** (`Service<AudioClipService>`)
## 职责
- 异步加载 AudioClip 资源
- 缓存 AudioClip 对象
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **Audio Event**.

- `EVT_GET_AUDIO_CLIP_ASYNC`
- `EVT_LOAD_AUDIO_CLIP_ASYNC`

## 文件结构
```
Services/Audio/
├── AudioClipService.cs
├── Agent.md
```
