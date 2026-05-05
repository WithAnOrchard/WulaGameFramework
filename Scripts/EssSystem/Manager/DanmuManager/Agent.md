# DanmuManager / B 站直播弹幕接入

## 概述

把 B 站直播开放平台的实时弹幕接入到框架的 `EventProcessor`，业务方只需 `[EventListener(DanmuService.EVT_*)]` 订阅即可。

```
身份码 → 签名代理(/sign) → biliapi(/v2/app/start)
       → wss + auth → OpenDanmakuLoader 长连接
       → DanmakuModel 回调（后台线程）
       → MainThreadDispatcher 切主线程
       → EventProcessor.TriggerEvent(EVT_*)
```

## 零外部依赖

本模块不依赖 `Newtonsoft.Json`。JSON 解析全部走框架自带的 `EssSystem.Core.Util.MiniJson`：
- `MiniJson.Serialize(dict)` — 把 `Dictionary<string,object>` 序列化为紧凑 JSON
- `MiniJson.Parse(json)` — 返回 `JsonNode` 包装器，语义 ≈ Newtonsoft `JToken`
  - `node["key"]["sub"][0].ToString()` / `.ToObject<int>()` / `.Value<string>("name")` / `.ToList()`
  - 缺失 key / 越界返回 `JsonNode.Missing`，**不抛异常**
  - `ToString()` 对 null/缺失返回 `""`（不是 `"null"`）

这意味着本模块可以直接在纯净 Unity 工程（不安装 `com.unity.nuget.newtonsoft-json`）下运行。

## 模块结构（Manager + Service + Dao + Net，严格遵循框架约定）

```
DanmuManager/
├── DanmuManager.cs             ← 门面（[Manager(50)]）：Inspector + 生命周期 + 调用 Service
├── DanmuService.cs             ← 业务（Service<>）：HTTP 鉴权、长连接生命周期、广播 EVT_*
├── Agent.md
├── Dao/                         ← 纯数据类（仅依赖 `MiniJson` + `System.*`）
│   ├── DanmakuModel.cs         ← 核心消息模型 + `MsgTypeEnum` + `InteractTypeEnum`；构造函数用 `MiniJson.Parse(json)` 解析
│   ├── DanmakuEvents.cs        ← 底层事件委托 + *EventArgs（仅 Net 层内部用）
│   └── GiftRank.cs             ← 礼物排行条目（`INotifyPropertyChanged`）
└── Net/                         ← 底层 WebSocket 协议（仅 DanmuService 引用）
    ├── OpenDanmakuLoader.cs    ← 长连接主类
    ├── DanmakuProtocol.cs      ← 16 字节包头解析
    ├── StreamExtensions.cs     ← `Stream.ReadBAsync` 填满扩展（internal）
    └── EndianBitConverter/     ← 第三方 MS 大端转换库（namespace `BiliBiliDanmu.Net.Internal`）
```

**命名空间规范**：
- `BiliBiliDanmu` — Manager / Service
- `BiliBiliDanmu.Dao` — 数据模型
- `BiliBiliDanmu.Net` — 网络层
- `BiliBiliDanmu.Net.Internal` — 第三方嵌入库（避免与 `System.BitConverter` 冲突）

**职责严格分层**：
- 所有 EVT_* 常量定义在 `DanmuService`（按约定：广播事件归 Service）
- `DanmuManager` 不持有任何业务状态，只是 Inspector 配置 + 生命周期钩子
- `Dao` 只含纯数据，不引用 `Net`
- `Net` 只对 `Dao` 单向依赖（读模型 + 触发底层事件）
- 业务层**禁止**直接 `using BiliBiliDanmu.Net` —— 网络层是 Service 的私有细节

## Inspector 配置

| 字段 | 默认 | 说明 |
|---|---|---|
| `_identityCode` | `E63TXTNA49OG5` | 主播身份码（直播姬→开放平台→主播识别码）。空 = 不自动连 |
| `_appId` | `1651388990835` | 第三方 AppId（B 站开发者后台获取） |
| `_autoConnect` | `true` | Awake 后是否立即 `ConnectAsync` |
| `_signEndpoint` | `https://bopen.ceve-market.org/sign` | 社区签名代理（B 站官方鉴权要服务端签名，借用社区代理简化） |
| `_startEndpoint` | `https://live-open.biliapi.com/v2/app/start` | B 站官方应用启动入口 |
| `_httpTimeoutSeconds` | `5` | HTTP 超时 |
| `_serviceEnableLogging` | `true` | 框架统一日志开关（继承自 Manager 基类） |

## Public C# API

```csharp
// 经 Manager 封装（Inspector 友好）
DanmuManager.Instance.ConnectAsync();   // 用 Inspector 当前配置连接
DanmuManager.Instance.Disconnect();     // 幂等断开
DanmuManager.Instance.Reconnect();      // ContextMenu 也可触发

// 直接走 Service（绕过 Inspector，自定义参数）
DanmuService.Instance.IsConnected;
DanmuService.Instance.RoomId;           // 已连接房间号；未连=0
await DanmuService.Instance.ConnectAsync(code, appId, signEndpoint, startEndpoint, timeout);
DanmuService.Instance.Disconnect();
```

## 线程模型（**重点**）

