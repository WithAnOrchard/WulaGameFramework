# 全局 Event 索引

> **必读**：所有框架 Event 的完整定义。跨模块调用时查本表确认 Event 名称和参数。

## 索引说明

| 列 | 含义 |
|---|---|
| **常量** | 定义方的常量名（如 `UIManager.EVT_REGISTER_ENTITY`） |
| **字符串值** | 消费方使用的 bare-string（如 `"RegisterUIEntity"`） |
| **定义模块** | 该 Event 所属的 Manager / Service |
| **用途** | 简要说明和参数签名 |

---

## 核心框架 Event

### Service 初始化

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `Service<T>.EVT_INITIALIZED` | `OnServiceInitialized` | Core/Base | Service 初始化完成（DataService 监听用于自动注册） |

---

## UIManager Event（5 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `UIManager.EVT_REGISTER_ENTITY` | `RegisterUIEntity` | Core/Presentation/UIManager | 注册 UI 实体（命令），参数 `[string daoId, UIComponent component]` → `Ok(UIEntity)` |
| `UIManager.EVT_GET_ENTITY` | `GetUIEntity` | Core/Presentation/UIManager | 获取已注册的 UI 实体（查询），参数 `[string daoId]` → `Ok(UIEntity)` |
| `UIManager.EVT_UNREGISTER_ENTITY` | `UnregisterUIEntity` | Core/Presentation/UIManager | 注销并销毁 UI 实体（命令），参数 `[string daoId]` |
| `UIManager.EVT_HOT_RELOAD` | `HotReloadUIConfigs` | Core/Presentation/UIManager | 热重载 UI 配置（命令） |
| `UIManager.EVT_GET_CANVAS_TRANSFORM` | `GetUICanvasTransform` | Core/Presentation/UIManager | 获取 Canvas 根 Transform（查询），返回 `Ok(Transform)` |
| `UIManager.EVT_GET_UI_GAMEOBJECT` | `GetUIGameObject` | Core/Presentation/UIManager | 按 daoId 查 UI GameObject（查询），参数 `[string daoId]` → `Ok(GameObject)` |
| `UIManager.EVT_DAO_PROPERTY_CHANGED` | `UIDaoPropertyChanged` | Core/Presentation/UIManager | UIComponent 属性变更广播，参数 `[string daoId, string propName, object value]` |
| `UIManager.EVT_ADD_WINDOW_BEHAVIOR` | `AddUIWindowBehavior` | Core/Presentation/UIManager | 向已注册面板附加窗口行为（命令），参数 `[string daoId]` → `Ok(UIWindowBehavior)` |

---

## ResourceManager Event（30+ 个）

### 异步资源加载

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `ResourceManager.EVT_LOAD_PREFAB_ASYNC` | `LoadPrefabAsync` | Core/Foundation/ResourceManager | 异步加载 Prefab，参数 `[string path]` → `Ok(GameObject)` |
| `ResourceManager.EVT_LOAD_SPRITE_ASYNC` | `LoadSpriteAsync` | Core/Foundation/ResourceManager | 异步加载 Sprite，参数 `[string path]` → `Ok(Sprite)` |
| `ResourceManager.EVT_LOAD_RULE_TILE_ASYNC` | `LoadRuleTileAsync` | Core/Foundation/ResourceManager | 异步加载 RuleTile，参数 `[string path]` → `Ok(RuleTile)` |
| `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC` | `LoadExternalSpriteAsync` | Core/Foundation/ResourceManager | 异步加载外部图片，参数 `[string path]` → `Ok(Sprite)` |

### 预加载与卸载

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `ResourceManager.EVT_ADD_PRELOAD_CONFIG` | `AddPreloadConfig` | Core/Foundation/ResourceManager | 添加预加载项（持久化），参数 `[string path, string category?]` |
| `ResourceManager.EVT_UNLOAD_RESOURCE` | `UnloadResource` | Core/Foundation/ResourceManager | 卸载单个资源，参数 `[string path]` |
| `ResourceManager.EVT_UNLOAD_ALL_RESOURCES` | `UnloadAllResources` | Core/Foundation/ResourceManager | 全量卸载，参数 `[]` |

