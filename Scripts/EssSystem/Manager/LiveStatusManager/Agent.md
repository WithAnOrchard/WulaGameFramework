# LiveStatusManager 直播状态模块

## 职责
- 轮询直播间状态并广播开播、下播和状态刷新事件。
- 模块路径：`Scripts/EssSystem/Manager/LiveStatusManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `LiveRoomInfo.cs`
- `LiveStatusManager.cs`
- `LiveStatusService.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `LiveStatusService.EVT_LIVE_ENDED` = `"OnLiveEnded"`
- `LiveStatusService.EVT_LIVE_STARTED` = `"OnLiveStarted"`
- `LiveStatusService.EVT_STATUS_POLLED` = `"OnLiveStatusPolled"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
