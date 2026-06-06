## NetworkManager / 澶氫汉鑱旀満缃戠粶閫氳锛堝熀浜?Mirror锛?### 姒傝堪
鎶?[Mirror](https://mirror-networking.com/) 鐨?Host-Client 缃戠粶妯″瀷鎺ュ叆鍒版鏋剁殑 EventProcessor锛屼笟鍔℃柟瀹屽叏閫氳繃浜嬩欢椹卞姩锛屼笉寮曠敤浠讳綍 Mirror 绫诲瀷銆?```
涓氬姟渚?TriggerEvent(EVT_HOST_START / EVT_CLIENT_CONNECT / EVT_SEND_*)
   -> NetworkManager [Event] handler
   -> WulaNetworkManagerBehaviour (Mirror.NetworkManager subclass)
   -> KCP / WebSocket Transport <-> 杩滅
   -> Mirror 鍥炶皟 -> NetworkService.NotifyXxx
   -> EventProcessor.TriggerEvent(EVT_NET_STATUS_CHANGED / EVT_NET_MESSAGE ...)
   -> 涓氬姟璁㈤槄鏂?```
### 鑷姩瀹夎 Mirror
绗竴娆℃寕杞?NetworkManager 鏃讹紝缂栬緫鍣ㄤ細瑙﹀彂 `MirrorInstaller`锛?1. 妫€娴?`Packages/manifest.json`锛岀己鍒欐敞鍏?OpenUPM scoped registry
2. 璋冪敤 `Client.Add("com.mirror-networking.mirror")` 瀹夎鍖?3. 瀹夎瀹屾垚鍚庤缃?`MIRROR_INSTALLED` 缂栬瘧瀹忥紙Standalone / Android / iOS / WebGL锛?鑿滃崟锛歚Tools/WulaSystem/Foundation/Network/Mirror/`
- `Install Mirror Now` 鈥斺€?鎵嬪姩瑙﹀彂
- `Uninstall Mirror`
- `Toggle Auto-Install` 鈥斺€?鍏抽棴鍚庝笉鍐嶅脊绐?- `Check Mirror Status`
鏈 Mirror 鏃舵墍鏈夊懡浠や簨浠惰繑鍥?`ResultCode.Fail("Mirror 鏈畨瑁咃細...")`锛屼笉浼氱紪璇戞姤閿欍€?### 妯″潡缁撴瀯
```
NetworkManager/
  NetworkManager.cs          -- 闂ㄩ潰 [Manager(2)]锛欼nspector + [Event] 鍛戒护澶勭悊
  NetworkService.cs          -- Service<>锛氱姸鎬?+ 骞挎挱 EVT_* + Payload 缂栬В鐮?  Agent.md
  README.md
  Editor/
    MirrorInstaller.cs       -- [InitializeOnLoad] 鑷姩瀹夎 Mirror锛圤penUPM锛?  Runtime/                   -- #if MIRROR_INSTALLED 鎵嶇紪璇?    WulaNetworkManagerBehaviour.cs  -- 缁ф壙 Mirror.NetworkManager
    NetMessage.cs                   -- struct WulaNetMessage : NetworkMessage
```
鍛藉悕绌洪棿锛?- `EssSystem.Core.Foundation.NetworkManager` 鈥斺€?Manager / Service / 鏋氫妇
- `EssSystem.Core.Foundation.NetworkManager.Runtime` 鈥斺€?Mirror 妗ユ帴锛圡irror 缂哄け鏃朵负绌猴級
- `EssSystem.Core.Foundation.NetworkManager.EditorTools` 鈥斺€?缂栬緫鍣ㄥ畨瑁呭櫒
### Inspector 瀛楁
| 瀛楁 | 榛樿 | 璇存槑 |
|---|---|---|
| `_autoStart` | false | Initialize 鍚庤嚜鍔ㄦ寜 `_autoMode` 鍚姩 |
| `_autoMode` | None | `Host` / `ServerOnly` / `Client` |
| `_port` | 7777 | 鐩戝惉 / 杩炴帴绔彛 |
| `_serverAddress` | localhost | 瀹㈡埛绔洰鏍囧湴鍧€ |
| `_mirrorHostObjectName` | MirrorHost | 妗ユ帴瀛愮墿浣撳悕锛堣嚜鍔ㄥ垱寤猴級 |
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **NetworkManager Event**.

- `NetworkManager.EVT_CLIENT_CONNECT`
- `NetworkManager.EVT_HOST_START`
- `NetworkManager.EVT_SEND_TO_ALL`
- `NetworkManager.EVT_SEND_TO_SERVER`
- `NetworkService.EVT_NET_MESSAGE`
- `EVT_BROADCAST`
- `EVT_DISCONNECT`
- `EVT_NET_ERROR`
- `EVT_NET_STATUS_CHANGED`
- `EVT_PEER_JOINED`
- `EVT_PEER_LEFT`
- `EVT_SEND_TO_PEER`
- `EVT_SERVER_START`