### SpriteService 专用 Event

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `SpriteService.EVT_GET_SPRITE_ASYNC` | `GetSpriteAsync` | Core/Foundation/ResourceManager | 异步获取 Sprite，参数 `[string path]` → `Ok(Sprite)` / `Fail("加载中")` |
| `SpriteService.EVT_LOAD_SPRITE_ASYNC` | `LoadSpriteAsync` | Core/Foundation/ResourceManager | 异步加载 Sprite，参数 `[string path]` → `Ok(Sprite)` / `Fail("加载中")` |
| `SpriteService.EVT_REGISTER_SPRITE_TO_CACHE` | `RegisterSpriteToCache` | Core/Foundation/ResourceManager | 注册 Sprite 到缓存，参数 `[string spriteId, Sprite sprite]` → `Ok()` / `Fail(msg)` |

### ResourceService 内部 Event

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `ResourceService.EVT_DATA_LOADED` | `OnResourceDataLoaded` | Core/Foundation/ResourceManager | 启动后跳预加载（内部） |
| `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC` | `LoadExternalImageAsync` | Core/Foundation/ResourceManager | 外部图片加载底层（内部） |
| `ResourceService.EVT_ADD_RESOURCE_CONFIG` | `AddResourceConfig` | Core/Foundation/ResourceManager | 写预加载配置（内部） |
| `ResourceService.EVT_EXTERNAL_IMAGE_LOADED` | `OnExternalImageLoaded` | Core/Foundation/ResourceManager | 外部图片加载成功**广播** |
| `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED` | `OnExternalImageLoadFailed` | Core/Foundation/ResourceManager | 外部图片加载失败**广播** |
| `ResourceService.EVT_GET_ALL_MODEL_PATHS` | `GetAllModelPaths` | Core/Foundation/ResourceManager | 枚举已索引的所有 FBX/Model 路径 |
| `ResourceService.EVT_RESOURCES_LOADED` | `OnResourcesLoaded` | Core/Foundation/ResourceManager | 资源全部预加载/索引完成后**广播** |
| `ResourceService.EVT_REGISTER_SPRITE_SHEET` | `RegisterSpriteSheet` | Core/Foundation/ResourceManager | 批量注册多精灵图集子图入缓存，参数 `[string sheetResourcePath]` → `Ok(addedCount)` |
| `ResourceService.EVT_GET_REFCOUNT_STATS` | `GetRefCountStats` | Core/Foundation/ResourceManager | 获取资源引用计数统计，参数 `[]` → `Ok(Dictionary)` |
| `ResourceService.EVT_CLEANUP_UNUSED_ASSETS` | `CleanupUnusedAssets` | Core/Foundation/ResourceManager | 清理超时未使用的资源，参数 `[]` → `Ok()` |

---

