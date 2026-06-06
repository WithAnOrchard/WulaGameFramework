# SpriteService
**Service** (`Service<SpriteService>`)
## 职责
- 异步加载 Sprite 资源
- 缓存 Sprite 对象
- 支持多精灵图集注册
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **Sprite Event**.

- `EVT_GET_SPRITE_ASYNC`
- `EVT_LOAD_SPRITE_ASYNC`
- `EVT_REGISTER_SPRITE_SHEET`
- `EVT_REGISTER_SPRITE_TO_CACHE`

## 文件结构
```
Services/Sprite/
├── SpriteService.cs
├── Agent.md
```
