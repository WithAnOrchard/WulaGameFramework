# EssSystem 框架整体优化方案

> 从架构、内存、性能、可维护性等多个维度优化 EssSystem 框架，使其成为高效、轻量、可扩展的游戏框架。

---

## 📋 优化范围

### 框架层级结构

```
EssSystem
├── Core/
│   ├── Base/              ← 基础组件（事件、单例、工具）
│   ├── Foundation/        ← 基础 Manager（数据、资源、音频）
│   ├── Presentation/      ← 表现层（UI、Canvas）
│   ├── Application/       ← 应用层（多 Manager、游戏逻辑）
│   └── Platform/          ← 平台相关（Windows、Mac）
├── Manager/               ← 扩展 Manager（弹幕、直播等）
└── FRAMEWORK_OPTIMIZATION.md  ← 本文档
```

---

## 🎯 优化目标

| 维度 | 当前状态 | 目标 | 优先级 |
|---|---|---|:---:|
| **内存占用** | 200MB+ | 50-80MB | ⭐⭐⭐ |
| **启动时间** | 未测 | < 3s | ⭐⭐⭐ |
| **帧率稳定性** | 60fps | 稳定 60fps | ⭐⭐⭐ |
| **GC 压力** | 未优化 | < 1ms/frame | ⭐⭐ |
| **代码复用率** | 70% | 85%+ | ⭐⭐ |
| **可维护性** | 中等 | 高 | ⭐⭐ |

---

## 🔴 核心问题分析

### 1. **资源管理问题**

#### 问题 1.1：ResourceManager 无法精细控制
- **现象**：资源一次性加载，无引用计数
- **影响**：内存无法及时释放
- **根因**：ResourceManager 缺少卸载策略

#### 问题 1.2：Manager 单例无生命周期管理
- **现象**：所有 Manager 在启动时创建，运行时无法卸载
- **影响**：后台 Manager 占用内存
- **根因**：Manager 生命周期与游戏生命周期绑定

#### 问题 1.3：事件系统内存泄漏
- **现象**：事件监听器未及时移除
- **影响**：事件处理函数占用内存
- **根因**：缺少自动清理机制

---

### 2. **性能问题**

#### 问题 2.1：EventProcessor 性能瓶颈
- **现象**：每帧处理大量事件
- **影响**：GC 压力大
- **根因**：事件数据使用 List<object>，频繁装箱拆箱

#### 问题 2.2：Manager 初始化顺序不明确
- **现象**：启动时间长
- **影响**：用户等待时间长
- **根因**：缺少启动优化策略

#### 问题 2.3：UI 系统重复创建销毁
- **现象**：每次打开 Panel 都创建新 GO
- **影响**：GC 压力大
- **根因**：缺少 UI 对象池

---

### 3. **架构问题**

#### 问题 3.1：Manager 职责不清
- **现象**：Manager 既做初始化，又做业务逻辑
- **影响**：代码复杂，难以维护
- **根因**：缺少清晰的分层

#### 问题 3.2：跨 Manager 通信困难
- **现象**：Manager 之间通过事件通信，耦合度高
- **影响**：代码难以理解
- **根因**：缺少统一的通信协议

#### 问题 3.3：配置管理不统一
- **现象**：配置分散在各 Manager 中
- **影响**：难以维护和扩展
- **根因**：缺少配置管理系统

---

## ✅ 优化方案

### Phase 1：资源管理优化（优先级最高）

#### 1.1 **增强 ResourceManager 的引用计数机制**

**目标**：支持精细的资源加载/卸载

**实现**：