## CharacterManager Event（12 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `CharacterManager.EVT_CREATE_CHARACTER` | `CreateCharacter` | Core/Presentation/CharacterManager | 创建 Character，参数 `[string configId, string instanceId, Transform? parent, Vector3? worldPosition]` → `Ok(Transform root)` |
| `CharacterManager.EVT_DESTROY_CHARACTER` | `DestroyCharacter` | Core/Presentation/CharacterManager | 销毁 Character，参数 `[string instanceId]` |
| `CharacterManager.EVT_PLAY_ACTION` | `PlayCharacterAction` | Core/Presentation/CharacterManager | 播放动作，参数 `[string instanceId, string actionName, string? partId]` |
| `CharacterManager.EVT_STOP_ACTION` | `StopCharacterAction` | Core/Presentation/CharacterManager | 停止动作，参数 `[string instanceId, string? partId]` |
| `CharacterManager.EVT_SET_CHARACTER_SCALE` | `SetCharacterScale` | Core/Presentation/CharacterManager | 设置根节点 localScale，参数 `[string instanceId, Vector3 scale]` |
| `CharacterManager.EVT_SET_CHARACTER_POSITION` | `SetCharacterPosition` | Core/Presentation/CharacterManager | 设置世界坐标，参数 `[string instanceId, Vector3 position]` |
| `CharacterManager.EVT_MOVE_CHARACTER` | `MoveCharacter` | Core/Presentation/CharacterManager | 在当前位置上平移，参数 `[string instanceId, Vector3 delta]` |
| `CharacterManager.EVT_PLAY_LOCOMOTION` | `PlayCharacterLocomotion` | Core/Presentation/CharacterManager | 分发运动状态（idle/walk/airborne），参数 `[string instanceId, bool moving, bool? grounded]` |
| `CharacterManager.EVT_TRIGGER_ATTACK` | `TriggerCharacterAttack` | Core/Presentation/CharacterManager | 触发攻击锁定动画，参数 `[string instanceId, float duration]` |
| `CharacterManager.EVT_SET_FACING` | `SetCharacterFacing` | Core/Presentation/CharacterManager | 设置面朝方向（翻转 localScale.x），参数 `[string instanceId, bool facingRight]` |
| `CharacterManager.EVT_SET_DIRECTION` | `SetCharacterDirection` | Core/Presentation/CharacterManager | 设置朝向（sheet 模式选行），参数 `[string instanceId, int direction]`（-1/0/+1） |
| `CharacterManager.EVT_GET_PART_SPRITE_ID` | `GetCharacterPartSpriteId` | Core/Presentation/CharacterManager | 查询部件当前帧的 spriteId，参数 `[string instanceId, string partId, string? actionName, int? frameIndex]` → `Ok(string spriteId)` |

### CharacterService 广播

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `CharacterService.EVT_FRAME_EVENT` | `OnCharacterFrameEvent` | Core/Presentation/CharacterManager | 角色动画某帧触发的**广播**，参数 `[GameObject owner, string eventName, string actionName, int frameIndex]` |

---

## AudioManager Event（12 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `AudioManager.EVT_PLAY_BGM` | `PlayBGM` | Core/Presentation/AudioManager | 播放背景音乐，参数 `[string path, bool fade?=true]` |
| `AudioManager.EVT_STOP_BGM` | `StopBGM` | Core/Presentation/AudioManager | 停止背景音乐，参数 `[bool fade?=true]` |
| `AudioManager.EVT_PAUSE_BGM` | `PauseBGM` | Core/Presentation/AudioManager | 暂停背景音乐，参数 `[]` |
| `AudioManager.EVT_RESUME_BGM` | `ResumeBGM` | Core/Presentation/AudioManager | 继续背景音乐，参数 `[]` |
| `AudioManager.EVT_PLAY_SFX` | `PlaySFX` | Core/Presentation/AudioManager | 播放自定义音效，参数 `[string path, float volumeScale?=1]` |
| `AudioManager.EVT_SET_MASTER_VOLUME` | `SetMasterVolume` | Core/Presentation/AudioManager | 设置主音量，参数 `[float volume]` |
| `AudioManager.EVT_SET_BGM_VOLUME` | `SetBGMVolume` | Core/Presentation/AudioManager | 设置 BGM 音量，参数 `[float volume]` |
| `AudioManager.EVT_SET_SFX_VOLUME` | `SetSFXVolume` | Core/Presentation/AudioManager | 设置 SFX 音量，参数 `[float volume]` |
| `AudioManager.EVT_PLAY_DAMAGE_SFX` | `PlayDamageSFX` | Core/Presentation/AudioManager | 播放受伤音效（便捷命令），参数 `[]` |
| `AudioManager.EVT_PLAY_ATTACK_SFX` | `PlayAttackSFX` | Core/Presentation/AudioManager | 播放攻击音效（便捷命令），参数 `[]` |
| `AudioManager.EVT_PLAY_UI_SFX` | `PlayUISFX` | Core/Presentation/AudioManager | 播放 UI 操作音效（便捷命令），参数 `[]` |
| `AudioManager.EVT_PLAY_ITEM_USE_SFX` | `PlayItemUseSFX` | Core/Presentation/AudioManager | 播放物品使用音效（便捷命令），参数 `[]` |
| `AudioManager.EVT_PLAY_POSITIONAL_LOOP_SFX` | `PlayPositionalLoopSFX` | Core/Presentation/AudioManager | 在指定 Transform 挂 3D 循环音源，参数 `[string clipPath, Transform anchor, float minDist?=1.5, float maxDist?=12, float volumeScale?=1]` → `Ok(string handleId)` |
| `AudioManager.EVT_STOP_POSITIONAL_SFX` | `StopPositionalSFX` | Core/Presentation/AudioManager | 停止并销毁由 `EVT_PLAY_POSITIONAL_LOOP_SFX` 创建的音源，参数 `[string handleId]` |

