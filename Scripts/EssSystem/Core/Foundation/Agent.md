# Foundation 妯″潡鎬讳綋鎸囧崡
> **Foundation 妯″潡鎻愪緵妗嗘灦鐨勫熀纭€璁炬柦鏈嶅姟**锛屽寘鎷瀯寤洪澶勭悊銆佹暟鎹寔涔呭寲銆佽祫婧愬姞杞姐€佺綉缁滈€氳绛夋牳蹇冨姛鑳姐€?>
> 鎵€鏈変笟鍔℃ā鍧楅兘渚濊禆 Foundation 妯″潡鐨勬湇鍔°€?## 馃搵 妯″潡缁撴瀯
```
Core/Foundation/
鈹溾攢鈹€ BuildSystem/              鈫?鏋勫缓鑿滃崟銆佹瀯寤哄墠棰勫鐞嗐€佽祫婧愭竻鍗曠敓鎴?鈹?  鈹溾攢鈹€ OneClickBuildHelper.cs - Editor 鏋勫缓杈呭姪
鈹?  鈹溾攢鈹€ Editor/               - 鑿滃崟涓?BuildPipeline 棰勫鐞嗗櫒
鈹?  鈹斺攢鈹€ Agent.md              - 璇︾粏鏂囨。
鈹?鈹溾攢鈹€ DataManager/              鈫?鏁版嵁鎸佷箙鍖栧拰 Service 鑷姩娉ㄥ唽
鈹?  鈹溾攢鈹€ DataManager.cs        - Manager锛圼Manager(-20)]锛屾渶鏃╁惎鍔級
鈹?  鈹溾攢鈹€ DataService.cs        - Service锛圫ervice 鑷姩娉ㄥ唽 + 缁熶竴鎸佷箙鍖栵級
鈹?  鈹斺攢鈹€ Agent.md              - 璇︾粏鏂囨。
鈹?鈹溾攢鈹€ ResourceManager/          鈫?璧勬簮鍔犺浇銆佺紦瀛樸€侀鍔犺浇
鈹?  鈹溾攢鈹€ ResourceManager.cs    - Manager锛圼Manager(0)]锛孎a莽ade锛?鈹?  鈹溾攢鈹€ ResourceService.cs    - Service锛堢紦瀛?+ 绱㈠紩 + FBX manifest锛?鈹?  鈹溾攢鈹€ ResourceRefCounter.cs - 寮曠敤璁℃暟绠＄悊
鈹?  鈹溾攢鈹€ Editor/               - 缂栬緫鍣ㄥ伐鍏?鈹?  鈹斺攢鈹€ Agent.md              - 璇︾粏鏂囨。
鈹?鈹斺攢鈹€ NetworkManager/           鈫?澶氫汉鑱旀満缃戠粶閫氳锛堝熀浜?Mirror锛?    鈹溾攢鈹€ NetworkManager.cs     - Manager锛圼Manager(2)]锛?    鈹溾攢鈹€ NetworkService.cs     - Service锛堢綉缁滅姸鎬?+ 娑堟伅缂栬В鐮侊級
    鈹溾攢鈹€ Editor/               - Mirror 鑷姩瀹夎鍣?    鈹溾攢鈹€ Runtime/              - Mirror 妗ユ帴浠ｇ爜
    鈹溾攢鈹€ Agent.md              - 璇︾粏鏂囨。
    鈹斺攢鈹€ README.md             - 蹇€熷紑濮?```
---
## 馃彈锔?鏍稿績鍔熻兘
### 1. BuildSystem 鈥?鏋勫缓杈呭姪鍜岄澶勭悊

**璁捐鐞嗗康**锛?- 灏嗘鏋剁骇鏋勫缓鍑嗗宸ヤ綔闆嗕腑鍒?Foundation
- 鏋勫缓鍓嶈嚜鍔ㄧ敓鎴愯祫婧愭竻鍗曪紝鍑忓皯涓氬姟椤圭洰閲嶅閰嶇疆
- Editor-only锛屼笉杩涘叆杩愯鏃堕€昏緫

**宸ヤ綔娴佺▼**锛?```
BuildPipeline.BuildPlayer
   鈫?EssSystemBuildPreprocessor.OnPreprocessBuild
   鈫?ResourceManifestGenerator.Generate()
   鈫?缁х画 Unity 鏋勫缓
```

