# EssManagers 机制 Agent 指南

## 概述

EssManagers 是 EssSystem 框架的管理器系统核心模块，提供统一的 MonoBehaviour 管理器基类、非 MonoBehaviour 服务基类，以及数据管理、资源管理、UI管理等基础服务。本指南面向 AI Agent，说明 EssManagers 整体架构及各子模块的协作关系。

## 核心架构

### 模块组成

EssManagers 包含以下核心组件：

1. **Manager<T>** - MonoBehaviour 管理器基类
   - 继承自 SingletonMono<T>
   - 支持优先级控制
   - 提供生命周期管理
   - 提供日志功能

2. **Service<T>** - 非 MonoBehaviour 服务基类
   - 继承自 SingletonNormal<T>
   - 内置分层数据存储
   - 自动数据持久化
   - 通过事件系统自动注册

3. **DataManager** - 数据管理
   - 继承自 Manager<DataManager>
   - 管理所有 Service 的数据持久化
   - 优先级 -20

4. **ResourceManager** - 资源管理
   - 继承自 Manager<ResourceManager>
   - 提供资源加载和缓存
   - 优先级 0

5. **UIManager** - UI 管理
   - 继承自 Manager<UIManager>
   - 统一管理所有 UI Entity
   - 优先级 5

## 子模块文档

### Manager 基类
详见：[Manager Agent 指南](EssSystem/Core/EssManagers/Manager/Agent.md)

**核心功能**：
- Manager<T> - MonoBehaviour 管理器基类
- Service<T> - 非 MonoBehaviour 服务基类
- 优先级控制机制
- 生命周期管理

### DataManager
详见：[DataManager Agent 指南](EssSystem/Core/EssManagers/DataManager/Agent.md)

**核心功能**：
- Service 自动注册机制
- 数据持久化管理
- 分类数据存储
- 事件驱动的数据保存

### ResourceManager
详见：[ResourceManager Agent 指南](EssSystem/Core/EssManagers/ResourceManager/Agent.md)

**核心功能**：
- 资源加载和缓存
- 异步资源加载
- 资源释放管理
- 资源配置持久化

### UIManager
详见：[UIManager Agent 指南](EssSystem/Core/EssManagers/UIManager/Agent.md)

**核心功能**：
- UI Entity 注册和注销
- UI Entity 查询
- UI Canvas 管理
- 统一的 UI 实体管理

## 架构规范

### 1. Manager 规范
- Manager 必须继承自 Manager<T>
- 必须使用 [Manager(priority)] 特性设置优先级
- Manager 的公开方法必须标记 [Event] 或 [EventListener]
- Manager 只能调用本地 Service，不能直接访问其他 Manager 的 Service
- Manager 挂载到 GameObject 上，由 AbstractGameManager 自动发现和初始化

### 2. Service 规范
- Service 必须继承自 Service<T>
- Service 的公开方法必须标记 [Event] 特性
- Service 初始化时会自动触发 OnServiceInitialized 事件
- Service 数据会自动被 DataManager 持久化
- Service 不需要挂载到 GameObject，通过单例模式管理

### 3. 通信规范
- Manager → 本地 Service：使用 EventProcessor.TriggerEventMethod()
- Manager → 其他 Manager：使用 EventManager.TriggerEvent()
- Service → Manager：使用 EventManager.TriggerEvent()
- 禁止直接访问其他 Manager 或其他 Manager 的 Service

### 4. 文件组织规范
- 数据类（Dao）必须放在 Dao 文件夹
- UI Entity 统一由 UIManager 管理，其他模块不得包含 Entity 文件夹
- 业务模块只负责 Dao 数据和 Service 业务逻辑，UI 表现层由 UIManager 统一处理

### 5. 优先级规范
- EventManager: -30（最高）
- DataManager: -20
- ResourceManager: 0
- UIManager: 5
- 业务 Manager: 5-10
- 游戏逻辑 Manager: 10+

