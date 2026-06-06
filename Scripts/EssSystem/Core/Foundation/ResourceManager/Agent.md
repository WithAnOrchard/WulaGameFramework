# ResourceManager 鎸囧崡
## 姒傝堪
`Foundation/ResourceManager`锛坄[Manager(0)]`锛夆€斺€?璧勬簮鍔犺浇/缂撳瓨/澶栭儴鍥剧墖/棰勫姞杞介厤缃殑缁熶竴鍏ュ彛銆?| | 绫?| 瑙掕壊 |
|---|---|---|
| Manager | `ResourceManager` | 鍚屾鏌ヨ fa莽ade + 澶栭儴 Sprite 鏌ヨ + 閰嶇疆杞彂锛涘紓姝ユ煡璇㈢敱鍚勭被鍨?Service 鐩存帴鎵挎帴浜嬩欢 |
| Service | `ResourceService` | 涓績缂撳瓨 / Inspector 绱㈠紩 / Resources 鎵归噺绱㈠紩 / 棰勫姞杞介厤缃?/ 鍗歌浇 |
> 璺ㄦā鍧楄皟鐢ㄤ竴寰?bare-string锛埪?.1锛夈€傛湰鏂囨。灞曠ず甯搁噺鏄负浜嗚浣犵湅娓呭瓧绗︿覆鍊硷紱瀹為檯璋冪敤鏃剁洿鎺ヤ紶瀛楃涓层€?## 鏂囦欢缁撴瀯
```
Foundation/ResourceManager/
鈹溾攢鈹€ ResourceManager.cs           Fa莽ade锛堝悓姝ユ煡璇?+ 澶栭儴 Sprite + 閰嶇疆杞彂锛?鈹溾攢鈹€ ResourceService.cs           涓績缂撳瓨 + 绱㈠紩 + 鍗歌浇锛涗笉鍐嶅疄鐜板閮ㄥ浘鐗囨垨妯″瀷鍔ㄧ敾鏌ヨ
鈹溾攢鈹€ Services/
鈹?  鈹溾攢鈹€ Prefab/                  Prefab 涓撶敤寮傛鏈嶅姟
鈹?  鈹溾攢鈹€ Sprite/                  Sprite 涓撶敤鏈嶅姟锛堝悓姝?寮傛鍔犺浇 + 瀛愬浘鍏滃簳 + 缂撳瓨娉ㄥ唽锛?鈹?  鈹溾攢鈹€ Audio/                   AudioClip 涓撶敤寮傛鏈嶅姟
鈹?  鈹溾攢鈹€ Texture/                 Texture 涓撶敤寮傛鏈嶅姟
鈹?  鈹溾攢鈹€ Material/                Material 涓撶敤寮傛鏈嶅姟
鈹?  鈹溾攢鈹€ RuleTile/                RuleTile 涓撶敤寮傛鏈嶅姟
鈹?  鈹溾攢鈹€ Animation/               AnimationClip / ModelAnimation 涓撶敤鏈嶅姟
鈹?  鈹溾攢鈹€ External/                澶栭儴鍥剧墖鍔犺浇鏈嶅姟
鈹?  鈹斺攢鈹€ Base/
鈹?      鈹斺攢鈹€ ResourceServiceBase.cs  鏈嶅姟鍩虹被
鈹斺攢鈹€ Agent.md                     鏈枃妗?```
## 鍚姩 / 鏁版嵁娴?```
1. ResourceManager.Start()
       鈹?TriggerEventMethod(ResourceService.EVT_DATA_LOADED)
       鈻?2. ResourceService.OnDataLoaded
       鈹溾攢 PreloadConfiguredResources()       鎸?DataService 閰嶇疆寮傛棰勫姞杞?       鈹溾攢 IndexAllResources()                鍏ㄩ噺绱㈠紩 Resources/
       鈹?    鈹溾攢 [Editor] AssetDatabase 鎸夌湡瀹炴枃浠跺悕寤虹储寮曪紙瀹瑰繊 m_Name 钀藉悗锛?       鈹?    鈹溾攢 [Editor] EditorIndexModelClipNames 椤烘墜鎶?FBX 鍐?clip 鍏ョ紦瀛?       鈹?    鈹溾攢 LoadFBXManifestIfPresent     璇?Resources/CharacterFBXManifest.json
       鈹?    鈹斺攢 Resources.LoadAll 鎸?m_Name 鍏滃簳锛圗ditor + Build 閮借窇锛?       鈹斺攢 骞挎挱 EVT_RESOURCES_LOADED
       鈻?3. 涓氬姟妯″潡閫氳繃 [EventListener("OnResourcesLoaded")] 绛夊緟璧勬簮灏辩华