**浣跨敤鍦烘櫙**锛?- 鐢熸垚 `ResourceManifest.json`
- 蹇€熸瀯寤洪」鐩?- 鎵╁睍妗嗘灦绾ф瀯寤哄墠澶勭悊姝ラ

---

### 2. DataManager 鈥?鏁版嵁鎸佷箙鍖栧拰 Service 鑷姩娉ㄥ唽
**璁捐鐞嗗康**锛?- 闆嗕腑绠＄悊鎵€鏈?Service 鐨勬寔涔呭寲
- 鑷姩娉ㄥ唽鏂?Service锛堥€氳繃浜嬩欢鐩戝惉锛?- 搴旂敤閫€鍑烘椂缁熶竴淇濆瓨
**浼樺厛绾?*锛歚[Manager(-20)]`锛堟渶鏃╁惎鍔級
**宸ヤ綔娴佺▼**锛?```
1. Service<T>.Initialize()
   鈫?2. 瑙﹀彂 EVT_INITIALIZED = "OnServiceInitialized" 浜嬩欢
   鈫?3. DataService 鐩戝惉鍣?鈫?灏?Service 鍔犲叆 _serviceInstances
   鈫?4. Application.quitting
   鈫?5. 閬嶅巻 _serviceInstances锛岄€愪釜璋?SaveAllCategories()
