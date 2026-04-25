# Manager 机制 Agent 指南

## 概述

Manager 机制是 EssSystem 的核心管理器系统，提供统一的 MonoBehaviour 管理器基类和 Service 服务基类。本指南面向 AI Agent，说明如何使用 Manager 和 Service 构建游戏系统。

## 架构规范

### 核心通信规则
1. **Manager 只能调用本地 Service**: Manager 不能直接访问其他 Manager 的 Service，只能访问自己创建的 Service
2. **只能通过 Event 触发其他 Manager 的 Service 方法**: Manager 调用其他 Manager 的 Service 方法必须通过 Event 系统触发，但可以直接调用自己创建的本地 Service
3. **Service 方法自动注册为 Event**: Service 的公开方法必须标记 `[Event]` 特性，自动注册为可调用的 Event
4. **Manager 方法自动注册为 Event**: Manager 的公开方法必须标记 `[Event]` 或 `[EventListener]` 特性，参与事件系统
5. **Manager 只能调用本地 Service 和 Event**: Manager 内部只能：
   - 直接调用自己创建的本地 Service
   - 通过 EventManager 触发其他 Event
   - 不能直接访问其他 Manager 或其他 Manager 的 Service

### 文件组织规则
6. **数据类必须放在 Dao 文件夹**: 所有数据类（Data Access Object）必须放在各自包下的 `Dao` 文件夹内
7. **GameObject 必须放在 Entity 文件夹**: 所有 Unity GameObject 相关的实体类必须放在各自包下的 `Entity` 文件夹内

## 核心组件

### 1. Manager<T>
```csharp
public abstract class Manager<T> : SingletonMono<T> where T : MonoBehaviour
```

**用途**: Unity MonoBehaviour 管理器基类

**特性**:
- 继承自 SingletonMono<T>，提供单例模式
- 自动管理 Unity 生命周期
- 支持优先级控制
- 提供日志功能

### 2. Service<T>
```csharp
public abstract class Service<T> : SingletonNormal<T> where T : class, new()
```

**用途**: 非 MonoBehaviour 服务基类，提供数据存储功能

**特性**:
- 继承自 SingletonNormal<T>，提供单例模式
- 内置分层数据存储
- 自动被 DataManager 发现和管理
- 支持数据持久化

### 3. ManagerAttribute
```csharp
[Manager(priority)]
```

**用途**: 控制 Manager 的初始化优先级

**参数**: priority（整数，数值越小越先执行）
- 推荐值: EventManager = -10, DataManager = -5, 业务 Manager >= 0

## 使用方法

### 1. 创建 Manager

```csharp
[Manager(0)]  // 可选：设置优先级
public class UIManager : Manager<UIManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
        Log("UIManager 初始化完成", Color.green);
    }

    protected override void Start()
    {
        base.Start();
        // Start 逻辑
    }

    protected override void Update()
    {
        base.Update();
        // Update 逻辑
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 清理逻辑
    }
}
```

**访问方式**:
```csharp
UIManager.Instance.DoSomething();
```

### 2. 创建 Service

```csharp
public class InventoryService : Service<InventoryService>
{
    public const string ITEMS_CATEGORY = "Items";
    public const string EQUIPMENT_CATEGORY = "Equipment";

    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
        // Service 初始化时会自动触发 OnServiceInitialized 事件
        // DataService 会监听该事件并自动注册此 Service
    }

    [Event("AddInventoryItem")]
    public List<object> AddItem(List<object> data)
    {
        string itemId = data[0] as string;
        int quantity = (int)data[1];
        SetData(ITEMS_CATEGORY, itemId, quantity);
        return new List<object> { "成功" };
    }

    [Event("GetInventoryItemQuantity")]
    public List<object> GetItemQuantity(List<object> data)
    {
        string itemId = data[0] as string;
        int quantity = GetData<int>(ITEMS_CATEGORY, itemId);
        return new List<object> { "成功", quantity };
    }
}
```