```
## 鏀寔鐨勮祫婧愮被鍨?> **FBX 妯″瀷**锛歚Resources/` 涓?`.fbx` 鏍硅祫浜ф槸 `GameObject`锛岀敤 `EVT_GET_PREFAB_ASYNC` 寮傛鍔犺浇銆傚叾鍐呴儴鐨?`AnimationClip` 瀛愯祫浜у湪鍚姩鏃舵寜 `clip.name` 绱㈠紩鍒板叏灞€缂撳瓨锛岀敤 `EVT_GET_ANIMATION_CLIP_ASYNC` 寮傛鑾峰彇銆?## Event API

## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **ResourceManager Event (40+) - facade constants**.

- `ResourceManager.EVT_ADD_PRELOAD_CONFIG`
- `ResourceManager.EVT_GET_PREFAB`
- `ResourceManager.EVT_GET_SPRITE`
- `ResourceManager.EVT_GET_AUDIO_CLIP`
- `ResourceManager.EVT_GET_TEXTURE`
- `ResourceManager.EVT_GET_MATERIAL`
- `ResourceManager.EVT_GET_RULE_TILE`
- `ResourceManager.EVT_GET_ANIMATION_CLIP`
- `ResourceManager.EVT_GET_EXTERNAL_SPRITE`
- `ResourceManager.EVT_GET_ANIMATION_CLIP_ASYNC`
- `ResourceManager.EVT_GET_AUDIO_CLIP_ASYNC`
- `ResourceManager.EVT_GET_EXTERNAL_SPRITE_ASYNC`
- `ResourceManager.EVT_GET_MATERIAL_ASYNC`
- `ResourceManager.EVT_GET_PREFAB_ASYNC`
- `ResourceManager.EVT_GET_RULE_TILE_ASYNC`
- `ResourceManager.EVT_GET_SPRITE_ASYNC`
- `ResourceManager.EVT_GET_TEXTURE_ASYNC`
- `ResourceManager.EVT_LOAD_PREFAB_ASYNC`
- `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC`
- `ResourceManager.EVT_LOAD_RULE_TILE_ASYNC`
- `ResourceManager.EVT_LOAD_SPRITE_ASYNC`
- `ResourceManager.EVT_UNLOAD_ALL_RESOURCES`
- `ResourceManager.EVT_UNLOAD_RESOURCE`
- `ResourceService.EVT_ADD_RESOURCE_CONFIG`
- `ResourceService.EVT_ADD_BULK_LOAD_PATH`
- `ResourceService.EVT_CLEANUP_UNUSED_ASSETS`
- `ResourceService.EVT_DATA_LOADED`
- `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED`
- `ResourceService.EVT_EXTERNAL_IMAGE_LOADED`
- `ResourceService.EVT_GET_ALL_MODEL_PATHS`
- `ResourceService.EVT_GET_MODEL_CLIPS`
- `ResourceService.EVT_GET_REFCOUNT_STATS`
- `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC`
- `ResourceService.EVT_REGISTER_SPRITE_SHEET`
- `ResourceService.EVT_RESOURCES_LOADED`
- `ResourceService.EVT_SET_BULK_LOAD_PATHS`
- `ResourceService.EVT_UNLOAD_RESOURCE`
- `SpriteService.EVT_GET_SPRITE_ASYNC`
- `SpriteService.EVT_GET_SPRITE`
- `SpriteService.EVT_LOAD_SPRITE_ASYNC`
- `SpriteService.EVT_REGISTER_SPRITE_TO_CACHE`
- `PrefabService.EVT_GET_PREFAB_ASYNC`
- `PrefabService.EVT_LOAD_PREFAB_ASYNC`
- `AudioClipService.EVT_GET_AUDIO_CLIP_ASYNC`
- `AudioClipService.EVT_LOAD_AUDIO_CLIP_ASYNC`
- `TextureService.EVT_GET_TEXTURE_ASYNC`
- `TextureService.EVT_LOAD_TEXTURE_ASYNC`
- `MaterialService.EVT_GET_MATERIAL_ASYNC`
- `MaterialService.EVT_LOAD_MATERIAL_ASYNC`
- `RuleTileService.EVT_GET_RULE_TILE_ASYNC`
- `RuleTileService.EVT_LOAD_RULE_TILE_ASYNC`
- `AnimationClipService.EVT_GET_ANIMATION_CLIP_ASYNC`
- `AnimationClipService.EVT_LOAD_ANIMATION_CLIP_ASYNC`