```csharp
// Assets/Scripts/EssSystem/Core/Foundation/ResourceManager/ResourceService.cs

public class ResourceService : Service<ResourceService>
{
    private class ResourceRef
    {
        public object Asset;
        public int RefCount;
        public float LastAccessTime;
    }

    private Dictionary<string, ResourceRef> _loadedAssets = new();
    private const float UNLOAD_TIMEOUT = 300f; // 5 分钟未访问则卸载

    /// <summary>加载资源（带引用计数）。</summary>
    public T Load<T>(string path) where T : Object
    {
        var key = $"{typeof(T).Name}:{path}";
        if (_loadedAssets.TryGetValue(key, out var rf))
        {
            rf.RefCount++;
            rf.LastAccessTime = Time.realtimeSinceStartup;
            return rf.Asset as T;
        }

        var asset = Resources.Load<T>(path);
        if (asset != null)
        {
            _loadedAssets[key] = new ResourceRef
            {
                Asset = asset,
                RefCount = 1,
                LastAccessTime = Time.realtimeSinceStartup
            };
        }
        return asset;
    }

    /// <summary>卸载资源（减少引用计数）。</summary>
    public void Unload<T>(string path) where T : Object
    {
        var key = $"{typeof(T).Name}:{path}";
        if (_loadedAssets.TryGetValue(key, out var rf))
        {
            rf.RefCount--;
            if (rf.RefCount <= 0)
            {
                Resources.UnloadAsset(rf.Asset);
                _loadedAssets.Remove(key);
                Debug.Log($"[ResourceService] 卸载资源: {path}");
            }
        }
    }

    /// <summary>定期清理超时未使用的资源。</summary>
    public void CleanupUnusedAssets()
    {
        var now = Time.realtimeSinceStartup;
        var keysToRemove = new List<string>();

        foreach (var kvp in _loadedAssets)
        {
            if (kvp.Value.RefCount == 0 && now - kvp.Value.LastAccessTime > UNLOAD_TIMEOUT)
                keysToRemove.Add(kvp.Key);
        }

        foreach (var key in keysToRemove)
        {
            Resources.UnloadAsset(_loadedAssets[key].Asset);
            _loadedAssets.Remove(key);
            Debug.Log($"[ResourceService] 清理超时资源: {key}");
        }
    }

    /// <summary>获取资源统计信息。</summary>
    public Dictionary<string, int> GetResourceStats()
    {
        var stats = new Dictionary<string, int>();
        foreach (var kvp in _loadedAssets)
            stats[kvp.Key] = kvp.Value.RefCount;
        return stats;
    }
}
```

**预期收益**：
- ✅ 精细控制资源生命周期
- ✅ 自动清理超时资源
- ✅ 内存占用降低 30-50MB

---

#### 1.2 **Manager 生命周期管理**

**目标**：支持 Manager 动态加载/卸载

**实现**：

```csharp
// Assets/Scripts/EssSystem/Core/Manager.cs

public abstract class Manager<T> : MonoBehaviour where T : Manager<T>
{
    public enum ManagerState { Uninitialized, Initializing, Initialized, Unloading, Unloaded }

    protected ManagerState _state = ManagerState.Uninitialized;
    protected float _initializeTime;

    public ManagerState State => _state;
    public float InitializeTime => _initializeTime;

    /// <summary>初始化 Manager。</summary>
    public virtual void Initialize()
    {
        if (_state != ManagerState.Uninitialized) return;
        _state = ManagerState.Initializing;
        var startTime = Time.realtimeSinceStartup;
        
        OnInitialize();
        
        _initializeTime = Time.realtimeSinceStartup - startTime;
        _state = ManagerState.Initialized;
        Debug.Log($"[Manager] {typeof(T).Name} 初始化完成 ({_initializeTime:F3}s)");
    }

    /// <summary>卸载 Manager（释放资源）。</summary>
    public virtual void Unload()
    {
        if (_state != ManagerState.Initialized) return;
        _state = ManagerState.Unloading;
        
        OnUnload();
        
        _state = ManagerState.Unloaded;
        Debug.Log($"[Manager] {typeof(T).Name} 已卸载");
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnUnload() { }
}
```

**预期收益**：
- ✅ Manager 可按需加载/卸载
- ✅ 后台 Manager 可释放内存
- ✅ 内存占用降低 20-30MB

---