---

## CameraManager Event（11 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `CameraManager.EVT_GET_MAIN_CAMERA` | `GetMainCamera` | Core/Presentation/CameraManager | 取主相机引用（查询），返回 `Ok(Camera)` |
| `CameraManager.EVT_FOLLOW_TARGET` | `FollowCameraTarget` | Core/Presentation/CameraManager | 设置跟随目标（命令），参数 `[Transform target, Vector3? offset]` |
| `CameraManager.EVT_STOP_FOLLOW` | `StopCameraFollow` | Core/Presentation/CameraManager | 停止跟随（命令），参数 `[]` |
| `CameraManager.EVT_SHAKE` | `ShakeCamera` | Core/Presentation/CameraManager | 触发震屏（命令），参数 `[float amplitude, float duration, int? frequency]` |
| `CameraManager.EVT_SET_ZOOM` | `SetCameraZoom` | Core/Presentation/CameraManager | 设置缩放（命令），参数 `[float value, float? duration]` |
| `CameraManager.EVT_WORLD_TO_SCREEN` | `WorldToScreenPoint` | Core/Presentation/CameraManager | 世界→屏幕坐标（查询），参数 `[Vector3]` → `Ok(Vector2)` |
| `CameraManager.EVT_SCREEN_TO_WORLD` | `ScreenToWorldPoint` | Core/Presentation/CameraManager | 屏幕→世界坐标（查询），参数 `[Vector2 screenPos, float? zDistance]` → `Ok(Vector3)` |
| `CameraManager.EVT_SET_POSITION` | `SetCameraPosition` | Core/Presentation/CameraManager | 瞬间设置相机位置（命令），参数 `[Vector3 worldPos]` |
| `CameraManager.EVT_LOOK_AT` | `LookCameraAt` | Core/Presentation/CameraManager | 瞬间相机朝向某点（命令），参数 `[Vector3 worldPoint]` |

---

## InputManager Event（11 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `InputManager.EVT_BIND_ACTION` | `BindInputAction` | Core/Presentation/InputManager | 覆盖 Action 绑定（命令），参数 `[string actionName, params KeyCode[] keys]` |
| `InputManager.EVT_UNBIND_ACTION` | `UnbindInputAction` | Core/Presentation/InputManager | 解绑 Action（命令），参数 `[string actionName]` |
| `InputManager.EVT_IS_PRESSED` | `IsInputPressed` | Core/Presentation/InputManager | Action 是否按住（查询），参数 `[string actionName]` → `Ok(bool)` |
| `InputManager.EVT_IS_DOWN` | `IsInputDown` | Core/Presentation/InputManager | Action 本帧是否按下（查询），参数 `[string actionName]` → `Ok(bool)` |
| `InputManager.EVT_IS_UP` | `IsInputUp` | Core/Presentation/InputManager | Action 本帧是否抬起（查询），参数 `[string actionName]` → `Ok(bool)` |
| `InputManager.EVT_GET_AXIS` | `GetInputAxis` | Core/Presentation/InputManager | 取轴向值（查询），参数 `[string axisName]` 或 `[string negativeAction, string positiveAction]` → `Ok(float)` |
| `InputManager.EVT_GET_MOVE_AXIS` | `GetInputMoveAxis` | Core/Presentation/InputManager | 取 2D 移动向量（查询），参数 `[]` → `Ok(Vector2)` |
| `InputManager.EVT_GET_MOUSE_POS` | `GetMouseScreenPosition` | Core/Presentation/InputManager | 鼠标屏幕坐标（查询），参数 `[]` → `Ok(Vector2)` |
| `InputManager.EVT_GET_MOUSE_SCROLL` | `GetMouseScroll` | Core/Presentation/InputManager | 鼠标滚轮 delta（查询），参数 `[]` → `Ok(float)` |
| `InputManager.EVT_INPUT_DOWN` | `OnInputDown` | Core/Presentation/InputManager | Action 本帧按下**广播**，参数 `[string actionName]` |
| `InputManager.EVT_INPUT_UP` | `OnInputUp` | Core/Presentation/InputManager | Action 本帧抬起**广播**，参数 `[string actionName]` |