```
**鍏抽敭鐗规€?*锛?- 鉁?鑷姩娉ㄥ唽锛堟棤闇€鎵嬪姩娣诲姞锛?- 鉁?缁熶竴鎸佷箙鍖栵紙涓€娆℃€т繚瀛樻墍鏈?Service锛?- 鉁?闆跺弽灏勶紙閫氳繃 IServicePersistence 鎺ュ彛锛?- 鉁?澧為噺淇濆瓨锛堟瘡涓?Service 鐙珛鏂囦欢锛?**浼樺寲**锛圥hase 1.1 - DataManager 浼樺寲锛夛細
- 鉁?Service 鍘婚噸妫€鏌ヤ紭鍖栵紙O(n) 鈫?O(1)锛?- 鉁?寤惰繜鍒濆鍖栵紙鍚姩鏃舵棤纾佺洏 I/O锛?5~10ms锛?- 鉁?鎵归噺淇濆瓨缁熻锛堟€ц兘鐩戞帶锛?**鎬ц兘鎸囨爣**锛?- 鍚姩鏃堕棿锛?5~10ms
- Service 娉ㄥ唽鏃堕棿锛歄(n) 鈫?O(1)
- 鍐呭瓨鍗犵敤锛氭棤澧炲姞
**鏁版嵁缁撴瀯**锛?```
{persistentDataPath}/ServiceData/
鈹溾攢鈹€ DataService/Settings.json
鈹溾攢鈹€ UIService/UIComponents.json
鈹溾攢鈹€ InventoryService/Items.json, Configs.json, 鈥?鈹斺攢鈹€ ResourceService/Prefab.json, Sprite.json, 鈥?```
**浣跨敤鍦烘櫙**锛?- 娓告垙瀛樻。
- 鐢ㄦ埛璁剧疆
- 搴旂敤閰嶇疆
**绀轰緥**锛?```csharp
// Service 鑷姩娉ㄥ唽锛堟棤闇€鎵嬪姩鎿嶄綔锛?public class PlayerService : Service<PlayerService>
{
    public const string CAT_DATA = "PlayerData";
    protected override void Initialize()
    {
        base.Initialize();  // 鑷姩瑙﹀彂 EVT_INITIALIZED
        // 鍒濆鍖栭€昏緫
    }
}
// 鏁版嵁鑷姩淇濆瓨锛堝簲鐢ㄩ€€鍑烘椂锛?PlayerService.Instance.SetData(CAT_DATA, "Name", "Player1");
```
---
### 3. ResourceManager 鈥?璧勬簮鍔犺浇銆佺紦瀛樸€侀鍔犺浇
**璁捐鐞嗗康**锛?- 缁熶竴鐨勮祫婧愬姞杞藉叆鍙?- 鑷姩缂撳瓨鍜岀储寮?- 鏀寔 Editor 鍜?Build 鍙岃矾寰?- FBX 妯″瀷鍜屽姩鐢荤壒娈婂鐞?**浼樺厛绾?*锛歚[Manager(0)]`
**鏀寔鐨勮祫婧愮被鍨?*锛?```csharp
public enum ResourceType 
{ 
    Prefab,         // GameObject 棰勫埗浣?    Sprite,         // 2D 绮剧伒
    AudioClip,      // 闊抽
    Texture,        // 绾圭悊
    RuleTile,       // 瑙勫垯鐡风爾
    AnimationClip   // 鍔ㄧ敾鐗囨
}
```
**宸ヤ綔娴佺▼**锛?```
1. ResourceManager.Start()
   鈫?2. ResourceService.OnDataLoaded
   鈹溾攢 PreloadConfiguredResources()  鎸夐厤缃紓姝ラ鍔犺浇
   鈹溾攢 IndexAllResources()           鍏ㄩ噺绱㈠紩 Resources/
   鈹? 鈹溾攢 [Editor] AssetDatabase 绱㈠紩
   鈹? 鈹溾攢 [Editor] FBX 鍐?clip 鍏ョ紦瀛?   鈹? 鈹溾攢 LoadFBXManifestIfPresent
   鈹? 鈹斺攢 Resources.LoadAll 鍏滃簳
   鈹斺攢 骞挎挱 EVT_RESOURCES_LOADED
   鈫?3. 涓氬姟妯″潡绛夊緟璧勬簮灏辩华
```
**鍏抽敭 API**锛?```csharp
// 鍚屾鍔犺浇
var prefab = EventProcessor.Instance.TriggerEventMethod(
    "GetPrefab", new List<object> { "Prefabs/Player" });
var sprite = EventProcessor.Instance.TriggerEventMethod(
    "GetSprite", new List<object> { "Sprites/UI/Button" });
var audioClip = EventProcessor.Instance.TriggerEventMethod(
    "GetAudioClip", new List<object> { "Audio/BGM/MainTheme" });
// 寮傛鍔犺浇
EventProcessor.Instance.TriggerEventMethod(
    "LoadPrefabAsync", new List<object> { "Prefabs/Enemy", callback });
// 鍗歌浇璧勬簮
EventProcessor.Instance.TriggerEventMethod(
    "UnloadAsset", new List<object> { "Prefabs/Player" });
```
**鐗规畩澶勭悊**锛?- **FBX 妯″瀷**锛歚Resources/` 涓?`.fbx` 鏍硅祫浜ф槸 GameObject
- **FBX 鍔ㄧ敾**锛氬唴閮?AnimationClip 瀛愯祫浜ф寜 `clip.name` 绱㈠紩鍒板叏灞€缂撳瓨
- **瀛愬浘**锛歋prite 鏂囦欢涓殑瀛愬浘鎸?`sprite.name` 鏌ユ壘
**浼樺寲**锛圥hase 1.1锛夛細
- 璧勬簮寮曠敤璁℃暟绠＄悊
- 鑷姩娓呯悊瓒呮椂鏈娇鐢ㄨ祫婧愶紙300 绉掞級
- 姣?60 绉掕嚜鍔ㄦ鏌ヤ竴娆?**鎬ц兘**锛?- 缂撳瓨鍛戒腑锛歄(1)
- 棣栨鍔犺浇锛氳嚜鍔?fallback 鍒板€欓€夊瓙鐩綍
- 棰勬湡鏁堟灉锛?2~5MB锛堝紩鐢ㄨ鏁颁紭鍖栵級
**浣跨敤鍦烘櫙**锛?- 娓告垙璧勬簮鍔犺浇
- UI 璧勬簮绠＄悊
- 闊抽鍔犺浇
- 棰勫姞杞戒紭鍖?---
### 4. NetworkManager 鈥?澶氫汉鑱旀満缃戠粶閫氳
**璁捐鐞嗗康**锛?- 鍩轰簬 Mirror 妗嗘灦
- 浜嬩欢椹卞姩锛堟棤闇€鐩存帴寮曠敤 Mirror 绫诲瀷锛?- 鑷姩瀹夎 Mirror锛圤penUPM锛?- 鏀寔 Host / Server / Client 妯″紡
**浼樺厛绾?*锛歚[Manager(2)]`
**鏋舵瀯**锛?```
涓氬姟渚?TriggerEvent(EVT_HOST_START / EVT_CLIENT_CONNECT / EVT_SEND_*)
   鈫?NetworkManager [Event] handler
   鈫?WulaNetworkManagerBehaviour (Mirror.NetworkManager 瀛愮被)
   鈫?KCP / WebSocket Transport 鈫?杩滅
   鈫?Mirror 鍥炶皟 鈫?NetworkService.NotifyXxx
   鈫?EventProcessor.TriggerEvent(EVT_NET_STATUS_CHANGED / EVT_NET_MESSAGE ...)
   鈫?涓氬姟璁㈤槄鏂?```