`OpenDanmakuLoader` 的 `ReceivedDanmaku` / `Disconnected` 事件来自 **后台线程**。
`DanmuService` 内部全部 `MainThreadDispatcher.Enqueue(...)` 切主线程后才调 `EventProcessor.TriggerEvent`。
**业务方订阅时无需关心线程问题**，可以在订阅回调里安全调 Unity API（`GameObject.Instantiate`、`Transform.position` 等）。

## Event API

> 全部为**广播**（订阅向）。常量挂在 `DanmuService` 上 —— 按框架约定：广播 = Service / 命令 = Manager。

### `EVT_CONNECTED` — 连接成功（广播）
- **常量**: `DanmuService.EVT_CONNECTED` = `"OnDanmuConnected"`
- **触发**: `DanmuService.ConnectAsync()` 握手成功后切主线程触发
- **参数**: `[long roomId]`
- **副作用**: 无（仅通知）
- **示例**:
  ```csharp
  [EventListener(DanmuService.EVT_CONNECTED)]
  void OnConnected(List<object> data) {
      var roomId = (long)data[0];
      Debug.Log($"Bilibili room {roomId} live!");
  }
  ```

### `EVT_DISCONNECTED` — 连接断开（广播）
- **常量**: `DanmuService.EVT_DISCONNECTED` = `"OnDanmuDisconnected"`
- **触发**: `OpenDanmakuLoader.Disconnected` 触发后切主线程
- **参数**: `[Exception errorOrNull]`（正常断开为 `null`，异常断开会带 Exception）
- **副作用**: 无（业务可在此重连）
- **示例**:
  ```csharp
  [EventListener(DanmuService.EVT_DISCONNECTED)]
  void OnDc(List<object> data) {
      var err = data[0] as Exception;
      if (err != null) DanmuManager.Instance.Reconnect();
  }
  ```

### `EVT_DANMAKU` — 普通弹幕评论（广播）
- **常量**: `DanmuService.EVT_DANMAKU` = `"OnDanmuComment"`
- **触发**: 收到 `MsgTypeEnum.Comment` 类型消息
- **参数**: `[string userName, string commentText, long userId]`
- **副作用**: 无
- **示例**:
  ```csharp
  [EventListener(DanmuService.EVT_DANMAKU)]
  void OnComment(List<object> data) {
      var name = (string)data[0];
      var text = (string)data[1];
      Debug.Log($"{name}: {text}");
  }
  ```

### `EVT_GIFT` — 礼物（广播）
- **常量**: `DanmuService.EVT_GIFT` = `"OnDanmuGift"`
- **触发**: 收到 `MsgTypeEnum.GiftSend`
- **参数**: `[string userName, string giftName, int giftCount, long userId]`
- **副作用**: 无
- **示例**:
  ```csharp
  [EventListener(DanmuService.EVT_GIFT)]
  void OnGift(List<object> data) {
      Debug.Log($"{data[0]} 送了 {data[2]}× {data[1]}");
  }
  ```

### `EVT_RAW` — 原始消息（广播，所有类型）
- **常量**: `DanmuService.EVT_RAW` = `"OnDanmuRaw"`
- **触发**: 任何类型的弹幕到达（含 LiveStart/LiveEnd/Welcome/SuperChat 等）
- **参数**: `[DanmakuModel model]`（完整模型，自带 `MsgType` 判别字段）
- **副作用**: 无
- **使用场景**: 需要 SuperChat、上船、舰长等高级类型时用此 Event 自行 dispatch
- **示例**:
  ```csharp
  [EventListener(DanmuService.EVT_RAW)]
  void OnRaw(List<object> data) {
      var dm = (DanmakuModel)data[0];
      if (dm.MsgType == MsgTypeEnum.SuperChat) HandleSC(dm);
  }
  ```

## 注意事项

- **第一次连接**有 1-3 秒延迟（HTTP 鉴权 + 握手），不要在连接立刻就期待事件到达
- **签名代理依赖第三方**：`bopen.ceve-market.org` 离线时无法连接；自部署可改 `_signEndpoint`
- **HttpClient 共享**：使用 static 单例 + Inspector 控制超时；不要在业务侧再创建 HttpClient（端口耗尽风险）
- **关掉 `_autoConnect`** 用于离线开发：避免每次进 Play 模式都跑外网请求
- **重连**：`OnLoaderDisconnected` 不会自动重连，业务方按需在 `EVT_DISCONNECTED` 订阅里调 `Reconnect()`
- **OnDestroy / OnApplicationQuit**：Manager 已自动 `Disconnect()`，避免线程泄漏
- **MiniJson 精度取舍**：SC 金额等 `decimal` 从 JSON 浮点转换（MiniJson 解析 float 为 `double`），小数点 2 位以内精确；如未来 B 站引入 8+ 位小数字段需自行走 `MiniJson.Parse` 拿 `JsonNode.Raw` 手工解析
- **未知消息类型不抛异常**：MiniJson 的 `JsonNode` 缺失 key 返回 `Missing` 而不是抛异常，DanmakuModel 解析新类型消息时若字段缺失会默默赋 default，不会炸掉整条连接。需要严格校验时订阅 `EVT_RAW` 自行检查 `dm.RawDataJToken`
