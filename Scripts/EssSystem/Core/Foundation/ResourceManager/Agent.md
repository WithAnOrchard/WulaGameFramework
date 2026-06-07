# ResourceManager 资源模块

## 职责
- 负责资源查询、异步加载转发、缓存注册、索引和批量扫描范围控制。
- 模块路径：`Scripts/EssSystem/Core/Foundation/ResourceManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Editor/`
- `Services/`
- `ResourceManager.cs`
- `ResourceManifestData.cs`
- `ResourceRefCounter.cs`
- `ResourceService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `ResourceManager.EVT_ADD_PRELOAD_CONFIG` = `"AddPreloadConfig"`
- `ResourceManager.EVT_GET_ANIMATION_CLIP` = `"GetAnimationClip"`
- `ResourceManager.EVT_GET_ANIMATION_CLIP_ASYNC` = `"GetAnimationClipAsync"`
- `ResourceManager.EVT_GET_AUDIO_CLIP` = `"GetAudioClip"`
- `ResourceManager.EVT_GET_AUDIO_CLIP_ASYNC` = `"GetAudioClipAsync"`
- `ResourceManager.EVT_GET_EXTERNAL_SPRITE` = `"GetExternalSprite"`
- `ResourceManager.EVT_GET_EXTERNAL_SPRITE_ASYNC` = `"GetExternalSpriteAsync"`
- `ResourceManager.EVT_GET_MATERIAL` = `"GetMaterial"`
- `ResourceManager.EVT_GET_MATERIAL_ASYNC` = `"GetMaterialAsync"`
- `ResourceManager.EVT_GET_PREFAB` = `"GetPrefab"`
- `ResourceManager.EVT_GET_PREFAB_ASYNC` = `"GetPrefabAsync"`
- `ResourceManager.EVT_GET_RULE_TILE` = `"GetRuleTile"`
- `ResourceManager.EVT_GET_RULE_TILE_ASYNC` = `"GetRuleTileAsync"`
- `ResourceManager.EVT_GET_SPRITE` = `"GetSprite"`
- `ResourceManager.EVT_GET_SPRITE_ASYNC` = `"GetSpriteAsync"`
- `ResourceManager.EVT_GET_TEXTURE` = `"GetTexture"`
- `ResourceManager.EVT_GET_TEXTURE_ASYNC` = `"GetTextureAsync"`
- `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC` = `"LoadExternalSpriteAsync"`
- `ResourceManager.EVT_LOAD_PREFAB_ASYNC` = `"LoadPrefabAsync"`
- `ResourceManager.EVT_LOAD_RULE_TILE_ASYNC` = `"LoadRuleTileAsync"`
- `ResourceManager.EVT_LOAD_SPRITE_ASYNC` = `"LoadSpriteAsync"`
- `ResourceManager.EVT_REGISTER_SPRITE_SHEET` -> `ResourceService.EVT_REGISTER_SPRITE_SHEET`
- `ResourceManager.EVT_UNLOAD_ALL_RESOURCES` = `"UnloadAllResources"`
- `ResourceManager.EVT_UNLOAD_RESOURCE` = `"UnloadResource"`
- `ResourceService.EVT_ADD_BULK_LOAD_PATH` = `"AddResourceBulkLoadPath"`
- `ResourceService.EVT_ADD_RESOURCE_CONFIG` = `"AddResourceConfig"`
- `ResourceService.EVT_CLEANUP_UNUSED_ASSETS` = `"CleanupUnusedAssets"`
- `ResourceService.EVT_DATA_LOADED` = `"OnResourceDataLoaded"`
- `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED` = `"OnExternalImageLoadFailed"`
- `ResourceService.EVT_EXTERNAL_IMAGE_LOADED` = `"OnExternalImageLoaded"`
- `ResourceService.EVT_GET_ALL_MODEL_PATHS` = `"GetAllModelPaths"`
- `ResourceService.EVT_GET_MODEL_CLIPS` = `"GetModelClips"`
- `ResourceService.EVT_GET_REFCOUNT_STATS` = `"GetRefCountStats"`
- `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC` = `"LoadExternalImageAsync"`
- `ResourceService.EVT_REGISTER_SPRITE_SHEET` = `"RegisterSpriteSheet"`
- `ResourceService.EVT_RESOURCES_LOADED` = `"OnResourcesLoaded"`
- `ResourceService.EVT_SET_BULK_LOAD_PATHS` = `"SetResourceBulkLoadPaths"`

## 维护注意
- 避免启动时全项目 Resources.LoadAll；优先使用批量扫描路径或 Demo 专属资源提供器。
- Editor 可以为 Demo 测试宽松加载资源目录，Build 必须只打包目标 Demo 所需资源。
- ResourceService 负责缓存和索引；各类型 Service 负责自己的异步加载事件。
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
