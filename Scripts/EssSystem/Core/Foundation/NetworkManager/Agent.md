## NetworkManager / 多人联机网络通讯（基于 Mirror）

### 概述

把 [Mirror](https://mirror-networking.com/) 的 Host-Client 网络模型接入到框架的 EventProcessor，业务方完全通过事件驱动，不引用任何 Mirror 类型。

```
业务侧 TriggerEvent(EVT_HOST_START / EVT_CLIENT_CONNECT / EVT_SEND_*)
   -> NetworkManager [Event] handler
   -> WulaNetworkManagerBehaviour (Mirror.NetworkManager subclass)
   -> KCP / WebSocket Transport <-> 远端
   -> Mirror 回调 -> NetworkService.NotifyXxx
   -> EventProcessor.TriggerEvent(EVT_NET_STATUS_CHANGED / EVT_NET_MESSAGE ...)
   -> 业务订阅方
```

### 自动安装 Mirror

第一次挂载 NetworkManager 时，编辑器会触发 `MirrorInstaller`：

1. 检测 `Packages/manifest.json`，缺则注入 OpenUPM scoped registry
2. 调用 `Client.Add("com.mirror-networking.mirror")` 安装包
3. 安装完成后设置 `MIRROR_INSTALLED` 编译宏（Standalone / Android / iOS / WebGL）

菜单：`Tools/WulaFramework/Network/`
- `Install Mirror Now` —— 手动触发
- `Uninstall Mirror`
- `Toggle Auto-Install` —— 关闭后不再弹窗
- `Check Mirror Status`

未装 Mirror 时所有命令事件返回 `ResultCode.Fail("Mirror 未安装：...")`，不会编译报错。

### 模块结构

```
NetworkManager/
  NetworkManager.cs          -- 门面 [Manager(2)]：Inspector + [Event] 命令处理
  NetworkService.cs          -- Service<>：状态 + 广播 EVT_* + Payload 编解码
  Agent.md
  README.md
  Editor/
    MirrorInstaller.cs       -- [InitializeOnLoad] 自动安装 Mirror（OpenUPM）
  Runtime/                   -- #if MIRROR_INSTALLED 才编译
    WulaNetworkManagerBehaviour.cs  -- 继承 Mirror.NetworkManager
    NetMessage.cs                   -- struct WulaNetMessage : NetworkMessage
```

命名空间：
- `EssSystem.Manager.NetworkManager` —— Manager / Service / 枚举
- `EssSystem.Manager.NetworkManager.Runtime` —— Mirror 桥接（Mirror 缺失时为空）
- `EssSystem.Manager.NetworkManager.EditorTools` —— 编辑器安装器

### Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_autoStart` | false | Initialize 后自动按 `_autoMode` 启动 |
| `_autoMode` | None | `Host` / `ServerOnly` / `Client` |
| `_port` | 7777 | 监听 / 连接端口 |
| `_serverAddress` | localhost | 客户端目标地址 |
| `_mirrorHostObjectName` | MirrorHost | 桥接子物体名（自动创建） |

## Event API

### 命令事件（业务方 -> NetworkManager）

通过 `EventProcessor.Instance.TriggerEventMethod(EVT_*, args)` 调用。

- `EVT_HOST_START` = `"NetHostStart"` -- 参数 `[ushort? port]` -- 启动主机（Server + 本地 Client）
- `EVT_SERVER_START` = `"NetServerStart"` -- 参数 `[ushort? port]` -- 纯专用服务器
- `EVT_CLIENT_CONNECT` = `"NetClientConnect"` -- 参数 `[string address, ushort? port]` -- 连接到指定服务器
- `EVT_DISCONNECT` = `"NetDisconnect"` -- 无参 -- 停止当前角色（幂等）
- `EVT_SEND_TO_SERVER` = `"NetSendToServer"` -- `[string topic, object payload]` -- Client -> Server
- `EVT_SEND_TO_ALL` = `"NetSendToAll"` -- `[string topic, object payload]` -- Server -> 所有就绪 Client
- `EVT_SEND_TO_PEER` = `"NetSendToPeer"` -- `[int connectionId, string topic, object payload]` -- Server -> 指定 Client
- `EVT_BROADCAST` = `"NetBroadcast"` -- `[string topic, object payload]` -- 对等广播：任意节点调用，所有节点都收到一次 `EVT_NET_MESSAGE`（客户端发到服务器，服务器自动 fan-out + 本机自我通知）

payload 通过 `NetworkService.EncodePayload` 自动 JSON 编码，支持 string / long / double / bool / List / Dictionary。

### 广播事件（NetworkService -> 业务方）

通过 `[EventListener(NetworkService.EVT_*)]` 订阅。

- `EVT_NET_STATUS_CHANGED` = `"OnNetworkStatusChanged"` -- `[NetworkRole role, bool connected]`
- `EVT_PEER_JOINED` = `"OnNetworkPeerJoined"` -- `[int connectionId]`（仅 Server 触发）
- `EVT_PEER_LEFT` = `"OnNetworkPeerLeft"` -- `[int connectionId]`（仅 Server 触发）
- `EVT_NET_MESSAGE` = `"OnNetworkMessage"` -- `[int senderConnectionId, string topic, string payloadJson]`
- `EVT_NET_ERROR` = `"OnNetworkError"` -- `[string source, string message]`

订阅端解码 payload：

```csharp
[EventListener(NetworkService.EVT_NET_MESSAGE)]
void OnNetMsg(List<object> data) {
    var senderId = (int)data[0];
    var topic = (string)data[1];
    var payload = NetworkService.DecodePayload((string)data[2]);  // -> Dict/List/原始
    if (topic == "Chat") {
        var dict = (Dictionary<string,object>)payload;
        Debug.Log($"[{senderId}] {dict["text"]}");
    }
}
```

### 典型用法

主机：
```csharp
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_HOST_START,
    new List<object>{ (ushort)7777 });
```

客户端：
```csharp
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_CLIENT_CONNECT,
    new List<object>{ "192.168.0.5", (ushort)7777 });
```

广播聊天（在 Server / Host 上）：
```csharp
var payload = new Dictionary<string,object>{
    {"text", "hello world"}, {"from", "Alice"}
};
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_SEND_TO_ALL,
    new List<object>{ "Chat", payload });
```

客户端发请求：
```csharp
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_SEND_TO_SERVER,
    new List<object>{ "RequestSpawn", new Dictionary<string,object>{ {"x",1.0},{"y",2.0} } });
```

### 设计原则

- **业务零依赖 Mirror**：上层代码不出现 `using Mirror;`，全部走 EVT_*
- **Topic 是字符串路由**：避免每条消息一个 struct，可以快速迭代；性能敏感的玩法（位置同步）后续可单独接 Mirror NetworkBehaviour
- **Payload JSON 化**：序列化稳定、跨版本兼容；强类型 NetworkWriter 路径以后再上
- **状态广播只在主线程触发**：Mirror 的回调本身在主线程，无需 MainThreadDispatcher
- **Manager.OnDestroy / OnApplicationQuit 自动 Disconnect**

### 已知限制

- payload 嵌套类型必须是 MiniJson 支持的（string/数值/bool/List/Dictionary），自定义类先 `ToDictionary()`
- 高频高吞吐（>200/s）建议绕开本封装，直接派生 `Mirror.NetworkBehaviour` 用 SyncVar / Cmd / Rpc
- WebGL 平台 KCP 不可用，需切 SimpleWebTransport（Mirror 自带，手动替换 Transport 组件即可）
- 首次安装 Mirror 会触发 Unity 重启编译（约 30-60s），编译完成后自动设置 `MIRROR_INSTALLED` 宏
