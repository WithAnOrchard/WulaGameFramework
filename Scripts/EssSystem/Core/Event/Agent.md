# Event 机制 Agent 指南

## 概述

Event 机制是 EssSystem 的核心通信系统，提供解耦的事件驱动架构，支持系统间的高效通信。本指南面向 AI Agent，说明如何使用 Event 系统进行模块间通信。

## 核心组件

### 1. EventManager
```csharp
[Manager(-30)]
public class EventManager : Manager<EventManager>
EventManager.Instance  // 事件管理器单例
```

**用途**: 全局事件管理器，管理所有事件的注册和触发

**特性**:
- 继承自 Manager<EventManager>，优先级为 -30（最高优先级）
- 确保在其他 Manager 和 Service 初始化前完成初始化
- 管理所有事件的注册和触发

**核心方法**:
- `AddListener(eventName, listener)` - 添加事件监听器
- `RemoveListener(eventName, listener)` - 移除事件监听器
- `TriggerEvent(eventName, data)` - 触发事件

### 2. EventProcessor
```csharp
EventProcessor.Instance  // 事件处理器单例
```

**用途**: 自动扫描和注册 `[Event]` 和 `[EventListener]` 标记的方法

**核心方法**:
- `TriggerEventMethod(eventName, data)` - 触发标记了 `[Event]` 的方法
- `GetEventNames()` - 获取所有已注册的 Event 方法名称
- `GetListenerEventNames()` - 获取所有已注册的监听器事件名称

## 事件类型

### 1. Event 处理器（单次调用）
使用 `[Event]` 特性标记的方法，通过 `EventProcessor.TriggerEventMethod` 直接调用。

**特性**:
- 每个事件名称只能有一个处理器
- 通过反射直接调用方法
- 适合 RPC 风格的调用

### 2. EventListener 监听器（广播模式）
使用 `[EventListener]` 特性标记的方法，注册到 EventManager，支持多个监听器。

**特性**:
- 每个事件名称可以有多个监听器
- 支持优先级排序
- 通过 EventManager 广播触发
- 适合发布-订阅模式

## 使用方法

### 1. 定义 Event 处理器

```csharp
public class MyManager : Manager<MyManager>
{
    [Event("GetData")]
    public List<object> GetDataHandler(List<object> data)
    {
        // 处理逻辑
        return new List<object> { "成功", result };
    }
}
```

**调用方式**:
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetData", new List<object> { param1, param2 });
```

### 2. 定义 EventListener 监听器

```csharp
public class MyManager : Manager<MyManager>
{
    [EventListener("OnPlayerDamage", Priority = 10)]
    public List<object> OnPlayerDamageHandler(string eventName, List<object> data)
    {
        // 处理逻辑
        return new List<object> { "处理完成" };
    }
}
```

**调用方式**:
```csharp
EventManager.Instance.TriggerEvent("OnPlayerDamage", new List<object> { damage, source });
```

### 3. 手动注册监听器（不推荐）

```csharp
EventManager.Instance.AddListener("MyEvent", (eventName, data) =>
{
    // 处理逻辑
    return new List<object> { "结果" };
});
```

## 特性参数

### EventAttribute
```csharp
[Event("EventName")]  // 事件名称，必需
```

### EventListenerAttribute
```csharp
[EventListener("EventName", Priority = 0)]  // 事件名称（必需），优先级（可选，默认0）
```

**优先级说明**: 数值越小越先执行，推荐范围 -100 到 100

## 事件数据格式

### 参数传递
```csharp
// 触发事件时传递参数
EventManager.Instance.TriggerEvent("MyEvent", new List<object>
{
    "param1",      // 字符串
    123,           // 整数
    myObject       // 对象
});
```

### 返回值格式
```csharp
// 成功格式
return new List<object> { "成功", data };

// 失败格式
return new List<object> { "错误信息" };

