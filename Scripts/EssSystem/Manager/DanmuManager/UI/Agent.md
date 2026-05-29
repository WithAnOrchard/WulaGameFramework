# DanmuManager UI 模块

## 概述

弹幕测试面板 UI 组件，用于显示实时弹幕、礼物、SC 等直播互动信息。

## 文件结构

```
UI/
├── DanmuTestPanel.cs       ← 弹幕面板逻辑（单例，[EventListener] 订阅）
└── DanmuTestPanelView.cs   ← 面板视图（MonoBehaviour）
```

## 事件订阅

本模块仅订阅（[EventListener]），不定义事件。订阅来自 `DanmuService` 的事件：

- `DanmuService.EVT_CONNECTED` — 连接成功
- `DanmuService.EVT_DISCONNECTED` — 连接断开
- `DanmuService.EVT_DANMAKU` — 弹幕消息
- `DanmuService.EVT_GIFT` — 礼物事件
- `DanmuService.EVT_SC` — 超级留言

## 使用示例

```csharp
// 打开面板
DanmuTestPanel.Open();

// 关闭面板
DanmuTestPanel.Close();

// 切换显隐
DanmuTestPanel.Toggle();
```
