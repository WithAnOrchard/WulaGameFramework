# Event机制文档

## 概述

Event机制是框架的核心通信系统，提供解耦的事件驱动架构，支持系统间的高效通信。

## 核心组件

### 1. EventManager
```csharp
public class EventManager : Manager<EventManager>
```
- **作用**: 全局事件管理器，管理所有事件的注册和触发
- **特性**: 单例模式，线程安全，支持优先级和参数传递

### 2. EventAttribute
```csharp
[AttributeUsage(AttributeTargets.Method)]
public class EventAttribute : Attribute
```
- **作用**: 标记方法为事件处理器
- **参数**: 事件名称（必需）

## 使用方法

### 1. 注册事件处理器
```csharp
public class MyManager : Manager<MyManager>
{
    [Event("MyCustomEvent")]
    public List<object> HandleMyEvent(List<object> data)
    {
        // 处理事件逻辑
        return new List<object> { "处理结果" };
    }
}
```

### 2. 触发事件
```csharp
var eventManager = EventManager.Instance;
var result = eventManager.TriggerEvent("MyCustomEvent", new List<object> 
{ 
    "参数1", 
    "参数2" 
});

// 检查结果
if (result != null && result.Count > 0)
{
    string status = result[0].ToString();
    if (status == "成功")
    {
        // 处理成功逻辑
    }
}
```

### 3. 事件处理器返回格式
```csharp
// 成功格式
return new List<object> { "成功", data };

// 失败格式
return new List<object> { "错误信息" };

// 详细错误格式
return new List<object> { "错误信息", 详细错误 };
```

## 内置事件

### 1. GetServiceDataById
- **用途**: 通过Service名称、分类和ID获取数据
- **参数**: `[serviceName, categoryName, dataId]`
- **返回**: `["成功", dataObject]` 或错误信息

### 2. SaveServiceCategory
- **用途**: 保存Service分类数据
- **参数**: `[serviceName, categoryName, categoryData]`
- **返回**: `["成功"]` 或错误信息

## 事件生命周期

1. **注册**: Manager初始化时自动扫描EventAttribute标记的方法
2. **触发**: 通过EventManager.TriggerEvent触发事件
3. **处理**: 按注册顺序执行处理器
4. **返回**: 处理器返回结果给调用者

## 最佳实践

### 1. 事件命名规范
```csharp
// 推荐格式
[Event("GetServiceDataById")]      // 动作 + 对象
[Event("SaveServiceCategory")]      // 动作 + 对象
[Event("UIComponentCreated")]       // 模块 + 事件
```

### 2. 错误处理
```csharp
[Event("MyEvent")]
public List<object> MyEventHandler(List<object> data)
{
    try
    {
        // 业务逻辑
        return new List<object> { "成功", result };
    }
    catch (Exception ex)
    {
        LogError($"事件处理失败: {ex.Message}");
        return new List<object> { "处理失败", ex.Message };
    }
}
```

### 3. 参数验证
```csharp
[Event("MyEvent")]
public List<object> MyEventHandler(List<object> data)
{
    if (data == null || data.Count < requiredParams)
    {
        LogWarning("参数不足");
        return new List<object> { "参数无效" };
    }
    
    string param1 = data[0] as string;
    if (string.IsNullOrEmpty(param1))
    {
        return new List<object> { "格式无效" };
    }
    
    // 处理逻辑...
}
```

## 高级特性

### 1. 事件优先级
```csharp
// 事件按注册顺序执行，可通过Manager初始化顺序控制优先级
```

### 2. 异步事件
```csharp
[Event("AsyncEvent")]
public async Task<List<object>> AsyncEventHandler(List<object> data)
{
    // 异步处理逻辑
    await Task.Delay(1000);
    return new List<object> { "成功", result };
}
```

### 3. 条件事件处理
```csharp
[Event("ConditionalEvent")]
public List<object> ConditionalEventHandler(List<object> data)
{
    if (ShouldHandleEvent(data))
    {
        return ProcessEvent(data);
    }
    else
    {
        return new List<object> { "跳过处理" };
    }
}
```

## 性能优化

1. **事件缓存**: EventManager内部缓存事件处理器映射
2. **参数复用**: 使用List<object>传递参数，避免装箱
3. **错误隔离**: 单个事件处理器错误不影响其他处理器

## 调试支持

```csharp
// 启用事件日志
EventManager.Instance.EnableLogging = true;

// 查看已注册事件
var registeredEvents = EventManager.Instance.GetRegisteredEvents();
```

## 注意事项

1. **循环依赖**: 避免事件处理器之间相互触发造成死循环
2. **异常处理**: 所有事件处理器都应该有完整的异常处理
3. **返回格式**: 严格按照约定格式返回结果
4. **线程安全**: EventManager是线程安全的，但事件处理器内部需要注意线程安全
