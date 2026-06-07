# NetworkManager 网络模块

## 职责
- 封装 Mirror 启动、连接、网络状态、消息路由和编辑器安装辅助。
- 模块路径：`Scripts/EssSystem/Core/Foundation/NetworkManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Editor/`
- `Runtime/`
- `NetworkManager.cs`
- `NetworkService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `NetworkManager.EVT_BROADCAST` = `"NetBroadcast"`
- `NetworkManager.EVT_CLIENT_CONNECT` = `"NetClientConnect"`
- `NetworkManager.EVT_DISCONNECT` = `"NetDisconnect"`
- `NetworkManager.EVT_HOST_START` = `"NetHostStart"`
- `NetworkManager.EVT_SEND_TO_ALL` = `"NetSendToAll"`
- `NetworkManager.EVT_SEND_TO_PEER` = `"NetSendToPeer"`
- `NetworkManager.EVT_SEND_TO_SERVER` = `"NetSendToServer"`
- `NetworkManager.EVT_SERVER_START` = `"NetServerStart"`
- `NetworkService.EVT_NET_ERROR` = `"OnNetworkError"`
- `NetworkService.EVT_NET_MESSAGE` = `"OnNetworkMessage"`
- `NetworkService.EVT_NET_STATUS_CHANGED` = `"OnNetworkStatusChanged"`
- `NetworkService.EVT_PEER_JOINED` = `"OnNetworkPeerJoined"`
- `NetworkService.EVT_PEER_LEFT` = `"OnNetworkPeerLeft"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
