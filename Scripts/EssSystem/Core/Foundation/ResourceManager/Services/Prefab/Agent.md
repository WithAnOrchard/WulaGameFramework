# PrefabService

**Service** (`Service<PrefabService>`)

## 职责

- 异步加载 Prefab 资源
- 缓存 Prefab 对象

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_PREFAB_ASYNC` | `GetPrefabAsync` | 异步获取 Prefab，参数 `[string path]` → `Ok(GameObject)` |
| `EVT_LOAD_PREFAB_ASYNC` | `LoadPrefabAsync` | 异步加载 Prefab，参数 `[string path]` → `Ok(GameObject)` |

## 文件结构

```
Services/Prefab/
├── PrefabService.cs
├── Agent.md
```