---

## LightManager Event（17 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `LightManager.EVT_APPLY_PRESET` | `ApplyLightPreset` | Core/Presentation/LightManager | 应用灯光预设（命令），参数 `[string presetId, float? blendDuration]` |
| `LightManager.EVT_CAPTURE_PRESET` | `CaptureLightPreset` | Core/Presentation/LightManager | 捕获当前灯光为预设（命令），参数 `[string presetId]` |
| `LightManager.EVT_SET_SUN_INTENSITY` | `SetSunIntensity` | Core/Presentation/LightManager | 设置太阳强度（命令），参数 `[float intensity]` |
| `LightManager.EVT_SET_AMBIENT_LIGHT` | `SetAmbientLight` | Core/Presentation/LightManager | 设置环境光（命令），参数 `[Color color]` |
| `LightManager.EVT_SET_FOG` | `SetFog` | Core/Presentation/LightManager | 设置雾（命令），参数 `[bool enabled, Color? color, float? density]` |
| `LightManager.EVT_SET_SUN_COLOR` | `SetSunColor` | Core/Presentation/LightManager | 设置主光颜色（命令），参数 `[Color]` |
| `LightManager.EVT_SET_SUN_ROTATION` | `SetSunRotation` | Core/Presentation/LightManager | 设置主光朝向（命令），参数 `[Vector3 euler]` |
| `LightManager.EVT_SET_SKYBOX` | `SetSkybox` | Core/Presentation/LightManager | 切换天空盒（命令），参数 `[string resourcesPath]` |
| `LightManager.EVT_SET_BLOOM` | `SetBloom` | Core/Presentation/LightManager | 设置 URP Bloom（命令），参数 `[float intensity, float? threshold]` |
| `LightManager.EVT_SET_VIGNETTE` | `SetVignette` | Core/Presentation/LightManager | 设置 URP Vignette（命令），参数 `[float intensity, Color? color]` |
| `LightManager.EVT_SET_CHROMATIC_ABERRATION` | `SetChromaticAberration` | Core/Presentation/LightManager | 设置 URP 色差（命令），参数 `[float strength]` |
| `LightManager.EVT_SET_COLOR_ADJUSTMENTS` | `SetColorAdjustments` | Core/Presentation/LightManager | 设置 URP 调色（命令），参数 `[float postExposure, float? saturation, float? contrast]` |
| `LightManager.EVT_REGISTER_PRESET` | `RegisterLightPreset` | Core/Presentation/LightManager | 注册灯光预设（命令），参数 `[LightPreset]` |
| `LightManager.EVT_REGISTER_LIGHT` | `RegisterLight` | Core/Presentation/LightManager | 注册 3D 动态光（命令），参数 `[string lightId, Light]` |
| `LightManager.EVT_SET_LIGHT_INTENSITY` | `SetLightIntensity` | Core/Presentation/LightManager | 设置 3D 光强度（命令），参数 `[string lightId, float intensity, float? duration]` |
| `LightManager.EVT_REGISTER_LIGHT_2D` | `RegisterLight2D` | Core/Presentation/LightManager | 注册 URP 2D Light2D（命令），参数 `[string lightId, Light2D]` |
| `LightManager.EVT_SET_LIGHT_2D_INTENSITY` | `SetLight2DIntensity` | Core/Presentation/LightManager | 设置 2D 光强度（命令），参数 `[string lightId, float intensity, float? duration]` |
| `LightManager.EVT_SET_LIGHT_2D_COLOR` | `SetLight2DColor` | Core/Presentation/LightManager | 设置 2D 光颜色（命令），参数 `[string lightId, Color]` |

---