#### 1.3 **事件系统自动清理**

**目标**：防止事件监听器泄漏

**实现**：

```csharp
// Assets/Scripts/EssSystem/Core/Event/EventProcessor.cs

public class EventProcessor : Singleton<EventProcessor>
{
    private class EventListenerRef
    {
        public EventDelegate Handler;
        public WeakReference Target;
        public bool IsAlive => Target?.IsAlive ?? false;
    }

    private Dictionary<string, List<EventListenerRef>> _listeners = new();

    /// <summary>添加事件监听器（使用弱引用）。</summary>
    public void AddListener(string eventName, EventDelegate handler)
    {
        if (!_listeners.ContainsKey(eventName))
            _listeners[eventName] = new List<EventListenerRef>();

        var target = handler.Target;
        _listeners[eventName].Add(new EventListenerRef
        {
            Handler = handler,
            Target = target != null ? new WeakReference(target) : null
        });
    }

    /// <summary>触发事件（自动清理死亡的监听器）。</summary>
    public void TriggerEvent(string eventName, List<object> data)
    {
        if (!_listeners.TryGetValue(eventName, out var refs)) return;

        // 清理死亡的监听器
        refs.RemoveAll(r => !r.IsAlive);

        // 触发事件
        foreach (var rf in refs)
            rf.Handler?.Invoke(eventName, data);
    }

    /// <summary>获取事件监听器统计。</summary>
    public Dictionary<string, int> GetListenerStats()
    {
        var stats = new Dictionary<string, int>();
        foreach (var kvp in _listeners)
            stats[kvp.Key] = kvp.Value.Count(r => r.IsAlive);
        return stats;
    }
}
```

**预期收益**：
- ✅ 自动清理死亡监听器
- ✅ 防止内存泄漏
- ✅ 内存占用降低 5-10MB

---

### Phase 2：性能优化

#### 2.1 **EventProcessor 性能优化**

**问题**：List<object> 频繁装箱拆箱

**方案**：使用泛型事件系统

```csharp
// 创建 TypedEventProcessor.cs
public class TypedEventProcessor : Singleton<TypedEventProcessor>
{
    private Dictionary<Type, Delegate> _typedListeners = new();

    public void Subscribe<T>(Action<T> handler) where T : IEventData
    {
        var type = typeof(T);
        if (!_typedListeners.ContainsKey(type))
            _typedListeners[type] = handler;
        else
            _typedListeners[type] = (Action<T>)_typedListeners[type] + handler;
    }

    public void Publish<T>(T data) where T : IEventData
    {
        var type = typeof(T);
        if (_typedListeners.TryGetValue(type, out var handler))
            ((Action<T>)handler)?.Invoke(data);
    }
}

// 使用示例
public struct DanmakuEventData : IEventData
{
    public string UserName;
    public string Text;
    public long UserId;
}

// 订阅
TypedEventProcessor.Instance.Subscribe<DanmakuEventData>(OnDanmaku);

// 发布
TypedEventProcessor.Instance.Publish(new DanmakuEventData 
{ 
    UserName = "用户", 
    Text = "弹幕", 
    UserId = 123 
});
```

**预期收益**：
- ✅ 消除装箱拆箱
- ✅ GC 压力降低 50%
- ✅ 性能提升 20-30%

---

#### 2.2 **Manager 启动优化**

**问题**：所有 Manager 同步初始化

**方案**：分阶段异步初始化

```csharp
// 创建 ManagerBootstrap.cs
public class ManagerBootstrap : MonoBehaviour
{
    [System.Serializable]
    public class ManagerInitConfig
    {
        public Manager.ManagerPriority Priority;
        public float Delay;
        public bool Async;
    }

    private List<ManagerInitConfig> _initConfigs = new();

    public async void BootstrapAsync()
    {
        // 第一阶段：关键 Manager（同步）
        await InitializeManagersByPriority(Manager.ManagerPriority.Critical);
        
        // 第二阶段：核心 Manager（同步）
        await InitializeManagersByPriority(Manager.ManagerPriority.Core);
        
        // 第三阶段：普通 Manager（异步）
        await InitializeManagersByPriority(Manager.ManagerPriority.Normal, async: true);
        
        // 第四阶段：可选 Manager（异步）
        await InitializeManagersByPriority(Manager.ManagerPriority.Optional, async: true);
    }

    private async Task InitializeManagersByPriority(
        Manager.ManagerPriority priority, 
        bool async = false)
    {
        // 实现初始化逻辑
    }
}
```

