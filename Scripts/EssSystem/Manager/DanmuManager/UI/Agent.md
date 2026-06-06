# DanmuManager UI 模块
## 概述
弹幕测试面板 UI 组件，用于显示实时弹幕、礼物、SC 等直播互动信息。
## 文件结构
```
UI/
├── DanmuTestPanel.cs       ← 弹幕面板逻辑（单例，[EventListener] 订阅）
└── DanmuTestPanelView.cs   ← 面板视图（MonoBehaviour）
```
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **UI Event**.

- `DanmuService.EVT_CONNECTED`
- `DanmuService.EVT_DANMAKU`
- `DanmuService.EVT_DISCONNECTED`
- `DanmuService.EVT_GIFT`
- `DanmuService.EVT_SC`

## 使用示例