**Service 自动注册机制**:
- Service 在构造函数调用 Initialize() 时会自动触发 `OnServiceInitialized` 事件
- DataService 监听该事件并自动注册 Service 实例
- DataService 特殊处理，不触发自己的初始化事件
- 无需手动调用任何注册方法

**访问方式**（必须通过 Event）:
```csharp
// 在本地 Manager 中调用本地 Service
EventProcessor.Instance.TriggerEventMethod("AddInventoryItem", new List<object> { "sword", 1 });
var result = EventProcessor.Instance.TriggerEventMethod("GetInventoryItemQuantity", new List<object> { "sword" });
if (result[0].ToString() == "成功")
{
    var quantity = (int)result[1];
}
```

## Manager 生命周期

### 初始化顺序
1. **Awake**: Unity Awake 方法，基类处理单例创建
2. **Initialize**: Manager 初始化方法（在 Awake 后调用）
3. **Start**: Unity Start 方法
4. **Update**: 每帧调用
5. **FixedUpdate**: 固定时间步调用
6. **LateUpdate**: Update 后调用
7. **OnDestroy**: 销毁时调用，基类清理单例引用

### 生命周期方法

```csharp
protected override void Initialize()      // 初始化（必须调用 base.Initialize()）
protected override void Awake()             // Unity Awake
protected virtual void Start()              // Unity Start
protected virtual void Update()             // Unity Update
protected virtual void FixedUpdate()       // Unity FixedUpdate
protected virtual void LateUpdate()        // Unity LateUpdate
protected override void OnDestroy()        // Unity OnDestroy（必须调用 base.OnDestroy()）
protected virtual void OnEnable()          // Unity OnEnable
protected virtual void OnDisable()         // Unity OnDisable
protected virtual void OnApplicationPause(bool pauseStatus)  // 应用暂停
protected virtual void OnApplicationFocus(bool hasFocus)    // 应用焦点
protected virtual void Cleanup()           // 清理方法
```

## Service 数据存储

### 数据结构
```csharp
Dictionary<string, Dictionary<string, object>>
{
    "Category1": {
        "Key1": Value1,
        "Key2": Value2
    },
    "Category2": {
        "Key1": Value1
    }
}
```

### 数据操作方法

```csharp
// 存储数据
SetData(category, key, value);

// 获取数据
var data = GetData(category, key);
var typedData = GetData<T>(category, key);

// 检查数据存在
bool exists = HasData(category, key);

// 移除数据
RemoveData(category, key);

// 清空分类
ClearCategory(category);

// 清空所有数据
ClearAll();

// 获取所有分类
var categories = GetCategories();

// 获取分类的所有键
var keys = GetKeys(category);

// 获取分类数据数量
int count = GetCategoryCount(category);

// 获取所有数据数量
int totalCount = GetAllDataCount();
```

## 优先级控制

### Manager 优先级
```csharp
[Manager(-30)]  // 最先执行 - EventManager
public class EventManager : Manager<EventManager> { }

[Manager(-20)]  // 第二执行 - DataManager
public class DataManager : Manager<DataManager> { }

[Manager(0)]    // 默认优先级 - ResourceManager
public class ResourceManager : Manager<ResourceManager> { }

[Manager(5)]    // 业务 Manager - UIManager
public class UIManager : Manager<UIManager> { }

[Manager(10)]   // 最后执行 - GameplayManager
public class GameplayManager : Manager<GameplayManager> { }
```

### 推荐优先级
- **-30**: EventManager（事件系统，最高优先级）
- **-20**: DataManager（数据管理）
- **0**: ResourceManager（资源管理）
- **5-10**: 业务 Manager（UIManager, AudioManager 等）
- **10+**: 游戏逻辑 Manager（GameplayManager, LevelManager 等）

## 使用示例

### 示例 1: UI Manager

