# Singleton 机制 Agent 指南

## 概述

Singleton 机制提供两种单例模式实现：`SingletonNormal<T>` 用于普通类（Service基类），`SingletonMono<T>` 用于 Unity MonoBehaviour（Manager基类）。本指南面向 AI Agent，说明如何使用单例模式管理全局唯一实例。

## 架构定位

Singleton 是 EssSystem 框架的基础单例模式实现，为 Manager<T> 和 Service<T> 提供单例支持：
- Manager<T> 继承自 SingletonMono<T>
- Service<T> 继承自 SingletonNormal<T>

## 核心组件

### 1. SingletonNormal<T>
```csharp
public class SingletonNormal<T> where T : class, new()
```

**用途**: 普通类的单例模式（非 MonoBehaviour）

**特性**:
- 线程安全（使用 lock）
- 延迟初始化
- 支持 IDisposable 接口
- 提供日志功能

### 2. SingletonMono<T>
```csharp
public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
```

**用途**: Unity MonoBehaviour 的单例模式

**特性**:
- 自动创建 GameObject（如果不存在）
- DontDestroyOnLoad 保护
- 场景切换时保持实例
- 应用退出时自动清理
- 多实例检测和警告

## 使用方法

### 1. 使用 SingletonNormal（普通类）

```csharp
public class MyService : SingletonNormal<MyService>
{
    private string _data;

    protected override void Initialize()
    {
        _data = "Initialized";
    }

    public void DoSomething()
    {
        Log("执行操作");
    }
}

// 访问方式
MyService.Instance.DoSomething();
```

### 2. 使用 SingletonMono（MonoBehaviour）

```csharp
public class MyManager : SingletonMono<MyManager>
{
    protected override void Awake()
    {
        base.Awake();
        // Awake 逻辑
    }

    protected override void Initialize()
    {
        // 初始化逻辑
        Log("初始化完成", Color.green);
    }

    public void DoSomething()
    {
        Log("执行操作");
    }
}

// 访问方式
MyManager.Instance.DoSomething();
```

## 核心属性和方法

### SingletonNormal 属性
```csharp
// 获取实例（自动创建）
MyService.Instance

// 检查实例是否存在
MyService.HasInstance

// 获取实例（不创建新实例）
MyService.TryGetInstance()

// 销毁实例
MyService.DestroyInstance()
```

### SingletonMono 属性
```csharp
// 获取实例（自动创建）
MyManager.Instance

// 检查实例是否存在
MyManager.HasInstance

// 获取实例（不创建新实例）
MyManager.TryGetInstance()
```

### 日志方法
```csharp
// 带颜色的日志
Log("消息内容", Color.green);

// 警告日志
LogWarning("警告消息");

// 错误日志
LogError("错误消息");
```

## 生命周期

### SingletonNormal 生命周期
1. **首次访问**: 调用 `Instance` 时创建实例
2. **构造函数**: 执行类构造函数
3. **Initialize**: 调用 `Initialize()` 方法
4. **使用**: 正常使用实例
5. **销毁**: 调用 `DestroyInstance()` 时销毁

### SingletonMono 生命周期
1. **Awake**: Unity Awake 方法，处理单例创建和重复实例销毁
2. **Initialize**: 调用 `Initialize()` 方法
3. **Start**: Unity Start 方法（子类可重写）
4. **Update**: 每帧调用（子类可重写）
5. **OnApplicationQuit**: 应用退出时设置标志
6. **OnDestroy**: 清理静态单例引用

## 使用示例

### 示例 1: 数据服务（SingletonNormal）

```csharp
public class DataService : SingletonNormal<DataService>
{
    private Dictionary<string, object> _cache;

    protected override void Initialize()
    {
        base.Initialize();
        _cache = new Dictionary<string, object>();
        Log("数据服务初始化完成", Color.green);
    }

    public void SetData(string key, object value)
    {
        _cache[key] = value;
    }

    public T GetData<T>(string key)
    {
        if (_cache.TryGetValue(key, out var value))
        {
            return (T)value;
        }
        return default(T);
    }
}

// 使用
DataService.Instance.SetData("player_name", "John");
var name = DataService.Instance.GetData<string>("player_name");
```

### 示例 2: UI 管理器（SingletonMono）

```csharp
public class UIManager : SingletonMono<UIManager>
{
    private Canvas _mainCanvas;

    protected override void Awake()
    {
        base.Awake();
        _mainCanvas = GetComponent<Canvas>();
    }

    protected override void Initialize()
    {
        base.Initialize();
        SetupUI();
        Log("UI 管理器初始化完成", Color.green);
    }

    private void SetupUI()
    {
        // UI 初始化逻辑
    }

    public void ShowPanel(string panelName)
    {
        Log($"显示面板: {panelName}", Color.blue);
    }
}

// 使用
UIManager.Instance.ShowPanel("MainMenu");
```

### 示例 3: 配置服务（SingletonNormal）

```csharp
public class ConfigService : SingletonNormal<ConfigService>
{
    private Dictionary<string, string> _config;

    protected override void Initialize()
    {
        base.Initialize();
        LoadConfig();
    }

    private void LoadConfig()
    {
        _config = new Dictionary<string, string>
        {
            ["server_url"] = "https://api.example.com",
            ["timeout"] = "30"
        };
        Log("配置加载完成", Color.green);
    }

    public string GetConfig(string key)
    {
        return _config.TryGetValue(key, out var value) ? value : null;
    }
}

// 使用
var serverUrl = ConfigService.Instance.GetConfig("server_url");
```

### 示例 4: 音频管理器（SingletonMono）

