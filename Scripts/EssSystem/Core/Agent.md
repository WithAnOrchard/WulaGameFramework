# Core 机制 Agent 指南

## 概述

EssSystem.Core 是 EssSystem 框架的核心模块，提供基础架构和管理机制。本指南面向 AI Agent，说明 Core 整体架构及各子模块的协作关系。

## Core 架构

### 核心模块

Core 包含以下主要子模块：

1. **EssManagers** - 管理器系统
   - Manager - 管理器基类
   - Service - 服务基类
   - DataManager - 数据管理
   - ResourceManager - 资源管理
   - UIManager - UI 管理
   - Agent.md: `EssSystem/Core/EssManagers/Agent.md`

2. **Event** - 事件系统
   - EventManager - 事件管理器
   - EventProcessor - 事件处理器
   - Event - 事件特性
   - EventListener - 事件监听器特性
   - Agent.md: `EssSystem/Core/Event/Agent.md`

3. **Singleton** - 单例模式
   - SingletonNormal - 普通类单例
   - SingletonMono - MonoBehaviour 单例
   - Agent.md: `EssSystem/Core/Singleton/Agent.md`

4. **Util** - 工具类
   - MainThreadDispatcher - 主线程调度器
   - MiniJson - JSON序列化工具

5. **AbstractGameManager** - 游戏管理器基类
   - 负责自动发现和初始化所有 Manager

## 模块依赖关系

### 初始化顺序

```
EventManager (-30)
    ↓
DataManager (-20)
    ↓
ResourceManager (0)
    ↓
UIManager (5)
    ↓
其他业务 Manager (10+)
```

### 依赖说明

- **EventManager** 优先级最高，必须最先初始化
  - 被 Service 初始化事件依赖
  - 被所有跨模块通信依赖

- **DataManager** 优先级 -20
  - 依赖 EventManager（Service 自动注册需要事件系统）
  - 被 Service 数据持久化依赖

- **ResourceManager** 优先级 0
  - 依赖 DataManager（资源配置持久化）
  - 被所有需要资源的 Manager 依赖

- **UIManager** 优先级 5
  - 依赖 ResourceManager（UI 资源加载）
  - 被游戏逻辑 Manager 依赖

### Service 自动注册机制

```
Service 初始化
    ↓
触发 OnServiceInitialized 事件
    ↓
DataService 监听事件
    ↓
自动注册 Service
    ↓
加载 Service 数据
```

## 子模块文档索引

### EssManagers
详见：[EssManagers Agent 指南](EssSystem/Core/EssManagers/Agent.md)

**核心功能**：
- Manager<T> - MonoBehaviour 管理器基类
- Service<T> - 非 MonoBehaviour 服务基类
- DataManager - 数据持久化管理
- ResourceManager - 资源加载管理
- UIManager - UI 实体管理

### Event
详见：[Event Agent 指南](EssSystem/Core/Event/Agent.md)

**核心功能**：
- EventManager - 全局事件管理器（优先级 -30）
- EventProcessor - 自动注册 [Event] 和 [EventListener] 方法
- 事件驱动架构支持跨模块通信

### Singleton
详见：[Singleton Agent 指南](EssSystem/Core/Singleton/Agent.md)

**核心功能**：
- SingletonNormal<T> - 普通类单例（线程安全）
- SingletonMono<T> - MonoBehaviour 单例（DontDestroyOnLoad）

### AbstractGameManager

**核心功能**：
- 自动发现所有继承自 Manager<T> 的类
- 按优先级顺序初始化 Manager
- 确保 EventManager 和 DataManager 按正确顺序初始化

## 使用指南

### 创建新 Manager

```csharp
[Manager(10)]  // 设置优先级
public class MyManager : Manager<MyManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
    }
}
```

### 创建新 Service

```csharp
public class MyService : Service<MyService>
{
    public const string MY_CATEGORY = "MyData";

    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
        // Service 会自动触发 OnServiceInitialized 事件
        // DataService 会自动注册并加载此 Service 的数据
    }

    [Event("MyServiceMethod")]
    public List<object> MyMethod(List<object> data)
    {
        // 业务逻辑
        return new List<object> { "成功" };
    }
}
```