**鑷姩瀹夎 Mirror**锛?- 绗竴娆℃寕杞?NetworkManager 鏃惰嚜鍔ㄨЕ鍙?- 妫€娴?Packages/manifest.json
- 閫氳繃 OpenUPM 瀹夎 Mirror
- 璁剧疆 MIRROR_INSTALLED 缂栬瘧瀹?**鑿滃崟**锛歚Tools/WulaSystem/Foundation/Network/Mirror/`
- `Install Mirror Now` 鈥?鎵嬪姩瑙﹀彂
- `Uninstall Mirror`
- `Toggle Auto-Install` 鈥?鍏抽棴鑷姩瀹夎鎻愮ず
- `Check Mirror Status` 鈥?妫€鏌ュ畨瑁呯姸鎬?**閰嶇疆**锛圛nspector锛夛細
- `_autoStart` 鈥?Initialize 鍚庤嚜鍔ㄥ惎鍔?- `_autoMode` 鈥?Host / ServerOnly / Client
- `_port` 鈥?鐩戝惉/杩炴帴绔彛锛堥粯璁?7777锛?- `_serverAddress` 鈥?鏈嶅姟鍣ㄥ湴鍧€锛堥粯璁?localhost锛?- `_mirrorHostObjectName` 鈥?妗ユ帴瀛愮墿浣撳悕
**鍛戒护浜嬩欢**锛?```csharp
// 鍚姩涓绘満锛圫erver + 鏈湴 Client锛?EventProcessor.Instance.TriggerEventMethod(
    "NetHostStart", new List<object> { 7777 });
// 鍚姩绾湇鍔″櫒
EventProcessor.Instance.TriggerEventMethod(
    "NetServerStart", new List<object> { 7777 });
// 杩炴帴鍒版湇鍔″櫒
EventProcessor.Instance.TriggerEventMethod(
    "NetClientConnect", new List<object> { "192.168.1.1", 7777 });
// 鏂紑杩炴帴
EventProcessor.Instance.TriggerEventMethod(
    "NetDisconnect", new List<object> { });
// 鍙戦€佹秷鎭埌鏈嶅姟鍣?EventProcessor.Instance.TriggerEventMethod(
    "NetSendToServer", new List<object> { "PlayerMove", playerData });
// 鏈嶅姟鍣ㄥ箍鎾埌鎵€鏈夊鎴风
EventProcessor.Instance.TriggerEventMethod(
    "NetSendToAll", new List<object> { "GameStateUpdate", gameState });
// 瀵圭瓑骞挎挱锛堟墍鏈夎妭鐐归兘鏀跺埌锛?EventProcessor.Instance.TriggerEventMethod(
    "NetBroadcast", new List<object> { "ChatMessage", message });
```
**骞挎挱浜嬩欢**锛?- `EVT_NET_STATUS_CHANGED` 鈥?缃戠粶鐘舵€佸彉鍖?- `EVT_NET_MESSAGE` 鈥?鎺ユ敹缃戠粶娑堟伅
- `EVT_NET_ERROR` 鈥?缃戠粶閿欒
**娑堟伅缂栬В鐮?*锛?- 鑷姩 JSON 缂栫爜
- 鏀寔绫诲瀷锛歴tring / long / double / bool / List / Dictionary
**浣跨敤鍦烘櫙**锛?- 澶氫汉娓告垙
- 瀹炴椂鍗忎綔
- 缃戠粶鍚屾
- 鑱婂ぉ绯荤粺
---
## 馃攧 鍚姩椤哄簭
Foundation 妯″潡鐨勫惎鍔ㄩ『搴忥紙鐢?Manager 浼樺厛绾ф帶鍒讹級锛?```
EventProcessor(-30)
   鈫?DataManager(-20)
   鈫?ResourceManager(0)
   鈫?NetworkManager(2)
   鈫?鍏朵粬 Manager(10+)
