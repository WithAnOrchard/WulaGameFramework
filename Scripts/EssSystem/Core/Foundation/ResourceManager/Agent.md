# ResourceManager 指南

## 概述

`Foundation/ResourceManager`（`[Manager(0)]`）—— 资源加载/缓存/外部图片/预加载配置的统一入口。

| | 类 | 角色 |
|---|---|---|
| Manager | `ResourceManager` | Façade，外部调用方走这层；6 个 sync getter + 3 个 async loader + 外部 Sprite + 卸载 + 配置 |
| Service | `ResourceService` | 底层实现，缓存（`ResourceKey` 字典）+ Editor/Build 双路索引 + FBX manifest |

> 跨模块调用一律 bare-string（§4.1）。本文档展示常量是为了让你看清字符串值；实际调用时直接传字符串。

## 文件结构

```
Foundation/ResourceManager/
├── ResourceManager.cs   Façade（公开 API：6 sync + 3 async + 4 配置/卸载）
├── ResourceService.cs   底层（缓存 + 索引 + FBX manifest + 外部图片）
└── Agent.md             本文档
```

## 启动 / 数据流

```
1. ResourceManager.Start()
       │ TriggerEventMethod(ResourceService.EVT_DATA_LOADED)
       ▼
2. ResourceService.OnDataLoaded
       ├─ PreloadConfiguredResources()       按 DataService 配置异步预加载
       ├─ IndexAllResources()                全量索引 Resources/
       │     ├─ [Editor] AssetDatabase 按真实文件名建索引（容忍 m_Name 落后）
       │     ├─ [Editor] EditorIndexModelClipNames 顺手把 FBX 内 clip 入缓存
       │     ├─ LoadFBXManifestIfPresent     读 Resources/CharacterFBXManifest.json
       │     └─ Resources.LoadAll 按 m_Name 兜底（Editor + Build 都跑）
       └─ 广播 EVT_RESOURCES_LOADED
       ▼
3. 业务模块通过 [EventListener("OnResourcesLoaded")] 等待资源就绪
```

## 支持的资源类型

```csharp
public enum ResourceType { Prefab, Sprite, AudioClip, Texture, RuleTile, AnimationClip }
```

> **FBX 模型**：`Resources/` 下 `.fbx` 根资产是 `GameObject`，用 `EVT_GET_PREFAB` 取。其内部的 `AnimationClip` 子资产在启动时按 `clip.name` 索引到全局缓存，用 `EVT_GET_ANIMATION_CLIP` 直接按名取。

## Event API

> 共 18 个：6 façade sync getter + 1 外部 sync + 3 façade async loader + 1 外部 async + 1 配置 + 2 卸载 + 4 Service 内部 + 3 广播。

### 命令类（façade — 同步获取）

> 缓存命中直接返；未命中且非外部 → 自动 fallback 按候选子目录 `Resources.Load`。子目录候选（按命中概率）：`""` / `Tiles` / `Sprites` / `Sprites/Tiles` / `Sprites/UI` / `Sprites/Characters` / `Prefabs` / `Audio` / `Sound` / `Models` / `Models/Characters3D`。路径已含 `/` 时只按原路径试一次，不叠加。

#### `ResourceManager.EVT_GET_PREFAB` — 同步取 Prefab（含 FBX 根）
- **常量**: `ResourceManager.EVT_GET_PREFAB` = `"GetPrefab"`
- **参数**: `[string path]`（相对 `Resources/`，不带扩展名）
- **返回**: `ResultCode.Ok(GameObject)` / `ResultCode.Fail("获取失败")`
- **副作用**: 缓存命中无副作用；未命中走 fallback `Resources.Load<GameObject>` 后写回缓存
- **示例**:
  ```csharp
  var r = EventProcessor.Instance.TriggerEventMethod(
      "GetPrefab", new List<object> { "Prefabs/Player" });
  if (ResultCode.IsOk(r)) { var prefab = r[1] as GameObject; }
  ```

#### `ResourceManager.EVT_GET_SPRITE` — 同步取 Sprite
- **常量**: `ResourceManager.EVT_GET_SPRITE` = `"GetSprite"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(Sprite)` / `ResultCode.Fail("获取失败")`
- **副作用**: fallback 命中后写缓存。子图兜底：若按候选路径仍 miss 且 `typeStr=="Sprite"`，会 `Resources.LoadAll<Sprite>` 各 hint 子目录按 `sprite.name == fileName` 找匹配子图

