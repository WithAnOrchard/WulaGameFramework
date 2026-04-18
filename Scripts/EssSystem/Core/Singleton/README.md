# Singleton机制文档

## 概述

Singleton机制是框架的核心模式之一，提供全局唯一实例管理，支持Unity MonoBehaviour和普通C#类两种实现。

## 核心组件

### 1. SingletonMono<T>
```csharp
public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
```
- **作用**: Unity MonoBehaviour单例基类
- **特性**: 自动创建实例，DontDestroyOnLoad，线程安全

### 2. SingletonNormal<T>
```csharp
public abstract class SingletonNormal<T> where T : class, new()
```
- **作用**: 普通C#类单例基类
- **特性**: 延迟初始化，线程安全，简单高效

## 使用方法

### 1. Unity MonoBehaviour单例
```csharp
public class GameManager : SingletonMono<GameManager>
{
    [SerializeField] private int _score = 0;
    
    public int Score => _score;
    
    public void AddScore(int points)
    {
        _score += points;
        Log($"分数增加: {points}, 当前分数: {_score}");
    }
    
    protected override void Awake()
    {
        base.Awake();
        // 初始化游戏逻辑
        InitializeGame();
    }
    
    private void InitializeGame()
    {
        _score = 0;
        Log("游戏初始化完成");
    }
}

// 使用方式
var gameManager = GameManager.Instance;
gameManager.AddScore(100);
int currentScore = gameManager.Score;
```

### 2. 普通C#类单例
```csharp
public class ConfigManager : SingletonNormal<ConfigManager>
{
    private Dictionary<string, object> _config = new Dictionary<string, object>();
    
    public void SetConfig(string key, object value)
    {
        _config[key] = value;
    }
    
    public T GetConfig<T>(string key, T defaultValue = default)
    {
        if (_config.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return defaultValue;
    }
}

// 使用方式
var configManager = ConfigManager.Instance;
configManager.SetConfig("MaxPlayers", 4);
int maxPlayers = configManager.GetConfig<int>("MaxPlayers");
```

## 实现原理

### 1. SingletonMono<T>实现
```csharp
public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static readonly object _lock = new object();
    private static bool _applicationIsQuitting = false;
    
    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[{typeof(T)}] 实例已在应用程序退出时销毁，不会返回。");
                return null;
            }
            
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = (T)FindObjectOfType(typeof(T));
                    
                    if (FindObjectsOfType(typeof(T)).Length > 1)
                    {
                        Debug.LogError($"[{typeof(T)}] 场景中存在多个单例实例！");
                        return _instance;
                    }
                    
                    if (_instance == null)
                    {
                        GameObject singleton = new GameObject();
                        _instance = singleton.AddComponent<T>();
                        singleton.name = $"(singleton) {typeof(T)}";
                        
                        DontDestroyOnLoad(singleton);
                        Debug.Log($"[{typeof(T)}] 创建单例实例");
                    }
                }
                
                return _instance;
            }
        }
    }
    
    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
}
```

### 2. SingletonNormal<T>实现
```csharp
public abstract class SingletonNormal<T> where T : class, new()
{
    private static T _instance;
    private static readonly object _lock = new object();
    
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new T();
                    }
                }
            }
            return _instance;
        }
    }
}
```

## 生命周期管理

### 1. SingletonMono生命周期
```csharp
public class MyMonoSingleton : SingletonMono<MyMonoSingleton>
{
    protected override void Awake()
    {
        base.Awake();
        // 单例创建时调用
        Debug.Log("MonoSingleton创建");
    }
    
    private void Start()
    {
        // Unity Start阶段
        Debug.Log("MonoSingleton开始");
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 单例销毁时调用
        Debug.Log("MonoSingleton销毁");
    }
    
    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        // 应用退出时调用
        Debug.Log("应用退出");
    }
}
```

### 2. SingletonNormal生命周期
```csharp
public class MyNormalSingleton : SingletonNormal<MyNormalSingleton>
{
    private bool _initialized = false;
    
    public void Initialize()
    {
        if (!_initialized)
        {
            // 初始化逻辑
            _initialized = true;
            Debug.Log("NormalSingleton初始化");
        }
    }
    
    ~MyNormalSingleton()
    {
        // 析构函数（不推荐依赖）
        Debug.Log("NormalSingleton析构");
    }
}
```

