# ResourceManager 指南

## 概述

`ResourceManager`（`[Manager(0)]`）+ `ResourceService` 提供资源加载、缓存、外部文件支持与预加载配置。

## ResourceType

```csharp
public enum ResourceType { Prefab, Sprite, AudioClip, Texture, RuleTile }
```

## 通过 Event 调用（推荐）

所有事件名 **必须** 用常量（参见根目录 `Agent.md` §4.1）。`ResourceManager` 是 façade（公开 API），`ResourceService` 是底层实现，两层都暴露常量；外部调用方一律用 `ResourceManager.EVT_*`。

```csharp
// 同步获取
var r = EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_GET_SPRITE,
    new List<object> { "Sprites/UI/Button" });
if (ResultCode.IsOk(r)) { var sprite = r[1] as Sprite; }

// 异步加载
EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_LOAD_SPRITE_ASYNC,
    new List<object> { "Sprites/UI/Icon" });

// 预加载配置
EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_ADD_PRELOAD_CONFIG,
    new List<object> { "player", "Prefabs/Player", ResourceType.Prefab, false });

// 卸载
EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_UNLOAD_RESOURCE,
    new List<object> { "Prefabs/Player", false });
EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_UNLOAD_ALL_RESOURCES, new List<object>());
```

## Event API（公开 façade — 优先用这层）

