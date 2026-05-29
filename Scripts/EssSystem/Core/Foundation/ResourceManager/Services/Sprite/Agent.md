# SpriteService

**Service** (`Service<SpriteService>`)

## 职责

- 异步加载 Sprite 资源
- 缓存 Sprite 对象
- 支持多精灵图集注册

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_SPRITE_ASYNC` | `GetSpriteAsync` | 异步获取 Sprite，参数 `[string path]` → `Ok(Sprite)` / `Fail("加载中")` |
| `EVT_LOAD_SPRITE_ASYNC` | `LoadSpriteAsync` | 异步加载 Sprite，参数 `[string path]` → `Ok(Sprite)` / `Fail("加载中")` |
| `EVT_REGISTER_SPRITE_TO_CACHE` | `RegisterSpriteToCache` | 注册 Sprite 到缓存，参数 `[string spriteId, Sprite sprite]` → `Ok()` / `Fail(msg)` |
| `EVT_REGISTER_SPRITE_SHEET` | `RegisterSpriteSheet` | 批量注册多精灵图集子图入缓存，参数 `[string sheetResourcePath]` → `Ok(int addedCount)` / `Fail(msg)` |

## 文件结构

```
Services/Sprite/
├── SpriteService.cs
├── Agent.md
```
