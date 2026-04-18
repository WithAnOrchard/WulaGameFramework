# Manager机制文档

## 概述

Manager机制是框架的核心管理系统，提供统一的管理器生命周期管理和功能扩展基础。

## 核心组件

### 1. Manager<T>基类
```csharp
public abstract class Manager<T> : MonoBehaviour where T : Manager<T>
```
- **作用**: 所有管理器的基类，提供统一的初始化和生命周期管理
- **特性**: 泛型单例模式，Unity MonoBehaviour生命周期集成

### 2. ManagerAttribute
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ManagerAttribute : Attribute
```
- **作用**: 标记类为管理器
- **参数**: 初始化优先级（可选，默认为0）

## 使用方法

### 1. 创建管理器
```csharp
[Manager]
public class MyManager : Manager<MyManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
    }
}
```

### 2. 设置优先级
```csharp
[Manager(10)]  // 优先级10，数字越小越先初始化
public class HighPriorityManager : Manager<HighPriorityManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        // 高优先级初始化逻辑
    }
}
```

### 3. 访问管理器
```csharp
// 通过单例访问
var myManager = MyManager.Instance;

// 或者通过ManagerRegistry访问
var manager = ManagerRegistry.GetManager<MyManager>();
```

## 生命周期

### 1. 初始化顺序
1. **Awake()**: MonoBehaviour唤醒，注册到ManagerRegistry
2. **Initialize()**: 管理器初始化（按优先级顺序）
3. **Start()**: Unity开始阶段（可选重写）

### 2. 生命周期方法
```csharp
public abstract class Manager<T> : MonoBehaviour where T : Manager<T>
{
    protected virtual void Awake()
    {
        // 注册到ManagerRegistry
        RegisterManager();
    }
    
    protected virtual void Start()
    {
        base.Start();
        // Unity开始逻辑
    }
    
    protected virtual void OnDestroy()
    {
        // 清理逻辑
        UnregisterManager();
    }
    
    public abstract void Initialize();
}
```

## 内置管理器

### 1. EventManager
- **优先级**: -10（最高优先级）
- **作用**: 事件系统管理
- **功能**: 事件注册、触发、处理器管理

### 2. DataManager
- **优先级**: -5
- **作用**: 数据管理
- **功能**: Service数据存储、序列化、持久化

### 3. ServiceManager
- **优先级**: 0
- **作用**: Service管理
- **功能**: Service实例管理、生命周期控制

## ManagerRegistry

### 1. 功能
```csharp
public static class ManagerRegistry
{
    // 注册管理器
    public static void RegisterManager<T>(T manager) where T : Manager<T>;
    
    // 获取管理器
    public static T GetManager<T>() where T : Manager<T>;
    
    // 获取所有管理器
    public static Dictionary<Type, ManagerBase> GetAllManagers();
    
    // 初始化所有管理器
    public static void InitializeAllManagers();
}
```

### 2. 使用示例
```csharp
// 获取特定管理器
var eventManager = ManagerRegistry.GetManager<EventManager>();
var dataManager = ManagerRegistry.GetManager<DataManager>();

// 获取所有管理器
var allManagers = ManagerRegistry.GetAllManagers();
foreach (var kvp in allManagers)
{
    Debug.Log($"Manager: {kvp.Key.Name}, Instance: {kvp.Value}");
}
```

## 最佳实践

### 1. 管理器设计原则
```csharp
[Manager(5)]  // 设置合适的优先级
public class UIManager : Manager<UIManager>
{
    // 私有字段
    private UIService _uiService;
    
    // 公共属性
    public UIService UIService => _uiService;
    
    protected override void Initialize()
    {
        base.Initialize();
        
        // 初始化依赖
        _uiService = UIService.Instance;
        
        // 注册事件处理器
        RegisterEventHandlers();
        
        // 初始化子系统
        InitializeSubSystems();
    }
    
    private void RegisterEventHandlers()
    {
        // 注册事件处理
    }
    