### `EVT_GET_PREFAB` / `EVT_GET_SPRITE` / `EVT_GET_AUDIO_CLIP` / `EVT_GET_TEXTURE` / `EVT_GET_RULE_TILE` — 同步获取已缓存资源
- **常量**: `ResourceManager.EVT_GET_*` = `"GetPrefab"` / `"GetSprite"` / `"GetAudioClip"` / `"GetTexture"` / `"GetRuleTile"`
- **参数**: `[string path]`（相对 `Resources/`，不带扩展名）
- **返回**: `ResultCode.Ok(asset)` / `ResultCode.Fail("获取失败")`
- **副作用**: 先查缓存；**缓存未命中** + 非外部 → 自动 fallback 到同步 `Resources.Load<T>(path)`，并按 `_resourceSubfolderHints` 枚举常见子目录（`""`/`Tiles`/`Sprites`/`Sprites/Tiles`/`Sprites/UI`/`Sprites/Characters`/`Prefabs`/`Audio`）。命中即写回缓存；调用方可以直接传裸文件名如 `"GrasslandsGround"`。若 path 已含 `/` 或 `\`（调用方自带子目录），则只按原路径 `Resources.Load` 一次，不再叠加 hints。
- **启动自动加载**: `ResourceService.AutoLoadAllResources` 启动时双通道预热：
  1. **Editor** 模式：`AssetDatabase.FindAssets("t:<Type>")` 遍历所有 `Resources/` 下的资产，按**真实文件名**（`Path.GetFileNameWithoutExtension`）入缓存。解决 `m_Name` 落后于文件名（外部改名/移动未刷新）的问题。
  2. **Build / 二级兜底**：`Resources.LoadAll<T>("")` 按 `m_Name` 作 key 入缓存。
  覆盖类型：`GameObject` / `Sprite` / `AudioClip` / `Texture2D` / `RuleTile`。

### `EVT_GET_EXTERNAL_SPRITE` — 同步获取外部图片缓存
- **常量**: `ResourceManager.EVT_GET_EXTERNAL_SPRITE` = `"GetExternalSprite"`
- **参数**: `[string filePath]`（绝对路径）
- **返回**: `ResultCode.Ok(sprite)` / `ResultCode.Fail("获取失败")`

### `EVT_LOAD_PREFAB_ASYNC` / `EVT_LOAD_SPRITE_ASYNC` / `EVT_LOAD_RULE_TILE_ASYNC` — 异步加载 Unity 资源
- **常量**: `ResourceManager.EVT_LOAD_*_ASYNC` = `"LoadPrefabAsync"` / `"LoadSpriteAsync"` / `"LoadRuleTileAsync"`
- **参数**: `[string path]`
- **返回**: 透传 `ResourceService.LoadAsync` 的返回（`["加载中"]` 或缓存命中时 `[ResultCode.OK, asset]`）
- **副作用**: 触发 `Resources.LoadAsync`，完成后写入缓存

### `EVT_LOAD_EXTERNAL_SPRITE_ASYNC` — 异步加载外部图片
- **常量**: `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC` = `"LoadExternalSpriteAsync"`
- **参数**: `[string filePath]`（绝对路径）
- **返回**: `["加载中"]`
- **副作用**: 后台线程读文件 → 主线程组装 `Sprite` → 写缓存 → **广播** `EVT_EXTERNAL_IMAGE_LOADED` / `EVT_EXTERNAL_IMAGE_LOAD_FAILED`

### `EVT_ADD_PRELOAD_CONFIG` — 添加预加载配置（持久化）
- **常量**: `ResourceManager.EVT_ADD_PRELOAD_CONFIG` = `"AddPreloadConfig"`
- **参数**: `[string id, string path, ResourceType type, bool isExternal=false]`
- **返回**: `[ResultCode.OK]`
- **副作用**: 写入 `ResourceService/{type}.json`，**下次启动**自动预加载

### `EVT_UNLOAD_RESOURCE` — 卸载单个
- **常量**: `ResourceManager.EVT_UNLOAD_RESOURCE` = `"UnloadResource"`
- **参数**: `[string path, bool isExternal=false, string typeStr=null]`
  - 第 3 参数 `typeStr` 可选，若传 `"Sprite"` 等则只卸载匹配 TypeTag 的缓存条目；不传则把相同 FileName + IsExternal 的所有 TypeTag 一并清除
- **返回**: `[ResultCode.OK]` 或 `["资源未加载"]`

### `EVT_UNLOAD_ALL_RESOURCES` — 全量卸载
- **常量**: `ResourceManager.EVT_UNLOAD_ALL_RESOURCES` = `"UnloadAllResources"`
- **参数**: `[]`
- **返回**: `[ResultCode.OK]`

## Event API（Service 内部 / 低层 — 一般不直接调）

暴露在 `ResourceService` 上，提供给 façade 内部委托用，**外部模块勿调**：

| 常量 | 字符串 | 说明 |
|---|---|---|
| `ResourceService.EVT_DATA_LOADED` | `OnResourceDataLoaded` | DataManager 加载完成后通知 Service 跑预加载（由 `ResourceManager.Start()` 触发） |
| `ResourceService.EVT_GET_RESOURCE` | `GetResource` | 同步获取（参数 `[path, typeStr, isExternal?]`）；cache miss 时按 fallback 规则同步加载 |
| `ResourceService.EVT_LOAD_RESOURCE_ASYNC` | `LoadResourceAsync` | 异步加载 Unity 资源 |
| `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC` | `LoadExternalImageAsync` | 异步加载外部图片 |
| `ResourceService.EVT_ADD_RESOURCE_CONFIG` | `AddResourceConfig` | 写预加载配置 |

## 广播事件（订阅用）

| 常量 | 字符串 | 触发时机 | data |
|---|---|---|---|
| `ResourceService.EVT_EXTERNAL_IMAGE_LOADED` | `OnExternalImageLoaded` | 外部图片加载成功 | `[Dictionary{path, sprite}]` |
| `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED` | `OnExternalImageLoadFailed` | 外部图片加载失败 | `[Dictionary{path, error}]` |

用 `[EventListener(ResourceService.EVT_EXTERNAL_IMAGE_LOADED)]` 订阅。

## ⚠️ 注意：façade 与 Service 同名事件

`EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` 在 `ResourceManager` 与 `ResourceService` **同字符串注册**，因 `_eventMethods` 是 `Dictionary<string, ...>`，扫描期后注册者覆盖前者。当前 façade 实现会自调（递归），但因被 Service 覆盖，实际进入 Service 的 `Unload` / `UnloadAll`。后续若改为多播或修复递归，**必须**同步更新本节。

## 缓存优化

`ResourceKey` 结构体作为缓存键，包含 3 个字段：
- `FileName` — 路径中的文件名（不带扩展名，由 `Path.GetFileNameWithoutExtension` 归一）
- `IsExternal` — 是否外部文件
- `TypeTag` — 资源类型标签，由 `NormalizeTypeTag` 归一：`"Prefab"`↔`"GameObject"`、`"Texture"`↔`"Texture2D"`，其余（`Sprite`/`AudioClip`/`RuleTile`）原样

用 `TypeTag` 做 key 一部分，可以避免同文件名不同类型（极少但可能发生）的缓存碰撞。`ToString()` 返回 `"unity:Sprite:GrasslandsGround"` 这样的字符串用于 Inspector 展示。

## 持久化

预加载配置按 `ResourceType` 分类保存：
```
ResourceService/
├── Prefab.json
├── Sprite.json
├── AudioClip.json
├── Texture.json
└── RuleTile.json
```

## 外部文件加载

通过 `Task.Run` 在后台线程读取，再用 `MainThreadDispatcher` 切回主线程组装 `Sprite`/`Texture2D`。

## 路径规范

- Unity 资源：相对 `Resources/`，如 `"Sprites/UI/Button"` ↔ `Resources/Sprites/UI/Button.png`
- 外部文件：完整路径，如 `"C:/Images/x.png"`，传 `isExternal: true`

## 错误处理模板

```csharp
var r = EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_GET_SPRITE, new List<object> { path });
if (!ResultCode.IsOk(r) || r.Count < 2)
{
    LogWarning($"加载失败: {r?[0]}");
    return;
}
var sprite = r[1] as Sprite;
```