```csharp
public class AudioManager : SingletonMono<AudioManager>
{
    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    protected override void Awake()
    {
        base.Awake();
        CreateAudioSources();
    }

    private void CreateAudioSources()
    {
        _musicSource = gameObject.AddComponent<AudioSource>();
        _sfxSource = gameObject.AddComponent<AudioSource>();
    }

    public void PlayMusic(AudioClip clip)
    {
        _musicSource.clip = clip;
        _musicSource.Play();
        Log($"播放音乐: {clip.name}", Color.blue);
    }

    public void PlaySFX(AudioClip clip)
    {
        _sfxSource.PlayOneShot(clip);
        Log($"播放音效: {clip.name}", Color.blue);
    }
}

// 使用
AudioManager.Instance.PlayMusic(myMusicClip);
AudioManager.Instance.PlaySFX(mySFXClip);
```

## 最佳实践

### 1. 选择合适的单例类型
- **需要 Unity 生命周期** → 使用 `SingletonMono<T>`
- **纯数据管理或业务逻辑** → 使用 `SingletonNormal<T>`
- **需要与场景交互** → 使用 `SingletonMono<T>`
- **不需要 Unity 依赖** → 使用 `SingletonNormal<T>`

### 2. 初始化顺序
```csharp
// SingletonNormal - 在首次访问时初始化
ConfigService.Instance.GetConfig("key");  // 第一次访问时初始化

// SingletonMono - 在 Awake 时初始化
// 确保 GameObject 存在于场景中
```

### 3. 实例检查
```csharp
// 检查实例是否存在
if (MyService.HasInstance)
{
    MyService.Instance.DoSomething();
}

// 安全获取实例
var instance = MyService.TryGetInstance();
if (instance != null)
{
    instance.DoSomething();
}
```

### 4. 资源清理
```csharp
// SingletonNormal - 实现 IDisposable
public class MyService : SingletonNormal<MyService>, IDisposable
{
    private IDisposable _resource;

    public void Dispose()
    {
        _resource?.Dispose();
    }
}

// SingletonMono - 在 OnDestroy 中清理
protected override void OnDestroy()
{
    base.OnDestroy();
    // 清理资源
}
```

### 5. 线程安全
- `SingletonNormal<T>` 使用 lock 保证线程安全
- `SingletonMono<T>` 在主线程运行，Unity 保证线程安全
- 不要在多线程环境中直接访问 SingletonMono 实例

## 性能优化

### 已实施的优化

1. **线程安全**: SingletonNormal 使用 lock 机制保证线程安全
2. **延迟初始化**: SingletonNormal 在首次访问时才创建实例
3. **DontDestroyOnLoad**: SingletonMono 自动设置 DontDestroyOnLoad，避免场景切换时重复创建
4. **实例检测**: SingletonMono 检测场景中的多个实例并输出警告

## 注意事项

1. **MonoBehaviour 场景要求**: SingletonMono 需要场景中有对应的 GameObject，如果没有会自动创建
2. **DontDestroyOnLoad**: SingletonMono 实例会自动设置 DontDestroyOnLoad，场景切换时不会销毁
3. **多实例警告**: SingletonMono 会检测场景中的多个实例并输出错误日志
4. **应用退出保护**: SingletonMono 在应用退出后不会再次创建实例
5. **静态引用清理**: SingletonMono 在 OnDestroy 时会清理静态引用，避免内存泄漏
6. **构造函数访问**: SingletonNormal 的构造函数应该是 protected 或 private
7. **IDisposable 支持**: SingletonNormal 支持 IDisposable，在销毁时会自动调用 Dispose
8. **初始化时机**: SingletonNormal 在首次访问 Instance 时初始化，SingletonMono 在 Awake 时初始化
9. **架构规范**: SingletonNormal 用于 Service，SingletonMono 用于 Manager，不要混用

## 常见问题

### Q: SingletonNormal 和 SingletonMono 有什么区别？
A:
- **SingletonNormal**: 用于普通类，线程安全，延迟初始化
- **SingletonMono**: 用于 MonoBehaviour，自动创建 GameObject，DontDestroyOnLoad 保护

### Q: 如何选择使用哪种单例？
A:
- 需要 Unity 生命周期（Update, FixedUpdate 等）→ SingletonMono
- 纯数据管理和业务逻辑 → SingletonNormal
- 需要与 Unity 场景交互 → SingletonMono
- 不需要 Unity 依赖 → SingletonNormal

### Q: SingletonMono 会自动创建 GameObject 吗？
A: 会。如果场景中没有对应的 GameObject，SingletonMono 会自动创建一个并添加组件。

### Q: 如何在场景切换时保持单例？
A: SingletonMono 自动使用 DontDestroyOnLoad，场景切换时实例不会销毁。

### Q: SingletonNormal 是线程安全的吗？
A: 是的，使用 lock 机制保证线程安全。

### Q: 如何安全地获取单例实例？
A: 使用 `HasInstance` 检查或 `TryGetInstance()` 获取：
```csharp
if (MyService.HasInstance)
{
    MyService.Instance.DoSomething();
}

var instance = MyService.TryGetInstance();
if (instance != null)
{
    instance.DoSomething();
}
```

### Q: 应用退出后还能访问单例吗？
A: SingletonMono 在应用退出后会返回 null 并输出警告。SingletonNormal 可以继续使用。

### Q: 如何销毁 SingletonNormal 实例？
A: 调用 `DestroyInstance()` 方法：
```csharp
MyService.DestroyInstance();
```

### Q: SingletonMono 会在场景切换时重复创建吗？
A: 不会。SingletonMono 使用 DontDestroyOnLoad 保护，场景切换时实例保持不变。

### Q: 如何处理单例的依赖关系？
A: 通过初始化顺序控制或事件系统解耦。对于 SingletonMono，使用 ManagerAttribute 控制优先级。