#### `ResourceManager.EVT_GET_AUDIO_CLIP` — 同步取 AudioClip
- **常量**: `ResourceManager.EVT_GET_AUDIO_CLIP` = `"GetAudioClip"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(AudioClip)` / `ResultCode.Fail("获取失败")`
- **副作用**: fallback 写缓存。`AudioManager` 内部走这条路加载 BGM / SFX

#### `ResourceManager.EVT_GET_TEXTURE` — 同步取 Texture2D
- **常量**: `ResourceManager.EVT_GET_TEXTURE` = `"GetTexture"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(Texture2D)` / `ResultCode.Fail("获取失败")`
- **副作用**: fallback 写缓存

#### `ResourceManager.EVT_GET_MATERIAL` — 同步取 Material
- **常量**: `ResourceManager.EVT_GET_MATERIAL` = `"GetMaterial"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(Material)` / `ResultCode.Fail("获取失败")`
- **副作用**: fallback 写缓存。`LightManager` 内部走这条路加载天空盒 Material

#### `ResourceManager.EVT_GET_RULE_TILE` — 同步取 RuleTile
- **常量**: `ResourceManager.EVT_GET_RULE_TILE` = `"GetRuleTile"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(RuleTile)` / `ResultCode.Fail("获取失败")`
- **副作用**: fallback 写缓存。`MapManager` 通过这条路加载地形 RuleTile

#### `ResourceManager.EVT_GET_ANIMATION_CLIP` — 按 clip 名取 AnimationClip（含 FBX 子资产）
- **常量**: `ResourceManager.EVT_GET_ANIMATION_CLIP` = `"GetAnimationClip"`
- **参数**: `[string clipName]`（FBX 内 take 名 / `.anim` 文件名）
- **返回**: `ResultCode.Ok(AnimationClip)` / `ResultCode.Fail("获取失败")`
- **副作用**: 启动时已把 Resources/ 下所有 AnimationClip（含 FBX 子资产）按 `clip.name` 入缓存；这里只查缓存，不会触发 Resources.Load

#### `ResourceManager.EVT_GET_EXTERNAL_SPRITE` — 同步取外部图片缓存
- **常量**: `ResourceManager.EVT_GET_EXTERNAL_SPRITE` = `"GetExternalSprite"`
- **参数**: `[string filePath]`（操作系统绝对路径）
- **返回**: `ResultCode.Ok(Sprite)` / `ResultCode.Fail("获取失败")`
- **副作用**: 仅查缓存（外部图片必须先经 `EVT_LOAD_EXTERNAL_SPRITE_ASYNC` 加载后才能命中）

### 命令类（façade — 异步加载）

> 缓存命中直接返 `Ok(cached)`；否则触发 `Resources.LoadAsync` 并立刻返回 `Fail("加载中")` 作为 sentinel，回调写缓存后下次 `Get` 即可命中。

#### `ResourceManager.EVT_LOAD_PREFAB_ASYNC` — 异步加载 Prefab
- **常量**: `ResourceManager.EVT_LOAD_PREFAB_ASYNC` = `"LoadPrefabAsync"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(GameObject)` / `ResultCode.Fail("加载中")`
- **副作用**: 后台 `Resources.LoadAsync` 完成时入缓存

#### `ResourceManager.EVT_LOAD_SPRITE_ASYNC` — 异步加载 Sprite
- **常量**: `ResourceManager.EVT_LOAD_SPRITE_ASYNC` = `"LoadSpriteAsync"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(Sprite)` / `ResultCode.Fail("加载中")`
- **副作用**: 同上

#### `ResourceManager.EVT_LOAD_RULE_TILE_ASYNC` — 异步加载 RuleTile
- **常量**: `ResourceManager.EVT_LOAD_RULE_TILE_ASYNC` = `"LoadRuleTileAsync"`
- **参数**: `[string path]`
- **返回**: `ResultCode.Ok(RuleTile)` / `ResultCode.Fail("加载中")`
- **副作用**: 同上