## EffectsManager Event（7 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `EffectsManager.EVT_REGISTER_VFX` | `RegisterVFX` | Core/Presentation/EffectsManager | 注册特效定义（命令），参数 `[string vfxId, string prefabPath]` |
| `EffectsManager.EVT_UNREGISTER_VFX` | `UnregisterVFX` | Core/Presentation/EffectsManager | 移除 VFX 注册（命令），参数 `[string vfxId]` |
| `EffectsManager.EVT_PLAY_VFX` | `PlayVFX` | Core/Presentation/EffectsManager | 播放特效（命令），参数 `[string vfxId, Vector3 worldPos, Quaternion? rot, float? autoDestroy]` → `Ok(string instanceId)` |
| `EffectsManager.EVT_STOP_VFX` | `StopVFX` | Core/Presentation/EffectsManager | 停止特效（命令），参数 `[string instanceId]` |
| `EffectsManager.EVT_STOP_ALL_VFX` | `StopAllVFX` | Core/Presentation/EffectsManager | 停止所有 VFX（命令），参数 `[]` |
| `EffectsManager.EVT_SCREEN_FLASH` | `PlayScreenFlash` | Core/Presentation/EffectsManager | 屏幕闪光（命令），参数 `[Color color, float duration?=0.15f]` |

---

## InventoryManager Event（13 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `InventoryManager.EVT_OPEN_UI` | `OpenInventoryUI` | Core/Application/InventoryManager | 打开背包 UI（命令） |
| `InventoryManager.EVT_CLOSE_UI` | `CloseInventoryUI` | Core/Application/InventoryManager | 关闭背包 UI（命令） |
| `InventoryManager.EVT_REGISTER_ITEM` | `InventoryRegisterItem` | Core/Application/InventoryManager | 注册物品模板（命令），参数 `[InventoryItemConfig]` |
| `InventoryManager.EVT_REGISTER_PICKABLE_ITEM` | `InventoryRegisterPickableItem` | Core/Application/InventoryManager | 注册可拾取物定义（命令），参数 `[PickableItemConfig]` |
| `InventoryManager.EVT_SPAWN_PICKABLE_ITEM` | `InventorySpawnPickableItem` | Core/Application/InventoryManager | 在场景中生成可拾取物（命令），参数 `[string itemId, Vector3 position, int? amount]` |
| `InventoryManager.EVT_HOTBAR_USE` | `InventoryHotbarUse` | Core/Application/InventoryManager | 玩家按 1~9 使用快捷栏槽位**广播**，参数 `[string invId, int slotIndex, InventoryItem item]` |
| `InventoryService.EVT_CREATE` | `InventoryCreate` | Core/Application/InventoryManager | 创建容器（命令），参数 `[string invId, int slotCount]` |
| `InventoryService.EVT_DELETE` | `InventoryDelete` | Core/Application/InventoryManager | 删除容器（命令），参数 `[string invId]` |
| `InventoryService.EVT_ADD` | `InventoryAdd` | Core/Application/InventoryManager | 添加物品（命令），参数 `[string invId, string itemId, int amount]` |
| `InventoryService.EVT_REMOVE` | `InventoryRemove` | Core/Application/InventoryManager | 移除物品（命令），参数 `[string invId, string itemId, int amount]` |
| `InventoryService.EVT_MOVE` | `InventoryMove` | Core/Application/InventoryManager | 移动物品（命令），参数 `[string invId, int fromSlot, int toSlot]` |
| `InventoryService.EVT_QUERY` | `InventoryQuery` | Core/Application/InventoryManager | 查询物品（查询），参数 `[string invId, string itemId]` → `Ok(int amount)` |
| `InventoryService.EVT_CHANGED` | `InventoryChanged` | Core/Application/InventoryManager | 背包变化**广播**，参数 `[string invId, InventoryChangeType changeType, ...]` |

---

