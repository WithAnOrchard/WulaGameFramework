# ExternalImageService
**Service** (`Service<ExternalImageService>`)
## 职责
- 异步加载外部图片资源（从网络或本地文件系统）
- 缓存外部图片为 Sprite 对象
- 广播加载成功/失败事件
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **External Event**.

- `EVT_EXTERNAL_IMAGE_LOAD_FAILED`
- `EVT_EXTERNAL_IMAGE_LOADED`
- `EVT_LOAD_EXTERNAL_IMAGE_ASYNC`

## 文件结构
```
Services/External/
├── ExternalImageService.cs
├── Agent.md
```
