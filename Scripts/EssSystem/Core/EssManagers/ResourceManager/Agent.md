# ResourceManager 指南

## 概述

`ResourceManager`（`[Manager(0)]`）+ `ResourceService` 提供资源加载、缓存、外部文件支持与预加载配置。

## ResourceType

```csharp
public enum ResourceType { Prefab, Sprite, AudioClip, Texture }
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

### `EVT_GET_PREFAB` / `EVT_GET_SPRITE` / `EVT_GET_AUDIO_CLIP` / `EVT_GET_TEXTURE` — 同步获取已缓存资源
- **常量**: `ResourceManager.EVT_GET_*` = `"GetPrefab"` / `"GetSprite"` / `"GetAudioClip"` / `"GetTexture"`
- **参数**: `[string path]`（相对 `Resources/`，不带扩展名）
- **返回**: `ResultCode.Ok(asset)` / `ResultCode.Fail("获取失败")`
- **副作用**: 仅命中缓存，**不会**触发加载。未加载时返回 Fail。

### `EVT_GET_EXTERNAL_SPRITE` — 同步获取外部图片缓存
- **常量**: `ResourceManager.EVT_GET_EXTERNAL_SPRITE` = `"GetExternalSprite"`
- **参数**: `[string filePath]`（绝对路径）
- **返回**: `ResultCode.Ok(sprite)` / `ResultCode.Fail("获取失败")`

### `EVT_LOAD_PREFAB_ASYNC` / `EVT_LOAD_SPRITE_ASYNC` — 异步加载 Unity 资源
- **常量**: `ResourceManager.EVT_LOAD_*_ASYNC` = `"LoadPrefabAsync"` / `"LoadSpriteAsync"`
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
- **参数**: `[string path, bool isExternal=false]`
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
| `ResourceService.EVT_GET_RESOURCE` | `GetResource` | 同步获取（按 type 字符串区分） |
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

`ResourceKey` 结构体（`Path` + `IsExternal`）作为缓存键，避免字符串拼接。
- 减少 GC
- 提速字典查找
- 类型安全

## 持久化

预加载配置按 `ResourceType` 分类保存：
```
ResourceService/
├── Prefab.json
├── Sprite.json
├── AudioClip.json
└── Texture.json
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