```csharp
[Manager(5)]
public class UIManager : Manager<UIManager>
{
    private Canvas _mainCanvas;

    protected override void Initialize()
    {
        base.Initialize();
        _mainCanvas = FindObjectOfType<Canvas>();
        Log("UIManager 初始化完成", Color.green);
    }

    protected override void Start()
    {
        base.Start();
        // 显示主菜单
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        // 显示主菜单逻辑
    }

    public void ShowHUD()
    {
        // 显示 HUD 逻辑
    }
}
```

### 示例 2: Inventory Service

```csharp
public class InventoryService : Service<InventoryService>
{
    public const string ITEMS_CATEGORY = "Items";
    public const string EQUIPMENT_CATEGORY = "Equipment";
    public const int MAX_SLOTS = 20;

    protected override void Initialize()
    {
        base.Initialize();
        // 初始化背包数据
        if (GetCategoryCount(ITEMS_CATEGORY) == 0)
        {
            InitializeEmptyInventory();
        }
    }

    private void InitializeEmptyInventory()
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            SetData(ITEMS_CATEGORY, $"slot_{i}", null);
        }
    }

    [Event("AddInventoryItem")]
    public List<object> AddItem(List<object> data)
    {
        string itemId = data[0] as string;
        int quantity = (int)data[1];

        // 查找空槽位
        foreach (var key in GetKeys(ITEMS_CATEGORY))
        {
            if (!HasData(ITEMS_CATEGORY, key) || GetData(ITEMS_CATEGORY, key) == null)
            {
                SetData(ITEMS_CATEGORY, key, new InventoryItem
                {
                    ItemId = itemId,
                    Quantity = quantity
                });
                return new List<object> { "成功", key };
            }
        }
        return new List<object> { "背包已满" };
    }

    [Event("GetInventoryItem")]
    public List<object> GetItem(List<object> data)
    {
        string slotKey = data[0] as string;
        var item = GetData<InventoryItem>(ITEMS_CATEGORY, slotKey);
        if (item != null)
        {
            return new List<object> { "成功", item };
        }
        return new List<object> { "槽位为空" };
    }
}
```

### 示例 3: Audio Manager

```csharp
[Manager(6)]
public class AudioManager : Manager<AudioManager>
{
    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    protected override void Initialize()
    {
        base.Initialize();
        SetupAudioSources();
        Log("AudioManager 初始化完成", Color.green);
    }

    private void SetupAudioSources()
    {
        // 创建 AudioSource
    }

    [Event("PlayBackgroundMusic")]
    public List<object> PlayMusic(List<object> data)
    {
        string clipName = data[0] as string;
        // 播放背景音乐
        Log($"播放背景音乐: {clipName}", Color.blue);
        return new List<object> { "成功" };
    }

    [Event("PlaySoundEffect")]
    public List<object> PlaySFX(List<object> data)
    {
        string clipName = data[0] as string;
        // 播放音效
        Log($"播放音效: {clipName}", Color.blue);
        return new List<object> { "成功" };
    }
}
```

### 示例 4: 跨 Manager/Service 通信

```csharp
// GameplayManager 拥有本地 PlayerService
[Manager(10)]
public class GameplayManager : Manager<GameplayManager>
{
    private PlayerService _playerService;

    protected override void Initialize()
    {
        base.Initialize();
        // 创建本地 Service
        _playerService = PlayerService.Instance;
    }

    [Event("UpdatePlayerHealth")]
    public List<object> UpdatePlayerHealth(List<object> data)
    {
        float health = (float)data[0];
        float maxHealth = (float)data[1];

        // 调用本地 Service（通过 Event）
        EventProcessor.Instance.TriggerEventMethod("SetPlayerHealth", 
            new List<object> { health, maxHealth });

        // 触发跨 Manager 事件
        EventManager.Instance.TriggerEvent("OnPlayerHealthChanged", 
            new List<object> { health, maxHealth });

        return new List<object> { "成功" };
    }
}

// 本地 PlayerService
public class PlayerService : Service<PlayerService>
{
    [Event("SetPlayerHealth")]
    public List<object> SetHealth(List<object> data)
    {
        float health = (float)data[0];
        float maxHealth = (float)data[1];
        SetData("Player", "Health", health);
        SetData("Player", "MaxHealth", maxHealth);
        return new List<object> { "成功" };
    }
}

// UIManager 通过 EventListener 监听事件
[Manager(5)]
public class UIManager : Manager<UIManager>
{
    [EventListener("OnPlayerHealthChanged")]
    public List<object> UpdateHealthBar(string eventName, List<object> data)
    {
        float health = (float)data[0];
        float maxHealth = (float)data[1];

        // 更新血条
        healthBar.fillAmount = health / maxHealth;

        return new List<object>();
    }
}
```