    private void InitializeSubSystems()
    {
        // 初始化子系统
    }
}
```

### 2. 依赖管理
```csharp
public class UIManager : Manager<UIManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        
        // 确保依赖管理器已初始化
        var eventManager = EventManager.Instance;
        var dataManager = DataManager.Instance;
        
        // 使用依赖
        eventManager.TriggerEvent("UIInitialized", new List<object>());
    }
}
```

### 3. 配置管理
```csharp
[Manager]
public class ConfigManager : Manager<ConfigManager>
{
    [SerializeField] private bool _enableLogging = true;
    [SerializeField] private float _updateInterval = 1.0f;
    
    protected override void Initialize()
    {
        base.Initialize();
        
        // 应用配置
        if (_enableLogging)
        {
            EnableLogging();
        }
        
        // 启动定时器
        StartCoroutine(UpdateRoutine());
    }
    
    private IEnumerator UpdateRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_updateInterval);
            // 定期更新逻辑
        }
    }
}
```

## 高级特性

### 1. 条件初始化
```csharp
[Manager]
public class ConditionalManager : Manager<ConditionalManager>
{
    [SerializeField] private bool _enableFeature = true;
    
    protected override void Initialize()
    {
        base.Initialize();
        
        if (!_enableFeature)
        {
            Log("功能已禁用，跳过初始化");
            return;
        }
        
        // 正常初始化逻辑
    }
}
```

### 2. 异步初始化
```csharp
[Manager]
public class AsyncManager : Manager<AsyncManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        
        // 异步初始化
        StartCoroutine(AsyncInitialize());
    }
    
    private IEnumerator AsyncInitialize()
    {
        // 异步加载资源
        var operation = Resources.LoadAsync<GameObject>("MyPrefab");
        yield return operation;
        
        if (operation.asset != null)
        {
            // 初始化完成
            Log("异步初始化完成");
        }
    }
}
```

### 3. 事件驱动初始化
```csharp
[Manager(10)]
public class EventDrivenManager : Manager<EventDrivenManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        
        // 注册事件处理器
        var eventManager = EventManager.Instance;
        eventManager.TriggerEvent("RegisterEventHandlers", new List<object>
        {
            this.GetType().Name
        });
    }
}
```

## 调试支持

### 1. 管理器状态检查
```csharp
// 检查管理器是否已初始化
bool isInitialized = ManagerRegistry.IsManagerInitialized<UIManager>();

// 获取管理器初始化顺序
var initOrder = ManagerRegistry.GetInitializationOrder();
```

### 2. 日志记录
```csharp
public class MyManager : Manager<MyManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        Log("管理器初始化开始", Color.yellow);
        
        try
        {
            // 初始化逻辑
            Log("管理器初始化完成", Color.green);
        }
        catch (Exception ex)
        {
            LogError($"管理器初始化失败: {ex.Message}");
        }
    }
}
```

## 性能优化

### 1. 延迟初始化
```csharp
[Manager]
public class LazyManager : Manager<LazyManager>
{
    private bool _initialized = false;
    
    public void EnsureInitialized()
    {
        if (!_initialized)
        {
            InitializeLazyComponents();
            _initialized = true;
        }
    }
    
    private void InitializeLazyComponents()
    {
        // 延迟初始化逻辑
    }
}
```

### 2. 对象池管理
```csharp
[Manager]
public class PoolManager : Manager<PoolManager>
{
    private Dictionary<Type, object> _pools = new Dictionary<Type, object>();
    
    protected override void Initialize()
    {
        base.Initialize();
        InitializePools();
    }
    
    private void InitializePools()
    {
        // 初始化对象池
    }
    
    public T GetFromPool<T>() where T : class, new()
    {
        // 从池中获取对象
    }
    
    public void ReturnToPool<T>(T obj) where T : class
    {
        // 返回对象到池中
    }
}
```

## 注意事项

1. **优先级设置**: 确保依赖关系正确的优先级设置
2. **循环依赖**: 避免管理器之间的循环依赖
3. **资源清理**: 在OnDestroy中正确清理资源
4. **线程安全**: 管理器主要在主线程运行，需要注意线程安全
5. **单例模式**: 每个管理器类型只有一个实例
