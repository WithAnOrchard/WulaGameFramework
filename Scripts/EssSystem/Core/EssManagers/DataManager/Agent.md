# DataManager Agent 指南

## 概述

DataManager (DataService) 是 EssSystem 的核心数据管理系统，提供统一的数据存储、序列化、持久化功能。本指南面向 AI Agent，说明如何与 DataManager 交互。

## 核心实例

```csharp
DataManager.Instance  // 数据管理器单例
DataService.Instance   // 数据服务单例
```

## 主要功能

### 1. Service 自动注册

DataService 通过 Event 机制自动注册所有 Service 实例。

**关键方法**:
- Service 初始化时触发 `OnServiceInitialized` 事件
- DataService 监听该事件并自动注册 Service
- DataService 自己注册自己，不触发初始化事件
- 应用退出时自动保存所有 Service 数据
- 启动时自动加载所有 Service 数据

### 2. 数据持久化

#### 保存所有数据
```csharp
DataService.Instance.SetData("System", "DataVersion", 1);
// 应用退出时自动保存
```

#### 自动保存机制
```csharp
// DataService 在 Application.quit 时自动保存所有 Service 数据
// 每个 Service 的数据保存到独立的 JSON 文件
```

### 3. Service 数据操作

### Service 基类方法

所有 Service 都继承自 `Service<T>`，提供以下数据操作方法：

```csharp
// 存储数据
service.SetData(category, key, value);

// 获取数据
var data = service.GetData(category, key);
var typedData = service.GetData<T>(category, key);

// 检查数据存在
bool exists = service.HasData(category, key);

// 移除数据
service.RemoveData(category, key);

// 清空分类
service.ClearCategory(category);

// 清空所有数据
service.ClearAll();

// 获取所有分类
var categories = service.GetCategories();

// 获取分类的所有键
var keys = service.GetKeys(category);

// 获取分类数据数量
int count = service.GetCategoryCount(category);

// 获取所有数据数量
int totalCount = service.GetAllDataCount();
```

## 数据存储结构

### Service 内部存储格式
```
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

### 持久化文件格式
```json
{
  "categories": {
    "Category1": {
      "Key1": Value1,
      "Key2": Value2
    },
    "Category2": {
      "Key1": Value1
    }
  }
}
```

**特性**:
- 即使 Service 数据为空也会生成文件
- 使用 MiniJson 进行序列化
- 美化格式输出（pretty: true）
- 每个 Service 独立文件存储

## 数据序列化

### 支持的数据类型
- 基本类型: int, float, bool, string, DateTime
- Unity 类型: Vector2, Vector3, Color, Rect
- 可序列化对象: 标记了 `[Serializable]` 的自定义类
- 集合: List<T>, Dictionary<K,V>

## Service 初始化事件

### OnServiceInitialized
- **用途**: Service 初始化时自动触发，DataService 监听此事件
- **参数**: `[serviceInstance]`
- **触发时机**: Service 构造函数调用 Initialize() 时
- **特殊处理**: DataService 不触发自己的初始化事件

## 使用示例

### 创建自定义 Service
```csharp
public class MyService : Service<MyService>
{
    public const string CONFIG_CATEGORY = "Config";
    public const string CACHE_CATEGORY = "Cache";
    
    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
    }
    
    public void StoreConfig(string key, object value)
    {
        SetData(CONFIG_CATEGORY, key, value);
    }
    
    public T GetConfig<T>(string key)
    {
        return GetData<T>(CONFIG_CATEGORY, key);
    }
}
```

### Service 自动注册
```csharp
// Service 初始化时会自动触发 OnServiceInitialized 事件
// DataService 监听该事件并自动注册 Service
// 无需手动调用任何注册方法
```

### 获取已注册的 Service 列表
```csharp
var serviceInstances = DataService.Instance.GetServiceInstances();
foreach (var service in serviceInstances)
{
    Debug.Log($"注册的Service: {service.GetType().Name}");
}
```

## 注意事项

1. **自动注册**: Service 必须是 `Service<T>` 的子类且是 public class
2. **序列化要求**: 自定义数据类必须标记 `[Serializable]` 属性
3. **循环引用**: 避免存储有循环引用的对象
4. **线程安全**: DataService 主要在主线程运行
5. **EventManager 依赖**: Service 初始化时需要 EventManager 已初始化
6. **空数据保存**: 即使 Service 数据为空也会生成保存文件
7. **DataService 特殊**: DataService 不触发自己的初始化事件，自己注册自己

## 调试

### 查看所有 Service 数据
```csharp
var serviceInstances = DataService.Instance.GetServiceInstances();
foreach (var service in serviceInstances)
{
    Debug.Log($"Service: {service.GetType().Name}");
}
```

### 数据验证
确保存储的数据类型可序列化，避免存储 Unity GameObject 或 MonoBehaviour。

## 文件路径

### 数据文件夹结构
- **数据文件夹**: `Application.persistentDataPath/ServiceData/`
  - `UIService.json` - UIService 的数据
  - `InventoryService.json` - InventoryService 的数据
  - `ResourceService.json` - ResourceService 的数据
  - 其他 Service 类似

### 单独文件存储优势
- 每个 Service 数据独立存储，便于管理和调试
- 只加载需要的 Service 数据，提升启动性能
- 减少文件大小，避免大文件读写
- 即使数据为空也生成文件，便于编辑

## 性能优化

### 已实施的优化

1. **对象池机制**: EventManager 提供 EventDataPool 对象池，减少频繁创建 `List<object>` 导致的 GC 压力
   - 使用方式: `EventManager.EventDataPool.Rent()` 和 `EventManager.EventDataPool.Return(list)`
   - 池大小限制: 最大 50 个列表，容量限制 100
   - 线程安全: 使用 lock 保证线程安全

2. **MiniJson 美化输出**: 使用 pretty: true 格式化 JSON，便于阅读和编辑

## 常见问题

### Q: Service 如何自动注册到 DataService？
A: Service 在初始化时会自动触发 `OnServiceInitialized` 事件，DataService 监听该事件并自动注册 Service。

### Q: DataService 会触发自己的初始化事件吗？
A: 不会。DataService 特殊处理，自己注册自己，避免循环依赖。

### Q: Service 数据为空时会保存文件吗？
A: 会。即使 Service 数据为空也会生成 JSON 文件，便于编辑。

### Q: 如何获取已注册的 Service 列表？
A: 使用 `DataService.Instance.GetServiceInstances()` 方法。

### Q: Service 数据何时保存？
A: DataService 在 `Application.quitting` 时自动保存所有 Service 数据。

### Q: EventManager 未初始化时 Service 会报错吗？
A: Service 的初始化事件触发有 try-catch 保护，EventManager 未初始化时会静默失败，不影响 Service 正常工作。