## 高级特性

### 1. 延迟初始化
```csharp
public class LazySingleton : SingletonNormal<LazySingleton>
{
    private HeavyResource _heavyResource;
    private bool _resourceLoaded = false;
    
    public HeavyResource HeavyResource
    {
        get
        {
            if (!_resourceLoaded)
            {
                LoadHeavyResource();
                _resourceLoaded = true;
            }
            return _heavyResource;
        }
    }
    
    private void LoadHeavyResource()
    {
        // 延迟加载重型资源
        _heavyResource = new HeavyResource();
        Debug.Log("重型资源延迟加载完成");
    }
}
```

### 2. 条件单例
```csharp
public class ConditionalSingleton : SingletonNormal<ConditionalSingleton>
{
    private bool _enabled = true;
    
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!_enabled)
            {
                // 禁用时清理资源
                Cleanup();
            }
        }
    }
    
    public void DoWork()
    {
        if (!_enabled)
        {
            Debug.LogWarning("单例已禁用");
            return;
        }
        
        // 正常工作逻辑
    }
    
    private void Cleanup()
    {
        // 清理资源
    }
}
```

### 3. 可重置单例
```csharp
public class ResettableSingleton : SingletonNormal<ResettableSingleton>
{
    private int _counter = 0;
    
    public int Counter => _counter;
    
    public void Increment()
    {
        _counter++;
    }
    
    public static void Reset()
    {
        // 重置单例实例
        var instance = Instance;
        instance._counter = 0;
        Debug.Log("单例已重置");
    }
    
    public static void Dispose()
    {
        // 销毁单例实例（仅限Normal类型）
        // 注意：这会破坏单例模式，仅在特殊情况下使用
    }
}
```

## 最佳实践

### 1. 单例设计原则
```csharp
// ✅ 好的设计
public class ServiceManager : SingletonNormal<ServiceManager>
{
    private Dictionary<Type, object> _services = new Dictionary<Type, object>();
    
    private ServiceManager()
    {
        // 私有构造函数防止外部实例化
        InitializeServices();
    }
    
    private void InitializeServices()
    {
        // 初始化服务
    }
    
    public void RegisterService<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }
    
    public T GetService<T>() where T : class
    {
        return _services[typeof(T)] as T;
    }
}

// ❌ 避免的设计
public class BadSingleton : SingletonNormal<BadSingleton>
{
    public BadSingleton()
    {
        // 不要在构造函数中访问其他单例
        var other = OtherSingleton.Instance; // 可能导致循环依赖
    }
}
```

### 2. 线程安全考虑
```csharp
public class ThreadSafeSingleton : SingletonNormal<ThreadSafeSingleton>
{
    private readonly object _dataLock = new object();
    private List<string> _data = new List<string>();
    
    public void AddData(string item)
    {
        lock (_dataLock)
        {
            _data.Add(item);
        }
    }
    
    public List<string> GetData()
    {
        lock (_dataLock)
        {
            return new List<string>(_data); // 返回副本
        }
    }
}
```

### 3. 内存管理
```csharp
public class MemoryAwareSingleton : SingletonNormal<MemoryAwareSingleton>
{
    private WeakReference[] _cache;
    private int _cacheSize = 100;
    
    public MemoryAwareSingleton()
    {
        _cache = new WeakReference[_cacheSize];
    }
    
    public void CacheData(int index, object data)
    {
        if (index >= 0 && index < _cacheSize)
        {
            _cache[index] = new WeakReference(data);
        }
    }
    
    public object GetCachedData(int index)
    {
        if (index >= 0 && index < _cacheSize)
        {
            return _cache[index]?.Target;
        }
        return null;
    }
}
```

## 性能优化

### 1. 避免频繁访问
```csharp
public class OptimizedUsage : MonoBehaviour
{
    private void Start()
    {
        // ✅ 缓存实例引用
        var gameManager = GameManager.Instance;
        var configManager = ConfigManager.Instance;
        
        for (int i = 0; i < 1000; i++)
        {
            gameManager.AddScore(1);
            var setting = configManager.GetConfig<string>("Setting");
        }
        
        // ❌ 避免在循环中频繁访问
        for (int i = 0; i < 1000; i++)
        {
            GameManager.Instance.AddScore(1); // 每次都要查找实例
            var setting = ConfigManager.Instance.GetConfig<string>("Setting");
        }
    }
}
```