## EntityManager Event（8 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `EntityManager.EVT_CREATE_ENTITY` | `CreateEntity` | Core/Application/EntityManager | 创建 Entity，参数 `[string configId, string instanceId, Transform? parent, Vector3? worldPosition]` → `Ok(Transform CharacterRoot)` |
| `EntityManager.EVT_DESTROY_ENTITY` | `DestroyEntity` | Core/Application/EntityManager | 销毁 Entity，参数 `[string instanceId]` |
| `EntityManager.EVT_REGISTER_SCENE_ENTITY` | `RegisterSceneEntity` | Core/Application/EntityManager | 注册已有场景 GameObject 为 Entity，参数 `[string instanceId, GameObject host, EntityRuntimeDefinition definition]` |
| `EntityManager.EVT_DAMAGE_ENTITY` | `DamageEntity` | Core/Application/EntityManager | 对运行时 Entity 造成伤害，参数 `[string instanceId, float damage, string? damageType]` |
| `EntityManager.EVT_REGISTER_ENTITY_CONFIG` | `RegisterEntityConfig` | Core/Application/EntityManager | 注册 Entity 配置（模板），参数 `[EntityConfig]` → `Ok(string configId)` |
| `EntityManager.EVT_GET_ENTITY` | `GetEntity` | Core/Application/EntityManager | 查询 Entity 实例，参数 `[string instanceId]` → `Ok(Entity)` |
| `EntityManager.EVT_APPLY_COLLIDER` | `ApplyCollider` | Core/Application/EntityManager | 应用 Collider 到 GameObject，参数 `[GameObject, EntityColliderConfig]` |
| `EntityManager.EVT_ATTACH_ENTITY_HANDLE` | `AttachEntityHandle` | Core/Application/EntityManager | 挂载 EntityHandle 桥接，参数 `[GameObject, Entity]` |

---

## BuildingManager Event（7 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `BuildingManager.EVT_REGISTER_BUILDING_CONFIG` | `RegisterBuildingConfig` | Core/Application/BuildingManager | 注册建筑模板（命令），参数 `[BuildingConfig]` → `Ok(string configId)` |
| `BuildingManager.EVT_PLACE_BUILDING` | `PlaceBuilding` | Core/Application/BuildingManager | 放置建筑（命令），参数 `[string configId, string instanceId, Vector3 position, bool? startCompleted]` → `Ok(Transform)` |
| `BuildingManager.EVT_SUPPLY_BUILDING` | `SupplyBuilding` | Core/Application/BuildingManager | 送材料（命令），参数 `[string instanceId, string itemId, int amount]` → `Ok(int remaining)` |
| `BuildingManager.EVT_DESTROY_BUILDING` | `DestroyBuilding` | Core/Application/BuildingManager | 销毁建筑（命令），参数 `[string instanceId]` → `Ok(string instanceId)` |
| `BuildingService.EVT_COMPLETED` | `OnBuildingCompleted` | Core/Application/BuildingManager | 建造完成**广播**，参数 `[string instanceId, string configId]` |
| `BuildingService.EVT_DESTROYED` | `OnBuildingDestroyed` | Core/Application/BuildingManager | 建筑销毁**广播**，参数 `[string instanceId]` |
| `BuildingService.EVT_SUPPLY_PROGRESS` | `OnBuildingSupplyProgress` | Core/Application/BuildingManager | 补给进度**广播**，参数 `[string instanceId, string itemId, int remaining]` |

---