### 跨模块通信

```csharp
// 使用 EventManager 触发事件（跨 Manager 通信）
EventManager.Instance.TriggerEvent("OnMyEvent", new List<object> { data });

// 使用 EventProcessor 调用 Event 方法（调用本地 Service）
EventProcessor.Instance.TriggerEventMethod("MyServiceMethod", new List<object> { data });
```

## 架构规范

### 1. Manager 规范
- Manager 必须继承自 Manager<T>
- 必须使用 [Manager(priority)] 特性设置优先级
- Manager 的公开方法必须标记 [Event] 或 [EventListener]
- Manager 只能调用本地 Service，不能直接访问其他 Manager 的 Service

### 2. Service 规范
- Service 必须继承自 Service<T>
- Service 的公开方法必须标记 [Event] 特性
- Service 初始化时会自动触发 OnServiceInitialized 事件
- Service 数据会自动被 DataService 持久化

### 3. 通信规范
- Manager → 本地 Service：使用 EventProcessor.TriggerEventMethod()
- Manager → 其他 Manager：使用 EventManager.TriggerEvent()
- Service → Manager：使用 EventManager.TriggerEvent()
- 禁止直接访问其他 Manager 或其他 Manager 的 Service

### 4. 文件组织规范
- 数据类（Dao）必须放在 Dao 文件夹
- UI Entity 统一由 UIManager 管理，其他模块不得包含 Entity 文件夹
- 业务模块只负责 Dao 数据和 Service 业务逻辑，UI 表现层由 UIManager 统一处理

## 快速参考

### 优先级设置
- EventManager: -30（最高）
- DataManager: -20
- ResourceManager: 0
- UIManager: 5
- 业务 Manager: 5-10
- 游戏逻辑 Manager: 10+

### 事件命名
- Event 方法：动词开头，如 `GetData`, `SaveData`
- EventListener：On 开头，如 `OnPlayerDamage`, `OnSceneLoaded`

### 数据分类
- Service 使用常量定义分类名称
- 示例：`public const string CONFIG_CATEGORY = "Config";`

## 常见问题

### Q: 如何确定 Manager 的优先级？
A: 根据依赖关系，被依赖的 Manager 优先级更高（数值更小）。EventManager 必须是 -30，DataManager 必须是 -20。

### Q: Service 如何自动注册到 DataManager？
A: Service 在初始化时会自动触发 OnServiceInitialized 事件，DataManager 监听该事件并自动注册。

### Q: 如何在 Manager 中调用本地 Service？
A: 使用 EventProcessor.Instance.TriggerEventMethod("ServiceMethodName", data)。

### Q: 如何跨 Manager 通信？
A: 使用 EventManager.Instance.TriggerEvent("EventName", data)，目标 Manager 使用 [EventListener] 监听。

### Q: EventManager 为什么必须是 Manager？
A: EventManager 作为 Manager 可以确保它在其他 Manager 和 Service 初始化前完成初始化，避免初始化顺序问题。

### Q: Service 数据何时保存？
A: DataManager 在 Application.quitting 时自动保存所有 Service 数据。

### Q: 如何查看所有已注册的 Service？
A: 使用 DataManager.Instance.GetServiceInstances() 方法。

## 更新日志

### 最新更新
- EventManager 改为继承自 Manager<EventManager>，优先级设置为 -30
- Service 初始化时自动触发 OnServiceInitialized 事件
- DataManager 通过事件机制自动注册 Service，移除扫描功能
- DataManager 精简，移除备份功能
- 即使 Service 数据为空也会生成保存文件
- UI Entity 统一由 UIManager 管理，其他模块不得包含 Entity 文件夹
- Service 数据持久化使用 MiniJson 支持嵌套对象和友好格式

## 相关文档

- [EssManagers Agent 指南](EssSystem/Core/EssManagers/Agent.md)
- [Event Agent 指南](EssSystem/Core/Event/Agent.md)
- [Singleton Agent 指南](EssSystem/Core/Singleton/Agent.md)
