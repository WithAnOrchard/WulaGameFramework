# NetworkManager 快速上手

挂载 -> 自动装 Mirror -> 用 Event 通讯。

## 1. 挂载

在场景或框架 GameManager 上挂 `NetworkManager`（自动单例，无需手动 AddComponent 也可，框架会保证存在）。

## 2. 首次自动安装 Mirror

挂载后编辑器会弹窗：

> WulaFramework · Mirror 未安装
> 点击 "立即安装" -> 自动注入 OpenUPM + 安装 com.mirror-networking.mirror + 设置 MIRROR_INSTALLED 宏

如未弹窗或安装失败，手动执行菜单：
`Tools/WulaFramework/Network/Install Mirror Now`

## 3. 三行代码联机 Demo

```csharp
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Manager.NetworkManager;

// A 设备：当主机
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_HOST_START,
    new List<object> { (ushort)7777 });

// B 设备：连过来
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_CLIENT_CONNECT,
    new List<object> { "192.168.0.5", (ushort)7777 });

// 任意一端发消息
EventProcessor.Instance.TriggerEvent(NetworkManager.EVT_SEND_TO_SERVER,
    new List<object> { "Hello", new Dictionary<string,object>{ {"msg","hi"} } });

// 任意一端订阅消息
public class ChatHandler {
    [EventListener(NetworkService.EVT_NET_MESSAGE)]
    public void OnMsg(List<object> data) {
        var topic = (string)data[1];
        var payload = NetworkService.DecodePayload((string)data[2]);
        UnityEngine.Debug.Log($"{topic}: {payload}");
    }
}
```

详见 `Agent.md`。