## 缂撳瓨閿?`ResourceKey`
3 瀛楁缁勫悎锛?| 瀛楁 | 鍚箟 | 褰掍竴鍖?|
|---|---|---|
| `FileName` | 鏂囦欢鍚嶏紙涓嶅甫鎵╁睍鍚嶏級 | `Path.GetFileNameWithoutExtension` |
| `IsExternal` | 鏄惁澶栭儴鏂囦欢 | 鈥?|
| `TypeTag` | 璧勬簮绫诲瀷鏍囩 | `NormalizeTypeTag`锛歚Prefab鈫擥ameObject`銆乣Texture鈫擳exture2D`锛屽叾浣欏師鏍?|
`TypeTag` 杩?key 鏄负浜嗛伩鍏嶅悓鍚嶄笉鍚岀被鍨嬬鎾炪€俙ToString()` 杩斿洖 `unity:Sprite:GrasslandsGround` 绛変覆鐢ㄤ簬 Inspector銆?## 鎸佷箙鍖栫粨鏋?棰勫姞杞介厤缃寜 `ResourceType` 鍒嗙被锛?```
ResourceService/
鈹溾攢鈹€ Prefab.json
鈹溾攢鈹€ Sprite.json
鈹溾攢鈹€ AudioClip.json
鈹溾攢鈹€ Texture.json
鈹斺攢鈹€ RuleTile.json
```
## 璺緞瑙勮寖
- **Unity 璧勬簮**锛氱浉瀵?`Resources/`锛屼笉甯︽墿灞曞悕銆備緥锛歚"Sprites/UI/Button"` 鈫?`Resources/Sprites/UI/Button.png`
- **澶栭儴鏂囦欢**锛氬畬鏁寸粷瀵硅矾寰勶紱璋冪敤鏃朵紶 `isExternal: true`
## 閿欒澶勭悊妯℃澘
## 鑱岃矗杈圭晫

