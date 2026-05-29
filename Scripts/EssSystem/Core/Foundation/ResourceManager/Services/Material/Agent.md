# MaterialService

**Service** (`Service<MaterialService>`)

## 职责

- 异步加载 Material 资源
- 缓存 Material 对象

## Event API

| 常量 | 字符串值 | 用途 |
|---|---|---|
| `EVT_GET_MATERIAL_ASYNC` | `GetMaterialAsync` | 异步获取 Material，参数 `[string path]` → `Ok(Material)` |
| `EVT_LOAD_MATERIAL_ASYNC` | `LoadMaterialAsync` | 异步加载 Material，参数 `[string path]` → `Ok(Material)` |

## 文件结构

```
Services/Material/
├── MaterialService.cs
├── Agent.md
```