## 日志功能

### Manager 日志
```csharp
// 带颜色的日志
Log("消息内容", Color.green);
LogWarning("警告消息");
LogError("错误消息");
```

### Service 日志
```csharp
// Service 继承自 SingletonNormal，也提供日志功能
Log("消息内容", Color.green);
LogWarning("警告消息");
LogError("错误消息");
```

## 单例模式

### Manager 单例（MonoBehaviour）
```csharp
// 获取实例
UIManager.Instance

// 检查实例是否存在
UIManager.HasInstance

// 尝试获取实例（不创建新实例）
UIManager.TryGetInstance()

// 销毁实例（通常不需要手动调用）
UIManager.DestroyInstance()
```

### Service 单例（普通类）
```csharp
// 获取实例
InventoryService.Instance

// 检查实例是否存在
InventoryService.HasInstance

// 尝试获取实例（不创建新实例）
InventoryService.TryGetInstance()

// 销毁实例
InventoryService.DestroyInstance()
```

## 最佳实践

### 1. Manager 设计原则
- **单一职责**: 每个 Manager 只负责一个功能领域
- **优先级合理**: 根据依赖关系设置正确的优先级
- **生命周期管理**: 在 OnDestroy 中清理资源
- **避免循环依赖**: 使用事件系统解耦 Manager 间通信
- **本地 Service**: Manager 只能访问自己创建的本地 Service
- **Event 注册**: Manager 的公开方法必须标记 `[Event]` 或 `[EventListener]`

### 2. Service 设计原则
- **数据分类**: 使用常量定义数据分类名称
- **类型安全**: 使用泛型方法获取数据
- **序列化要求**: 自定义数据类必须标记 `[Serializable]`
- **数据验证**: 存储前验证数据有效性
- **Event 注册**: Service 的公开方法必须标记 `[Event]` 特性
- **只能通过 Event 访问**: Service 方法不能直接调用，必须通过 Event 触发

### 3. 通信规范
- **Manager → 本地 Service**: 使用 `EventProcessor.Instance.TriggerEventMethod()`
- **Manager → 其他 Manager**: 使用 `EventManager.Instance.TriggerEvent()`
- **Service → Manager**: 使用 `EventManager.Instance.TriggerEvent()`
- **禁止直接访问**: 禁止直接访问其他 Manager 或其他 Manager 的 Service

### 3. 初始化顺序
```csharp
[Manager(-10)]
public class EventManager : Manager<EventManager> { }

[Manager(-5)]
public class DataService : Service<DataService> { }

[Manager(0)]
public class ResourceManager : Service<ResourceService> { }

[Manager(5)]
public class UIManager : Manager<UIManager> { }

[Manager(10)]
public class GameplayManager : Manager<GameplayManager> { }
```

### 4. 数据分类设计
```csharp
public class MyService : Service<MyService>
{
    // 使用常量定义分类
    public const string CONFIG_CATEGORY = "Config";
    public const string CACHE_CATEGORY = "Cache";
    public const string TEMP_CATEGORY = "Temp";
    
    // 按功能分类存储
    public void StoreConfig(string key, object value)
    {
        SetData(CONFIG_CATEGORY, key, value);
    }
    
    public void StoreCache(string key, object value)
    {
        SetData(CACHE_CATEGORY, key, value);
    }
}
```

## 性能优化

### 已实施的优化

