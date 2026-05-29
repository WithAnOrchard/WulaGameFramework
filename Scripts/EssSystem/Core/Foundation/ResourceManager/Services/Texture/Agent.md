# TextureService

**Service** (`Service<TextureService>`)

## 职责

- 异步加载 Texture 资源
- 缓存 Texture 对象

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_TEXTURE_ASYNC` | `GetTextureAsync` | 异步获取 Texture，参数 `[string path]` → `Ok(Texture)` |
| `EVT_LOAD_TEXTURE_ASYNC` | `LoadTextureAsync` | 异步加载 Texture，参数 `[string path]` → `Ok(Texture)` |

## 文件结构

```
Services/Texture/
├── TextureService.cs
├── Agent.md
```
