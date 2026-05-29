# RuleTileService

**Service** (`Service<RuleTileService>`)

## 职责

- 异步加载 RuleTile 资源
- 缓存 RuleTile 对象

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_RULE_TILE_ASYNC` | `GetRuleTileAsync` | 异步获取 RuleTile，参数 `[string path]` → `Ok(RuleTile)` |
| `EVT_LOAD_RULE_TILE_ASYNC` | `LoadRuleTileAsync` | 异步加载 RuleTile，参数 `[string path]` → `Ok(RuleTile)` |

## 文件结构

```
Services/RuleTile/
├── RuleTileService.cs
├── Agent.md
```