1. **委托缓存机制**: EventProcessor 在扫描时为每个 Event 方法创建委托缓存
   - 当前状态: 由于方法签名不匹配（Event 方法通常是 `List<object>` 参数，而委托是 `object[]`），暂时使用反射调用
   - 降级机制: 自动降级到反射调用，保证功能正常
   - 机制不变: 对外 API 完全兼容，内部实现可进一步优化

2. **对象池机制**: EventManager 提供 EventDataPool 对象池，减少频繁创建 `List<object>` 导致的 GC 压力
   - 使用方式: `EventManager.EventDataPool.Rent()` 和 `EventManager.EventDataPool.Return(list)`
   - 池大小限制: 最大 50 个列表，容量限制 100
   - 线程安全: 使用 lock 保证线程安全
   - 内存泄漏修复: 已修复 TriggerEvent 中临时 List 未返回的问题
   - 机制不变: 对外 API 完全兼容，内部自动使用对象池

3. **资源缓存优化**: ResourceService 使用 struct ResourceKey 替代字符串拼接作为缓存键
   - 减少字符串分配和 GC
   - 提升字典查找速度
   - 兼容性修复: 使用传统 hash 算法替代 HashCode.Combine，确保 Unity 版本兼容
   - 机制不变: 对外 API 完全兼容

4. **数据保存优化**: DataManager 的 SaveServiceCategory 方法优化
   - 性能提升: 避免每次保存单个分类都重新遍历所有 Service 实例
   - 机制不变: 对外 API 完全兼容，内部实现优化

## 注意事项

### 架构规范
1. **Manager 只能访问本地 Service**: 禁止直接访问其他 Manager 的 Service
2. **Service 方法必须标记 Event**: Service 的公开方法必须标记 `[Event]` 特性
3. **Manager 方法必须标记 Event**: Manager 的公开方法必须标记 `[Event]` 或 `[EventListener]` 特性
4. **只能通过 Event 触发 Service**: 不能直接调用 Service 方法，必须通过 EventProcessor 触发
5. **跨 Manager 通信必须用 Event**: Manager 之间通信必须使用 EventManager 触发事件

### 基础规则
6. **Manager 必须是 MonoBehaviour**: Manager 继承自 MonoBehaviour，需要挂载到 GameObject 上
7. **Service 不需要 MonoBehaviour**: Service 是普通类，通过单例模式管理
8. **Initialize 必须调用 base**: 重写 Initialize 方法时必须调用 `base.Initialize()`
9. **OnDestroy 必须调用 base**: 重写 OnDestroy 方法时必须调用 `base.OnDestroy()`
10. **Service 自动发现**: Service 会被 DataService 自动发现和管理
11. **数据持久化**: Service 的数据会自动被 DataService 持久化
12. **优先级影响初始化顺序**: 确保依赖的 Manager 优先级更高
13. **避免在构造函数中访问单例**: Manager 的构造函数可能在单例创建前调用

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

### Q: 为什么 Manager 不能直接调用 Service 方法？
A: 为了保持架构的解耦和一致性。所有 Service 方法必须通过 Event 触发，这样可以：
- 统一通信方式
- 便于追踪和调试
- 支持异步和远程调用
- 避免循环依赖

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

### Q: Service 的数据什么时候保存？
A: DataService 会在应用退出时自动保存所有 Service 的数据，也可以通过事件手动触发保存。

### Q: 如何在 Service 中访问 Manager？
A: Service 不能直接访问 Manager，必须通过 EventManager 触发事件：
```csharp
EventManager.Instance.TriggerEvent("SomeEvent", data);
```

### Q: Manager 的优先级如何设置？
A: 根据依赖关系设置，被依赖的 Manager 优先级更高（数值更小）。推荐：EventManager (-10), DataService (-5), 其他 Manager (0+)。

### Q: 如果需要访问其他 Manager 的 Service 怎么办？
A: 不允许直接访问其他 Manager 的 Service。应该：
1. 让目标 Manager 提供一个 Event 方法
2. 通过 EventManager 触发该 Event
3. 由目标 Manager 处理请求并返回结果
