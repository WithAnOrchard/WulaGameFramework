# DanmuManager 弹幕模块

## 职责
- 负责直播弹幕连接、消息解析、礼物、醒目留言和原始消息广播。
- 模块路径：`Scripts/EssSystem/Manager/DanmuManager`。
- 本文档只记录模块契约，具体实现细节以代码为准。

## 结构
- `Auth/`
- `Dao/`
- `Net/`
- `UI/`
- `BilibiliDanmuManager.cs`
- `DanmuService.cs`
- `WbiSigner.cs`

## 边界
- 本模块只拥有“职责”中描述的行为，不隐式接管兄弟模块职责。
- 跨模块协作优先使用 EventProcessor 字符串协议，或目标模块明确暴露的窄接口。
- Demo 专属逻辑留在 Demo 目录，除非已经确认可复用为框架能力。

## Event API
- `DanmuService.EVT_CONNECTED` = `"OnDanmuConnected"`
- `DanmuService.EVT_DANMAKU` = `"OnDanmuComment"`
- `DanmuService.EVT_DISCONNECTED` = `"OnDanmuDisconnected"`
- `DanmuService.EVT_GIFT` = `"OnDanmuGift"`
- `DanmuService.EVT_RAW` = `"OnDanmuRaw"`
- `DanmuService.EVT_SC` = `"OnDanmuSuperChat"`

## 维护注意
- 新增、改名或删除事件常量时，同步更新本节和根目录 Events.md。
- 示例保持最小化；实现细节写在代码注释里，模块契约写在本文档里。
- 已完成的 TODO 从本文档移除，必要时移动到 TODO.md。