## DialogueManager Event（12 个）

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `DialogueManager.EVT_OPEN_UI` | `OpenDialogueUI` | Core/Application/DialogueManager | 打开对话 UI 并启动会话（命令），参数 `[string dialogueId, string? configId]` |
| `DialogueManager.EVT_CLOSE_UI` | `CloseDialogueUI` | Core/Application/DialogueManager | 结束对话并隐藏 UI（命令），参数 `[]` |
| `DialogueManager.EVT_SET_PORTRAIT_SPRITE` | `SetDialoguePortraitSprite` | Core/Application/DialogueManager | 直接贴 Sprite 头像层（命令），参数 `[Sprite single]` 或 `[List<Sprite> layers]` → `Ok()` / `Fail(msg)` |
| `DialogueService.EVT_REGISTER_DIALOGUE` | `RegisterDialogue` | Core/Application/DialogueManager | 注册 `Dialogue`（命令），参数 `[Dialogue]` |
| `DialogueService.EVT_REGISTER_CONFIG` | `RegisterDialogueConfig` | Core/Application/DialogueManager | 注册 `DialogueConfig`（命令），参数 `[DialogueConfig]` |
| `DialogueService.EVT_ADVANCE` | `AdvanceDialogue` | Core/Application/DialogueManager | 推进到下一行（命令），参数 `[]` |
| `DialogueService.EVT_SELECT_OPTION` | `SelectDialogueOption` | Core/Application/DialogueManager | 选择当前行第 N 个选项（命令），参数 `[int index]` |
| `DialogueService.EVT_END` | `EndDialogue` | Core/Application/DialogueManager | 强制结束当前会话（命令），参数 `[]` |
| `DialogueService.EVT_QUERY_CURRENT` | `QueryDialogueCurrent` | Core/Application/DialogueManager | 查询当前会话（查询），返回 `Ok(string dialogueId, string lineId, string configId)` / `Fail` |
| `DialogueService.EVT_STARTED` | `OnDialogueStarted` | Core/Application/DialogueManager | 对话启动**广播**，参数 `[string dialogueId, string configId]` |
| `DialogueService.EVT_LINE_CHANGED` | `OnDialogueLineChanged` | Core/Application/DialogueManager | 当前行切换**广播**，参数 `[string dialogueId, string lineId]` |
| `DialogueService.EVT_ENDED` | `OnDialogueEnded` | Core/Application/DialogueManager | 对话结束**广播**，参数 `[string dialogueId]` |

---

## 第三方 Manager Event

### BilibiliDanmuManager（B 站弹幕直播）— 6 个

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `DanmuService.EVT_CONNECTED` | `OnDanmuConnected` | Manager/DanmuManager | B 站长连接握手成功**广播**，参数 `[long roomId]` |
| `DanmuService.EVT_DISCONNECTED` | `OnDanmuDisconnected` | Manager/DanmuManager | B 站长连接断开**广播**，参数 `[Exception? errorOrNull]` |
| `DanmuService.EVT_DANMAKU` | `OnDanmuComment` | Manager/DanmuManager | 普通弹幕评论**广播**，参数 `[string userName, string commentText, long userId]` |
| `DanmuService.EVT_GIFT` | `OnDanmuGift` | Manager/DanmuManager | 礼物**广播**，参数 `[string userName, string giftName, int giftCount, long userId]` |
| `DanmuService.EVT_SC` | `OnDanmuSuperChat` | Manager/DanmuManager | 超级留言 SuperChat **广播**，参数 `[string userName, string text, int priceYuan, long userId]` |
| `DanmuService.EVT_RAW` | `OnDanmuRaw` | Manager/DanmuManager | 全类型原始 `DanmakuModel`**广播**（含 SuperChat / 上船 / 进场等高级类型） |

### LiveStatusManager（B 站直播间开播状态轮询）— 3 个

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `LiveStatusService.EVT_LIVE_STARTED` | `OnLiveStarted` | Manager/LiveStatusManager | 开播**广播**（0/2 → 1 状态边沿触发），参数 `[long roomId, string title, LiveRoomInfo info]` |
| `LiveStatusService.EVT_LIVE_ENDED` | `OnLiveEnded` | Manager/LiveStatusManager | 下播**广播**（1 → 0/2 状态边沿触发），参数 `[long roomId, string title, LiveRoomInfo info]` |
| `LiveStatusService.EVT_STATUS_POLLED` | `OnLiveStatusPolled` | Manager/LiveStatusManager | 每次轮询都触发**广播**（无论状态是否变更），参数 `[long roomId, int liveStatus, string title, LiveRoomInfo info]` |

---

## 注意事项

> ⚠️ **façade vs Service 同名**：`ResourceManager.EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` 与 `ResourceService.EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` **字符串相同**，仅后者实际生效（字典覆盖）。调用方只需用 façade 常量。

> ℹ️ **几乎无 Event 的模块**：`MapManager` 当前以纯 C# API 为主（`MapService.Instance.XXX`），目前不暴露 `EVT_*`。若将来新增跨模块 Event，必须同步更新本表并运行 `agent_lint.ps1 -Strict`。