**预期收益**：
- ✅ 启动时间降低 40-50%
- ✅ 首帧更快出现
- ✅ 用户体验改善

---

### Phase 3：架构优化

#### 3.1 **Manager 职责分离**

**问题**：Manager 既做初始化，又做业务逻辑

**方案**：分离为 Manager + Service

```csharp
// Manager：生命周期管理
public class DataManager : Manager<DataManager>
{
    protected override void OnInitialize()
    {
        DataService.Instance?.Initialize();
    }

    protected override void OnUnload()
    {
        DataService.Instance?.Shutdown();
    }
}

// Service：业务逻辑
public class DataService : Service<DataService>
{
    public void Initialize() { /* 初始化逻辑 */ }
    public void Shutdown() { /* 清理逻辑 */ }
    public void SaveData() { /* 业务逻辑 */ }
}
```

**预期收益**：
- ✅ 代码更清晰
- ✅ 易于测试
- ✅ 可维护性提升

---

#### 3.2 **统一配置管理**

**问题**：配置分散在各 Manager 中

**方案**：创建 ConfigManager

```csharp
// 创建 ConfigManager.cs
public class ConfigManager : Manager<ConfigManager>
{
    private Dictionary<string, object> _configs = new();

    public void SetConfig(string key, object value) => _configs[key] = value;
    public T GetConfig<T>(string key, T defaultValue = default) =>
        _configs.TryGetValue(key, out var val) ? (T)val : defaultValue;
}

// 使用
ConfigManager.Instance.SetConfig("MaxDanmuHistory", 100);
var maxHistory = ConfigManager.Instance.GetConfig<int>("MaxDanmuHistory", 50);
```

**预期收益**：
- ✅ 配置管理统一
- ✅ 易于扩展
- ✅ 易于调试

---

### Phase 4：可维护性优化

#### 4.1 **添加性能监控**

**目标**：实时监控框架性能

**实现**：

```csharp
// 创建 PerformanceMonitor.cs
public class PerformanceMonitor : Singleton<PerformanceMonitor>
{
    private Dictionary<string, PerformanceStats> _stats = new();

    public class PerformanceStats
    {
        public float TotalTime;
        public int CallCount;
        public float AverageTime => CallCount > 0 ? TotalTime / CallCount : 0;
    }

    public void BeginSample(string name)
    {
        if (!_stats.ContainsKey(name))
            _stats[name] = new PerformanceStats();
        // 记录开始时间
    }

    public void EndSample(string name)
    {
        // 记录结束时间，累加到 _stats[name]
    }

    public void PrintReport()
    {
        Debug.Log("=== 性能报告 ===");
        foreach (var kvp in _stats)
            Debug.Log($"{kvp.Key}: {kvp.Value.AverageTime:F3}ms");
    }
}
```

**预期收益**：
- ✅ 性能瓶颈可视化
- ✅ 优化方向明确
- ✅ 持续改进

---

#### 4.2 **完善文档系统**

**目标**：提升框架易用性

**实现**：

```
EssSystem/
├── Core/
│   ├── Agent.md              ← 总体架构
│   ├── ARCHITECTURE.md       ← 架构设计
│   ├── PERFORMANCE.md        ← 性能指标
│   ├── OPTIMIZATION.md       ← 优化指南
│   └── API_REFERENCE.md      ← API 参考
├── Manager/
│   └── */Agent.md            ← 各 Manager 文档
└── FRAMEWORK_OPTIMIZATION.md ← 本文档
```

