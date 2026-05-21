# LiveStatusManager / B 站直播开播状态轮询

## 概述

不需要主播身份码或登录态，仅通过公开接口 `api.live.bilibili.com/room/v1/Room/get_info` 定时拉取直播间状态，把"开播 / 下播 / 轮询心跳"统一转成框架事件，方便业务方做 UI 切换、桌宠表情切换等。

```
LiveStatusManager (Inspector: roomId, intervalSeconds, autoStart)
   -> LiveStatusService.StartPolling(roomId, interval)
      -> 每 N 秒 GET get_info?room_id=xxx
         -> 解析 JSON -> LiveRoomInfo (live_status: 0/1/2)
         -> 边沿检测 + 状态广播
            -> EVT_STATUS_POLLED   (每次轮询)
            -> EVT_LIVE_STARTED    (非1 -> 1)
            -> EVT_LIVE_ENDED      (1 -> 非1)
```

## 模块结构

```
LiveStatusManager/
  LiveStatusManager.cs   -- [Manager(50)] 门面：Inspector + 启动钩子
  LiveStatusService.cs   -- Service<>：HTTP 轮询 + 事件广播
  LiveRoomInfo.cs        -- DTO：room_id / live_status / title / online ...
  Agent.md
```

## Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_roomId` | 0 | 直播间号（不是 UID）。≤0 不启动轮询 |
| `_autoStart` | true | Initialize 后自动 StartPolling |
| `_intervalSeconds` | 30 | 轮询间隔，建议 ≥15s 避免被风控 |

## Event API

### 广播事件（LiveStatusService -> 业务方）

通过 `[EventListener(LiveStatusService.EVT_*)]` 订阅。

- `EVT_LIVE_STARTED` = `"OnLiveStarted"` -- 参数 `[long roomId, string title, LiveRoomInfo info]` -- 状态由非 1 切到 1（开播边沿）
- `EVT_LIVE_ENDED` = `"OnLiveEnded"` -- 参数 `[long roomId, string title, LiveRoomInfo info]` -- 状态由 1 切到非 1（下播 / 轮播）
- `EVT_STATUS_POLLED` = `"OnLiveStatusPolled"` -- 参数 `[long roomId, int liveStatus, string title, LiveRoomInfo info]` -- 每次轮询都触发，无论状态是否变更

`liveStatus` 含义：`0` 未播 / `1` 直播中 / `2` 轮播。

## 典型用法

```csharp
[EventListener(LiveStatusService.EVT_LIVE_STARTED)]
public List<object> OnLiveStart(List<object> data) {
    var roomId = (long)data[0];
    var title = (string)data[1];
    Debug.Log($"开播了：{roomId} - {title}");
    return null;
}

[EventListener(LiveStatusService.EVT_LIVE_ENDED)]
public List<object> OnLiveEnd(List<object> data) {
    Debug.Log("下播");
    return null;
}
```

手动控制轮询：

```csharp
LiveStatusService.Instance.StartPolling(roomId: 123456, intervalSeconds: 30f);
LiveStatusService.Instance.StopPolling();
```

## 设计原则

- **不需要登录态**：仅 GET 公开接口，不传 cookie；适合"桌宠 + 主播开播提示"等不需要弹幕的场景
- **状态边沿广播**：开播 / 下播只在状态切换时触发一次，避免业务侧每秒处理重复事件
- **轮询心跳保留**：`EVT_STATUS_POLLED` 每次都触发，用于"在线人数 / 标题 / 封面"等持续刷新的 UI
- **请求失败容错**：HTTP 失败 / JSON 解析失败仅打 Warning 不抛异常，下一次轮询继续

## 已知限制

- 仅支持 Bilibili 直播间。其他平台需自己写一份 Service
- 间隔过小（<10s）可能被官方限流；30s 是经验值
- `EVT_LIVE_STARTED` 不会在第一次拉取时触发（边沿检测排除冷启动），如需冷启动播放可订阅 `EVT_STATUS_POLLED` 自己判断 `liveStatus == 1`