#### `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC` — 异步加载外部图片
- **常量**: `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC` = `"LoadExternalSpriteAsync"`
- **参数**: `[string filePath]`（绝对路径）
- **返回**: `ResultCode.Ok(Sprite)` / `ResultCode.Fail("加载中")` / `ResultCode.Fail("文件不存在")`
- **副作用**: 后台 `Task.Run` 读 bytes → `MainThreadDispatcher` 切回主线程组装 `Texture2D` + `Sprite` → 入缓存 → 广播 `EVT_EXTERNAL_IMAGE_LOADED` / `EVT_EXTERNAL_IMAGE_LOAD_FAILED`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "LoadExternalSpriteAsync",
      new List<object> { "C:/Users/.../avatar.png" });
  ```

### 命令类（配置 / 卸载）

#### `ResourceManager.EVT_ADD_PRELOAD_CONFIG` — 添加预加载项
- **常量**: `ResourceManager.EVT_ADD_PRELOAD_CONFIG` = `"AddPreloadConfig"`
- **参数**: `[string id, string path, ResourceType type, bool isExternal?]`
- **返回**: `ResultCode.Ok()`
- **副作用**: 写入 `ResourceService/{type}.json` 持久化分类，**下次启动**自动跑 `PreloadConfiguredResources`
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      "AddPreloadConfig",
      new List<object> { "player", "Prefabs/Player", ResourceType.Prefab, false });
  ```

#### `ResourceManager.EVT_UNLOAD_RESOURCE` — 卸载单个资源
- **常量**: `ResourceManager.EVT_UNLOAD_RESOURCE` = `"UnloadResource"`
  > 字符串值与 `ResourceService.EVT_UNLOAD_RESOURCE` **相同**；扫描期 Service 上的 `[Event]` 实现覆盖了 façade 的常量，调用方使用任一常量都进入 Service 实现。