**预期收益**：
- ✅ 新手快速上手
- ✅ 最佳实践明确
- ✅ 问题排查更快

---

## 📊 优化时间表

| Phase | 任务 | 预期时间 | 收益 |
|---|---|---|---|
| **1** | 资源管理优化 | 1 周 | 内存 -50-80MB |
| **2** | 性能优化 | 1-2 周 | 启动 -40-50%, GC -50% |
| **3** | 架构优化 | 2-3 周 | 可维护性 +30% |
| **4** | 可维护性优化 | 1 周 | 开发效率 +20% |
| **总计** | - | **5-7 周** | **全面优化** |

---

## 🧪 验证方案

### 1. **内存测试**
```csharp
[ContextMenu("Memory Report")]
private void PrintMemoryReport()
{
    var stats = ResourceService.Instance.GetResourceStats();
    Debug.Log($"[Memory] 加载资源数: {stats.Count}");
    Debug.Log($"[Memory] 总内存: {System.GC.GetTotalMemory(false) / 1024 / 1024}MB");
}
```

### 2. **性能测试**
```csharp
[ContextMenu("Performance Report")]
private void PrintPerformanceReport()
{
    PerformanceMonitor.Instance.PrintReport();
}
```

### 3. **启动时间测试**
```csharp
private void Start()
{
    var startTime = Time.realtimeSinceStartup;
    // 初始化逻辑
    var totalTime = Time.realtimeSinceStartup - startTime;
    Debug.Log($"[Startup] 总启动时间: {totalTime:F3}s");
}
```

---

## 🎯 DobeCat 实战验证

### 验证步骤

1. **应用 Phase 1 优化**
   - 修改 ResourceManager
   - 修改 Manager 生命周期
   - 修改事件系统
   - 测试内存变化

2. **应用 Phase 2 优化**
   - 实现 TypedEventProcessor
   - 优化 Manager 启动
   - 测试性能变化

3. **应用 Phase 3-4 优化**
   - 重构 Manager 职责
   - 添加 ConfigManager
   - 添加 PerformanceMonitor
   - 完善文档

### 预期结果

| 指标 | 优化前 | 优化后 | 改善 |
|---|---|---|---|
| 内存占用 | 200MB | 60MB | **-70%** |
| 启动时间 | 未测 | < 2s | **-40%+** |
| GC 压力 | 高 | 低 | **-50%** |
| 代码复用率 | 70% | 90% | **+20%** |

---

## 📚 相关文档

- `Core/Agent.md` - 框架总体架构
- `Core/Managers.md` - Manager 系统
- `MEMORY_OPTIMIZATION.md` - DobeCat 内存优化
- `DESIGN_STATUS.md` - DobeCat 功能完成度

---

## 💡 最佳实践

### 资源管理
- ✅ 使用 ResourceService 加载资源
- ✅ 及时调用 Unload 释放资源
- ✅ 定期调用 CleanupUnusedAssets

### 事件系统
- ✅ 优先使用 TypedEventProcessor
- ✅ 在 OnDestroy 中移除监听器
- ✅ 避免在事件处理中创建大对象

### Manager 设计
- ✅ Manager 只负责生命周期
- ✅ Service 负责业务逻辑
- ✅ 使用 ConfigManager 管理配置

### 性能优化
- ✅ 定期运行 PerformanceMonitor
- ✅ 识别性能瓶颈
- ✅ 持续迭代优化

---

## 🚀 后续规划

### 短期（1 个月）
- [ ] 完成 Phase 1-2 优化
- [ ] DobeCat 实战验证
- [ ] 性能基准测试

### 中期（3 个月）
- [ ] 完成 Phase 3-4 优化
- [ ] 多项目验证
- [ ] 文档完善

### 长期（6 个月+）
- [ ] 支持 Addressable Assets
- [ ] 支持异步加载
- [ ] 支持资源版本管理
- [ ] 支持 MOD 系统