// 详细错误格式
return new List<object> { "错误信息", 详细错误 };
```

## 自动注册机制

EventProcessor 在初始化时会自动扫描所有程序集（跳过系统程序集），查找标记了 `[Event]` 和 `[EventListener]` 的方法。

### 扫描规则
- 跳过系统程序集（System.*, Microsoft.*, Unity.*, UnityEngine*, UnityEditor*, Mono.*, nunit.*, mscorlib, netstandard, System）
- 扫描 public 和 non-public 方法
- 支持实例方法和静态方法
- 自动创建单例实例（Service<T>）或 MonoBehaviour 实例

### 实例创建策略
1. **MonoBehaviour**: 从场景中查找现有实例
2. **SingletonNormal<T>**: 通过 `.Instance` 获取单例
3. **普通类**: 使用 `Activator.CreateInstance` 创建实例

## 内置事件

### Service 初始化事件

#### OnServiceInitialized
- **用途**: Service 初始化时自动触发，DataService 监听此事件
- **参数**: `[serviceInstance]`
- **触发时机**: Service 构造函数调用 Initialize() 时
- **特殊处理**: DataService 不触发自己的初始化事件

## 使用示例

### 示例 1: Event 处理器（RPC 风格）

```csharp
// 定义处理器
public class DataService : Service<DataService>
{
    [Event("GetPlayerInfo")]
    public List<object> GetPlayerInfo(List<object> data)
    {
        string playerId = data[0] as string;
        var playerInfo = GetData<PlayerInfo>("Players", playerId);
        
        if (playerInfo != null)
        {
            return new List<object> { "成功", playerInfo };
        }
        return new List<object> { "玩家不存在" };
    }
}

// 调用
var result = EventProcessor.Instance.TriggerEventMethod("GetPlayerInfo", new List<object> { "player123" });
if (result[0].ToString() == "成功")
{
    var info = result[1] as PlayerInfo;
}
```

### 示例 2: EventListener 监听器（发布-订阅）

```csharp
// 定义监听器
public class UIManager : Manager<UIManager>
{
    [EventListener("OnPlayerHealthChanged", Priority = 10)]
    public List<object> UpdateHealthBar(string eventName, List<object> data)
    {
        float health = (float)data[0];
        float maxHealth = (float)data[1];
        
        // 更新 UI
        healthBar.fillAmount = health / maxHealth;
        
        return new List<object> { "UI已更新" };
    }
}

public class AudioService : Service<AudioService>
{
    [EventListener("OnPlayerHealthChanged", Priority = 5)]
    public List<object> PlayDamageSound(string eventName, List<object> data)
    {
        float health = (float)data[0];
        
        if (health < 30)
        {
            PlayLowHealthSound();
        }
        
        return new List<object>();
    }
}

// 触发事件
EventManager.Instance.TriggerEvent("OnPlayerHealthChanged", new List<object> { 50f, 100f });
```

### 示例 3: 跨模块通信

```csharp
// 在 InventoryService 中定义事件
public class InventoryService : Service<InventoryService>
{
    [Event("AddItem")]
    public List<object> AddItem(List<object> data)
    {
        string itemId = data[0] as string;
        int quantity = (int)data[1];
        
        // 添加物品逻辑
        AddItemToInventory(itemId, quantity);
        
        return new List<object> { "添加成功" };
    }
}

