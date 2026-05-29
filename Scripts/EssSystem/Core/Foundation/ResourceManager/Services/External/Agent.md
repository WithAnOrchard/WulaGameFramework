# ExternalImageService

**Service** (`Service<ExternalImageService>`)

## 职责

- 异步加载外部图片资源（从网络或本地文件系统）
- 缓存外部图片为 Sprite 对象
- 广播加载成功/失败事件

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_LOAD_EXTERNAL_IMAGE_ASYNC` | `LoadExternalImageAsync` | 异步加载外部图片，参数 `[string path]` → `Ok(Sprite)` / `Fail(msg)` |
| `EVT_EXTERNAL_IMAGE_LOADED` | `OnExternalImageLoaded` | 外部图片加载成功**广播**，参数 `[string path, Sprite sprite]` |
| `EVT_EXTERNAL_IMAGE_LOAD_FAILED` | `OnExternalImageLoadFailed` | 外部图片加载失败**广播**，参数 `[string path, string errorMsg]` |

## 文件结构

```
Services/External/
├── ExternalImageService.cs
├── Agent.md
```