- `ResourceManager` 璐熻矗鍚屾浜嬩欢鍏ュ彛锛屽 `GetPrefab` / `GetMaterial` / `GetAudioClip`銆?- 鍚勭被鍨?Service 璐熻矗寮傛浜嬩欢鍏ュ彛锛屽 `GetPrefabAsync` / `LoadSpriteAsync`銆?- `ExternalImageService` 璐熻矗 `LoadExternalImageAsync`锛屽姞杞界粨鏋滃啓鍏?`SpriteService`锛屽苟鍚屾鐧昏鍒?`ResourceService` 涓績缂撳瓨銆?- `ModelAnimationService` 璐熻矗 `GetModelClips` / `GetAllModelPaths`銆?- `ResourceService` 涓嶆姠鍗犱笂杩扮被鍨嬩笓灞炰簨浠讹紝閬垮厤鍚屽悕 `[Event]` 琚壂鎻忛『搴忚鐩栥€?
## SpriteService 浣跨敤鎸囧崡
### 鑱岃矗鍒嗙
- **ResourceManager / ResourceService**锛氶€氱敤璧勬簮鍔犺浇妗嗘灦
- **SpriteService**锛歋prite 涓撶敤鏈嶅姟锛屽鐞嗗紓姝ュ姞杞姐€佸瓙鍥惧厹搴曘€佺紦瀛樻敞鍐?- **涓氬姟鏂癸紙濡?CharacterManager锛?*锛氳礋璐ｉ鍔犺浇绛栫暐鍜岃矾寰勯厤缃?### 棰勫姞杞芥祦绋?1. **涓氬姟鏂规壂鎻忛厤缃?*锛氶亶鍘嗗凡娉ㄥ唽鐨?CharacterConfig锛屾敹闆嗛渶瑕佺殑 Sprite Sheet 璺緞
2. **涓氬姟鏂瑰姞杞?Sheet**锛歚Resources.LoadAll<Sprite>(sheetPath)` 鍔犺浇鏁翠釜 Sprite Sheet
3. **涓氬姟鏂规敞鍐屽瓙鍥?*锛氶€愪釜璋冪敤 `EVT_REGISTER_SPRITE_TO_CACHE` 灏嗗瓙鍥炬敞鍐屽埌缂撳瓨
4. **杩愯鏃舵煡璇?*锛欳haracterPartView 璋冪敤 `EVT_GET_SPRITE_ASYNC` 鐩存帴浠庣紦瀛樺懡涓?### 璺緞瑙勮寖
- **Sprite ID 鏍煎紡**锛歚{category}_{variant}_{action}_{frameIndex}`
  - 渚嬶細`Skin_warrior_1_Idle_0` 鈫?绫诲埆 `Skin`锛屽彉浣?`warrior_1`锛屽姩浣?`Idle`锛屽抚 `0`
- **Sprite Sheet 璺緞**锛歚{basePath}/{category}/{variant}`
  - 渚嬶細`Characters/PixArt/Skin/warrior_1` 鈫?瀵瑰簲 `Resources/Characters/PixArt/Skin/warrior_1.png`
## 娉ㄦ剰浜嬮」
- 璺ㄦā鍧?*鍙蛋 bare-string**锛埪?.1锛夛紱涓嶈涓鸿 `EVT_*` 甯搁噺鑰?`using` 鏈ā鍧楋紙Anti-Patterns 搂A2锛?- 鍗歌浇浜嬩欢 fa莽ade 涓?Service 鍚屽瓧绗︿覆锛氭壂鎻忔湡 Service 瀹炵幇瑕嗙洊 fa莽ade锛岀粨鏋滀竴鑷?- FBX manifest锛坄Resources/CharacterFBXManifest.json`锛夌敱鑿滃崟 `Tools/WulaSystem/Presentation/Character/3D/FBX/Rebuild FBX Manifest` 鎴?Build 棰勫鐞?`FBXManifestBuilder` 鐢熸垚
- Editor 璺緞鎸夋枃浠跺悕绱㈠紩銆丅uild 璺緞鎸?`m_Name`锛欱uild 鍓嶈嫢鍋氳繃鏂囦欢鏀瑰悕锛岃寰楀湪 Project 绐楀彛鍙抽敭 Reimport 鎴栭噸鐢熸垚 FBX manifest
- **SpriteService 棰勫姞杞?*锛氫笉搴斿湪 ResourceManager 涓‖缂栫爜锛岃€屽簲鐢变笟鍔℃柟锛堝 DobeCat锛夋牴鎹嚜宸辩殑璧勬簮缁撴瀯璋冪敤 `CharacterManager.PreloadCharacterSprites(basePath)` 浼犲叆姝ｇ‘鐨勮矾寰?