// 在 UIService 中调用
var result = EventProcessor.Instance.TriggerEventMethod("AddItem", new List<object> { "sword", 1 });
```

## 事件命名规范

推荐使用以下格式：
- **动作 + 对象**: `GetPlayerInfo`, `SaveData`, `LoadConfig`
- **模块 + 事件**: `OnPlayerDamage`, `UIComponentCreated`, `InventoryChanged`
- **动词风格**: 使用动词开头，明确表达事件意图

## 错误处理

### 推荐的错误处理模式
```csharp
[Event("MyEvent")]
public List<object> MyEventHandler(List<object> data)
{
    try
    {
        // 参数验证
        if (data == null || data.Count < 1)
        {
            return new List<object> { "参数无效" };
        }
        
        // 业务逻辑
        var result = ProcessData(data);
        
        return new List<object> { "成功", result };
    }
    catch (Exception ex)
    {
        LogError($"事件处理失败: {ex.Message}");
        return new List<object> { "处理失败", ex.Message };
    }
}
```

## 性能优化

### 已实施的优化

1. **委托缓存机制**: EventProcessor 在扫描时为每个 Event 方法创建委托缓存
   - 当前状态: 由于方法签名不匹配（Event 方法通常是 `List<object>` 参数，而委托是 `object[]`），暂时使用反射调用
   - 降级机制: 自动降级到反射调用，保证功能正常
   - 未来优化: 可通过表达式树或代码生成实现真正的委托缓存

2. **对象池机制**: EventManager 提供 EventDataPool 对象池，减少频繁创建 `List<object>` 导致的 GC 压力
   - 使用方式: `EventManager.EventDataPool.Rent()` 和 `EventManager.EventDataPool.Return(list)`
   - 池大小限制: 最大 50 个列表，容量限制 100
   - 线程安全: 使用 lock 保证线程安全
   - 内存泄漏修复: 已修复 TriggerEvent 中临时 List 未返回的问题

3. **自动注册**: EventProcessor 会跳过系统程序集，加快启动速度
4. **优先级控制**: 使用 Priority 参数控制监听器执行顺序
5. **错误隔离**: 单个监听器错误不影响其他监听器
6. **参数复用**: 使用 `List<object>` 传递参数，结合对象池减少 GC

## 调试支持

### 查看已注册的事件
```csharp
// 查看 Event 方法
var eventNames = EventProcessor.Instance.GetEventNames();
foreach (var name in eventNames)
{
    Debug.Log($"Event: {name}");
}

// 查看 EventListener
var listenerNames = EventProcessor.Instance.GetListenerEventNames();
foreach (var name in listenerNames)
{
    Debug.Log($"Listener: {name}");
}
```

### 启用日志
EventProcessor 和 EventManager 都会在初始化和操作时输出日志，包括：
- 发现的 Event 方法
- 发现的 EventListener 方法
- 跳过的系统程序集数量
- 事件触发和监听器调用

## 注意事项

1. **循环依赖**: 避免事件处理器之间相互触发造成死循环
2. **异常处理**: 所有事件处理器都应该有完整的异常处理
3. **返回格式**: 严格按照约定格式返回结果（`["成功", data]` 或 `["错误"]`）
4. **线程安全**: EventManager 是线程安全的，但事件处理器内部需要注意线程安全
5. **实例管理**: EventProcessor 会自动创建实例，确保类可以被正确实例化
6. **MonoBehaviour**: MonoBehaviour 组件必须存在于场景中才能被扫描到
7. **优先级**: 合理设置优先级，避免监听器执行顺序混乱
8. **事件命名**: 使用有意义的事件名称，避免冲突

## 常见问题

### Q: 为什么我的事件没有被注册？
A: 检查以下几点：
- 方法是否标记了 `[Event]` 或 `[EventListener]` 特性
- 类是否为 public
- 如果是 MonoBehaviour，是否存在于场景中
- 类是否在系统程序集中（会被跳过）

### Q: Event 和 EventListener 有什么区别？
A: 
- **Event**: 单次调用，通过 `EventProcessor.TriggerEventMethod` 直接调用，适合 RPC
- **EventListener**: 广播模式，通过 `EventManager.TriggerEvent` 触发，支持多个监听器

### Q: 如何传递复杂对象？
A: 确保对象可序列化，然后作为 `List<object>` 的元素传递：
```csharp
var myObject = new MyData { Id = 1, Name = "test" };
EventManager.Instance.TriggerEvent("MyEvent", new List<object> { myObject });
```