```
**鍏抽敭鐐?*锛?1. DataManager 鏈€鏃╁惎鍔紝鍑嗗濂?Service 娉ㄥ唽鏈哄埗
2. ResourceManager 鍚姩鍚庡姞杞借祫婧?3. NetworkManager 鍚姩鍚庡噯澶囩綉缁滆繛鎺?4. 涓氬姟 Manager 鏈€鍚庡惎鍔紝姝ゆ椂鎵€鏈夊熀纭€璁炬柦灏辩华
---
## 鈿狅笍 娉ㄦ剰浜嬮」
### DataManager
- 鉁?Service 蹇呴』 `public`锛堝弽灏勫垱寤猴級
- 鉁?DAO 绫诲繀椤?`[Serializable]`
- 鉂?绂佹瀛樺偍 GameObject / MonoBehaviour / Transform
- 鈿狅笍 DataService 涓嶇洃鍚嚜宸辩殑 EVT_INITIALIZED锛堥伩鍏嶆棤闄愰€掑綊锛?### ResourceManager
- 鉁?璧勬簮璺緞鐩稿 `Resources/`锛屼笉甯︽墿灞曞悕
- 鉁?鏀寔鑷姩 fallback 鍒板€欓€夊瓙鐩綍
- 鈿狅笍 FBX 鍔ㄧ敾闇€瑕佸湪 `Resources/CharacterFBXManifest.json` 涓厤缃?- 鈿狅笍 寮曠敤璁℃暟锛?00 绉掓棤浣跨敤鑷姩鍗歌浇
### NetworkManager
- 鉁?Mirror 鏈畨瑁呮椂鍛戒护杩斿洖 Fail锛堜笉浼氱紪璇戦敊璇級
- 鉁?浜嬩欢椹卞姩锛堟棤闇€鐩存帴寮曠敤 Mirror锛?- 鈿狅笍 娑堟伅 payload 蹇呴』鍙?JSON 搴忓垪鍖?- 鈿狅笍 澶氫汉娓告垙闇€瑕佸鐞嗙綉缁滃欢杩熷拰鍚屾闂
---
## 馃搳 鎬ц兘鎸囨爣
| 妯″潡 | 浼樺寲椤?| 棰勬湡鏁堟灉 |
|---|---|---|
| DataManager | 缁熶竴鎸佷箙鍖?| 鏃犻澶栧紑閿€ |
| ResourceManager | 寮曠敤璁℃暟 | -2~5MB |
| NetworkManager | 浜嬩欢椹卞姩 | 鏃犻澶栧紑閿€ |
---
## 馃搶 鎬荤粨
**Foundation 妯″潡鎻愪緵妗嗘灦鐨勫熀纭€璁炬柦鏈嶅姟**锛?- 鉁?BuildSystem 鈥?鏋勫缓杈呭姪鍜屾瀯寤哄墠棰勫鐞?- 鉁?DataManager 鈥?鏁版嵁鎸佷箙鍖栧拰 Service 鑷姩娉ㄥ唽
- 鉁?ResourceManager 鈥?璧勬簮鍔犺浇銆佺紦瀛樸€侀鍔犺浇
- 鉁?NetworkManager 鈥?澶氫汉鑱旀満缃戠粶閫氳
**鎺ㄨ崘浣跨敤**锛?1. 鏋勫缓鍓嶆鏋跺噯澶囧伐浣滄斁鍒?BuildSystem
2. 鎵€鏈?Service 鑷姩娉ㄥ唽鍒?DataManager
3. 鎵€鏈夎祫婧愬姞杞介€氳繃 ResourceManager
4. 缃戠粶閫氳閫氳繃 NetworkManager 浜嬩欢椹卞姩
**鍚姩椤哄簭**锛?1. EventProcessor(-30)
2. DataManager(-20)
3. ResourceManager(0)
4. NetworkManager(2)
5. 涓氬姟 Manager(10+)
---
**Foundation 妯″潡宸插垎绫诲畬鎴愶紒**