## 初始化流程

### Manager 初始化流程

```
AbstractGameManager 启动
    ↓
扫描所有继承自 Manager<T> 的类
    ↓
按优先级排序
    ↓
依次初始化 Manager
    ↓
Manager.Awake()
    ↓
Manager.Initialize()
    ↓
Manager.Start()
```

### Service 初始化流程

```
Service 构造函数
    ↓
SingletonNormal.Instance 创建实例
    ↓
Service.Initialize()
    ↓
触发 OnServiceInitialized 事件
    ↓
DataManager 监听事件
    ↓
自动注册 Service
    ↓
加载 Service 数据
```

## 使用示例

### 创建业务 Manager

```csharp
[Manager(10)]
public class GameplayManager : Manager<GameplayManager>
{
    private PlayerService _playerService;

    protected override void Initialize()
    {
        base.Initialize();
        _playerService = PlayerService.Instance;
        Log("GameplayManager 初始化完成", Color.green);
    }

    [Event("UpdatePlayerHealth")]
    public List<object> UpdatePlayerHealth(List<object> data)
    {
        float health = (float)data[0];
        // 调用本地 Service
        EventProcessor.Instance.TriggerEventMethod("SetPlayerHealth", data);
        return new List<object> { "成功" };
    }
}
```

### 创建业务 Service

```csharp
public class PlayerService : Service<PlayerService>
{
    public const string PLAYER_CATEGORY = "Player";

    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
    }

    [Event("SetPlayerHealth")]
    public List<object> SetHealth(List<object> data)
    {
        float health = (float)data[0];
        SetData(PLAYER_CATEGORY, "Health", health);
        return new List<object> { "成功" };
    }
}
```

## 常见问题

### Q: Manager 和 Service 有什么区别？
A:
- **Manager**: 继承自 MonoBehaviour，需要挂载到 GameObject，适合需要 Unity 生命周期的系统
- **Service**: 普通类单例，不需要 GameObject，适合纯数据管理和业务逻辑

### Q: 如何决定使用 Manager 还是 Service？
A:
- 需要 Unity 生命周期（Update, FixedUpdate 等）→ 使用 Manager
- 纯数据管理和业务逻辑 → 使用 Service
- 需要与 Unity 场景交互 → 使用 Manager
- 不需要 Unity 依赖 → 使用 Service

### Q: Service 如何自动注册到 DataManager？
A: Service 在初始化时会自动触发 OnServiceInitialized 事件，DataManager 监听该事件并自动注册。

### Q: Manager 如何调用本地 Service？
A: 使用 EventProcessor 触发 Service 的 Event 方法：
```csharp
EventProcessor.Instance.TriggerEventMethod("ServiceMethodName", data);
```

### Q: Manager 如何与其他 Manager 通信？
A: 使用 EventManager 触发事件：
```csharp
EventManager.Instance.TriggerEvent("EventName", data);
```

### Q: UI Entity 应该放在哪里？
A: UI Entity 统一由 UIManager 管理，其他模块不得包含 Entity 文件夹。业务模块只负责 Dao 数据和 Service 业务逻辑。

## 更新日志

### 最新更新
- UI Entity 统一由 UIManager 管理，其他模块不得包含 Entity 文件夹
- Service 数据持久化使用 MiniJson 支持嵌套对象和友好格式
- DataManager 通过事件机制自动注册 Service，移除扫描功能
- Service 初始化时自动触发 OnServiceInitialized 事件

## 相关文档

- [Manager Agent 指南](EssSystem/Core/EssManagers/Manager/Agent.md)
- [DataManager Agent 指南](EssSystem/Core/EssManagers/DataManager/Agent.md)
- [ResourceManager Agent 指南](EssSystem/Core/EssManagers/ResourceManager/Agent.md)
- [UIManager Agent 指南](EssSystem/Core/EssManagers/UIManager/Agent.md)