### 2. 合理使用Mono vs Normal
```csharp
// ✅ 需要Unity生命周期时使用MonoSingleton
public class AudioManager : SingletonMono<AudioManager>
{
    private AudioSource _audioSource;
    
    protected override void Awake()
    {
        base.Awake();
        _audioSource = GetComponent<AudioSource>();
    }
    
    public void PlaySound(AudioClip clip)
    {
        _audioSource.PlayOneShot(clip);
    }
}

// ✅ 纯数据管理时使用NormalSingleton
public class DataManager : SingletonNormal<DataManager>
{
    private Dictionary<string, object> _data = new Dictionary<string, object>();
    
    public void SetData(string key, object value)
    {
        _data[key] = value;
    }
    
    public T GetData<T>(string key)
    {
        return (T)_data[key];
    }
}
```

## 调试支持

### 1. 单例状态检查
```csharp
public static class SingletonDebugger
{
    [ContextMenu("Show All Singletons")]
    public static void ShowAllSingletons()
    {
        Debug.Log("=== 当前单例状态 ===");
        
        // 检查MonoSingletons
        var monoSingletons = FindObjectsOfType<MonoBehaviour>()
            .Where(mb => mb.GetType().BaseType?.IsGenericType == true)
            .Where(mb => mb.GetType().BaseType?.GetGenericTypeDefinition() == typeof(SingletonMono<>))
            .ToList();
            
        foreach (var singleton in monoSingletons)
        {
            Debug.Log($"MonoSingleton: {singleton.GetType().Name} - {singleton.gameObject.name}");
        }
        
        // 注意：NormalSingletons无法通过反射直接检查
    }
}
```

### 2. 内存泄漏检测
```csharp
public class SingletonProfiler : MonoBehaviour
{
    [ContextMenu("Profile Singleton Memory")]
    public void ProfileSingletonMemory()
    {
        var beforeMemory = GC.GetTotalMemory(false);
        
        // 强制创建所有单例
        var gameManager = GameManager.Instance;
        var configManager = ConfigManager.Instance;
        
        var afterMemory = GC.GetTotalMemory(false);
        var memoryUsed = afterMemory - beforeMemory;
        
        Debug.Log($"单例内存使用: {memoryUsed} bytes");
    }
}
```

## 常见问题

### 1. 循环依赖
```csharp
// ❌ 循环依赖
public class ServiceA : SingletonNormal<ServiceA>
{
    public void Initialize()
    {
        var serviceB = ServiceB.Instance; // ServiceB也依赖ServiceA
    }
}

public class ServiceB : SingletonNormal<ServiceB>
{
    public void Initialize()
    {
        var serviceA = ServiceA.Instance; // 循环依赖
    }
}

// ✅ 解决方案：使用依赖注入或延迟初始化
public class ServiceA : SingletonNormal<ServiceA>
{
    public void Initialize()
    {
        // 延迟初始化或通过事件系统通信
    }
}
```

### 2. 场景切换问题
```csharp
// ✅ 正确处理场景切换
public class ScenePersistentManager : SingletonMono<ScenePersistentManager>
{
    protected override void Awake()
    {
        base.Awake();
        
        // 确保在场景切换时不被销毁
        DontDestroyOnLoad(gameObject);
    }
    
    private void OnLevelWasLoaded(int level)
    {
        // 场景加载后的处理
        Debug.Log($"场景 {level} 加载完成");
    }
}
```

## 注意事项

1. **线程安全**: 两种单例都是线程安全的，但单例内部数据需要额外保护
2. **内存泄漏**: 单例生命周期与应用程序相同，注意及时清理资源
3. **循环依赖**: 避免单例之间的循环依赖
4. **测试困难**: 单例模式可能使单元测试复杂化，考虑使用依赖注入
5. **过度使用**: 不要滥用单例模式，仅在真正需要全局唯一实例时使用
