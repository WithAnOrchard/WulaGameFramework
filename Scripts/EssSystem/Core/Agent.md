# Core 模块指南

## 概述

`EssSystem.Core` 是框架的核心层，提供单例、事件总线、Manager/Service 基类与数据持久化。本指南面向 AI Agent，说明 Core 的整体结构与子模块协作关系。

## 目录结构

```
Core/
├── AbstractGameManager.cs   ← 启动入口，自动发现并按优先级初始化 Manager
├── EssManagers/
│   ├── Manager/             ← Manager<T> / Service<T> 基类
│   ├── DataManager/         ← 数据持久化
│   ├── ResourceManager/     ← 资源加载/缓存
│   └── UIManager/           ← UI 实体注册中心
├── Event/                   ← EventProcessor（统一事件中心）
├── Singleton/               ← SingletonNormal / SingletonMono
└── Util/                    ← AssemblyUtils, MainThreadDispatcher, MiniJson, ResultCode
```

## 初始化优先级（数值越小越先 Awake）

| Manager | 优先级 | 职责 |
|---|---:|---|
| `EventProcessor` | **-30** | 事件总线 + `[Event]`/`[EventListener]` 自动注册 |
| `DataManager` | -20 | Service 自动注册 + 应用退出时持久化 |
| `ResourceManager` | 0 | 资源加载/缓存 |
| `UIManager` | 5 | Canvas + UI 实体注册 |
| 业务 Manager | 10+ | InventoryManager(10) 等 |

## 通信模式

| 场景 | 推荐方式 |
|---|---|
| 框架内部强类型调用（如 UIManager → UIService） | 直接调用 `Service.Instance.XXX(...)` |
| 跨模块解耦调用 | `EventProcessor.Instance.TriggerEvent("EventName", data)` |
| 调用 `[Event]` 标注的方法（无监听器） | `EventProcessor.Instance.TriggerEventMethod("EventName", data)` |
| 监听广播事件 | 在方法上加 `[EventListener("EventName")]` |

## Service 自动注册流程

```
Service 构造 → SingletonNormal.Instance 创建实例
         ↓
Service.Initialize() → 触发 "OnServiceInitialized" 事件
         ↓
DataService 监听该事件 → 自动加入 _serviceInstances
         ↓
Application.quitting → DataService 调用 SaveAllCategories() 保存
```

## ResultCode

`EssSystem.Core.ResultCode`（位于 `Util/ResultCode.cs`，namespace 仍为 `EssSystem.Core`）：
- `ResultCode.OK` / `ResultCode.ERROR` 常量
- `ResultCode.Ok(data?)` / `ResultCode.Fail(msg)` 构造结果列表
- `ResultCode.IsOk(result)` 判断成功

## 子模块文档

- [Singleton 指南](Singleton/Agent.md)
- [Event 指南](Event/Agent.md)
- [EssManagers 指南](EssManagers/Agent.md)
- [Manager/Service 基类指南](EssManagers/Manager/Agent.md)
- [DataManager 指南](EssManagers/DataManager/Agent.md)
- [ResourceManager 指南](EssManagers/ResourceManager/Agent.md)
- [UIManager 指南](EssManagers/UIManager/Agent.md)