- **参数**: `[string path, bool isExternal?, string typeStr?]`
  > 第 3 参数 `typeStr` 可选；不传则按 FileName + IsExternal 卸载所有 TypeTag；传 `"Sprite"` 等只卸载匹配
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail("资源未加载")`
- **副作用**: 调 `Resources.UnloadAsset` + 从缓存字典移除

#### `ResourceManager.EVT_UNLOAD_ALL_RESOURCES` — 全量卸载
- **常量**: `ResourceManager.EVT_UNLOAD_ALL_RESOURCES` = `"UnloadAllResources"`
- **参数**: `[]`
- **返回**: `ResultCode.Ok()`
- **副作用**: 遍历 `Resources.UnloadAsset` + `_loadedResources.Clear()`

### 命令类（Service 内部 — 一般不直调）

> 给 façade 委托用。`EVT_GET_MODEL_CLIPS` / `EVT_GET_ALL_MODEL_PATHS` 没有 façade alias，跨模块直接 bare-string。

#### `ResourceService.EVT_DATA_LOADED` — 启动加载链入口
- **常量**: `ResourceService.EVT_DATA_LOADED` = `"OnResourceDataLoaded"`
- **参数**: `[]`
- **返回**: `ResultCode.Ok()` / `ResultCode.Ok(true)`（已加载过）
- **副作用**: 跑 PreloadConfiguredResources + IndexAllResources + 广播 `EVT_RESOURCES_LOADED`。**幂等**：内部 `_dataLoaded` 标志去重
- **触发**: `ResourceManager.Start()` 自动触发；`Demo` 也可在 `Start` 阶段主动触发以确保资源就绪

#### `ResourceService.EVT_GET_RESOURCE` — 同步取（含 fallback）
- **常量**: `ResourceService.EVT_GET_RESOURCE` = `"GetResource"`
- **参数**: `[string path, string typeStr, bool isExternal?]`
- **返回**: `ResultCode.Ok(asset)` / `ResultCode.Fail("资源未加载")`
- **副作用**: façade 6 个 sync getter 都委托到这里

#### `ResourceService.EVT_LOAD_RESOURCE_ASYNC` — 异步加载（含 sentinel）
- **常量**: `ResourceService.EVT_LOAD_RESOURCE_ASYNC` = `"LoadResourceAsync"`
- **参数**: `[string path, string typeStr, bool isExternal?]`
- **返回**: `ResultCode.Ok(cached)` / `ResultCode.Fail("加载中")` / `ResultCode.Fail("不支持的资源类型")`
- **副作用**: façade 3 个 async loader 都委托到这里

#### `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC` — 外部图片异步底层
- **常量**: `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC` = `"LoadExternalImageAsync"`
- **参数**: `[string filePath]`
- **返回**: 同 façade `EVT_LOAD_EXTERNAL_SPRITE_ASYNC`
- **副作用**: façade `EVT_LOAD_EXTERNAL_SPRITE_ASYNC` 直接转发

#### `ResourceService.EVT_ADD_RESOURCE_CONFIG` — 写预加载配置底层
- **常量**: `ResourceService.EVT_ADD_RESOURCE_CONFIG` = `"AddResourceConfig"`
- **参数**: 同 façade `EVT_ADD_PRELOAD_CONFIG`
- **返回**: `ResultCode.Ok()`
- **副作用**: 写 `ResourceConfigItem` 到 `{type}.json`

#### `ResourceService.EVT_GET_MODEL_CLIPS` — 取 FBX 容器内全部 AnimationClip
- **常量**: `ResourceService.EVT_GET_MODEL_CLIPS` = `"GetModelClips"`
- **参数**: `[string modelPath]`（Resources 相对路径或裸文件名）
- **返回**: `ResultCode.Ok(List<AnimationClip>)`
- **副作用**: 1) 优先查 `_modelClipNames`（Editor 启动 + Build manifest 双路填充）→ 反取全局 AnimationClip 缓存；2) Editor fallback 走 `AssetDatabase.LoadAllAssetsAtPath`
- **跨模块调用**: bare-string `"GetModelClips"`（无 façade alias）

#### `ResourceService.EVT_GET_ALL_MODEL_PATHS` — 枚举所有已索引的 FBX 路径
- **常量**: `ResourceService.EVT_GET_ALL_MODEL_PATHS` = `"GetAllModelPaths"`
- **参数**: `[]`
- **返回**: `ResultCode.Ok(List<string>)` —— Resources 相对路径，不含扩展名（如 `"Models/Characters3D/zombie"`）
- **副作用**: 无；用于批量扫描 FBX，**Editor / Build 都可用**

### 广播类（订阅用）

#### `ResourceService.EVT_RESOURCES_LOADED` — 资源全部加载/索引完成
- **常量**: `ResourceService.EVT_RESOURCES_LOADED` = `"OnResourcesLoaded"`
- **触发时机**: `OnDataLoaded` 末尾；启动期一次性
- **data**: `[]`
- **订阅示例**:
  ```csharp
  [EventListener("OnResourcesLoaded")]
  public List<object> OnResourcesReady(List<object> data) { /* 注册 FBX 等 */ return null; }
  ```

#### `ResourceService.EVT_EXTERNAL_IMAGE_LOADED` — 外部图片加载成功
- **常量**: `ResourceService.EVT_EXTERNAL_IMAGE_LOADED` = `"OnExternalImageLoaded"`
- **触发时机**: 外部图片 `Texture2D.LoadImage` 成功，主线程组装 Sprite 后
- **data**: `[Dictionary{ "path"→string, "sprite"→Sprite }]`

#### `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED` — 外部图片加载失败
- **常量**: `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED` = `"OnExternalImageLoadFailed"`
- **触发时机**: `Texture2D.LoadImage` 返回 false（数据非法图片）
- **data**: `[Dictionary{ "path"→string, "error"→string }]`

## 缓存键 `ResourceKey`

3 字段组合：

| 字段 | 含义 | 归一化 |
|---|---|---|
| `FileName` | 文件名（不带扩展名） | `Path.GetFileNameWithoutExtension` |
| `IsExternal` | 是否外部文件 | — |
| `TypeTag` | 资源类型标签 | `NormalizeTypeTag`：`Prefab↔GameObject`、`Texture↔Texture2D`，其余原样 |

`TypeTag` 进 key 是为了避免同名不同类型碰撞。`ToString()` 返回 `unity:Sprite:GrasslandsGround` 等串用于 Inspector。

## 持久化结构

预加载配置按 `ResourceType` 分类：

```
ResourceService/
├── Prefab.json
├── Sprite.json
├── AudioClip.json
├── Texture.json
└── RuleTile.json
```

## 路径规范

- **Unity 资源**：相对 `Resources/`，不带扩展名。例：`"Sprites/UI/Button"` ↔ `Resources/Sprites/UI/Button.png`
- **外部文件**：完整绝对路径；调用时传 `isExternal: true`

## 错误处理模板

```csharp
var r = EventProcessor.Instance.TriggerEventMethod(
    "GetSprite", new List<object> { path });
if (!ResultCode.IsOk(r) || r.Count < 2)
{
    LogWarning($"加载失败: {r?[0]}");
    return;
}
var sprite = r[1] as Sprite;
```

## 注意事项

- 跨模块**只走 bare-string**（§4.1）；不要为读 `EVT_*` 常量而 `using` 本模块（Anti-Patterns §A2）
- 卸载事件 façade 与 Service 同字符串：扫描期 Service 实现覆盖 façade，结果一致
- FBX manifest（`Resources/CharacterFBXManifest.json`）由菜单 `Tools/Character/Rebuild FBX Manifest` 或 Build 预处理 `FBXManifestBuilder` 生成
- Editor 路径按文件名索引、Build 路径按 `m_Name`：Build 前若做过文件改名，记得在 Project 窗口右键 Reimport 或重生成 FBX manifest
