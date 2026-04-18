# DataManager机制文档

## 概述

DataManager是框架的核心数据管理系统，提供统一的数据存储、序列化、持久化和跨Service数据访问功能。

## 核心组件

### 1. DataManager
```csharp
public class DataManager : Manager<DataManager>
```
- **作用**: 全局数据管理器，管理所有Service的数据存储
- **特性**: 单例模式，支持本地持久化，事件驱动数据访问

### 2. Service数据存储
```csharp
public abstract class Service<T> : Manager<T> where T : Service<T>
{
    protected Dictionary<string, Dictionary<string, object>> _dataStorage;
}
```
- **作用**: Service基类提供的数据存储机制
- **结构**: 分层字典结构 {Category -> {Key -> Value}}

## 核心功能

### 1. Service数据管理
```csharp
// Service基类方法
public void SetData(string category, string key, object value)
public T GetData<T>(string category, string key)
public bool RemoveData(string category, string key)
public void ClearCategory(string category)
public int GetCategoryCount(string category)
```

### 2. 数据持久化
```csharp
// DataManager方法
public List<object> SaveDataToLocal(List<object> data)
public List<object> LoadDataFromLocal(List<object> data)
public List<object> ClearLocalData(List<object> data)
```

### 3. 跨Service数据访问
```csharp
// 通过事件系统访问其他Service数据
[Event("GetServiceDataById")]
public List<object> GetServiceDataById(List<object> data)

[Event("SaveServiceCategory")]
public List<object> SaveServiceCategory(List<object> data)
```

## 使用方法

### 1. Service数据存储
```csharp
public class UIService : Service<UIService>
{
    public const string UI_COMPONENTS_CATEGORY = "UIComponents";
    public const string UI_ENTITIES_CATEGORY = "UIEntities";
    
    public void StoreUIComponent(UIComponent component)
    {
        SetData(UI_COMPONENTS_CATEGORY, component.Id, component);
    }
    
    public UIComponent GetUIComponent(string componentId)
    {
        return GetData<UIComponent>(UI_COMPONENTS_CATEGORY, componentId);
    }
}
```

### 2. 数据持久化
```csharp
// 保存所有Service数据
var result = DataManager.Instance.SaveDataToLocal(new List<object>());
if (result[0].ToString() == "成功")
{
    Debug.Log("数据保存成功");
}

// 加载所有Service数据
var loadResult = DataManager.Instance.LoadDataFromLocal(new List<object>());
if (loadResult[0].ToString() == "成功")
{
    Debug.Log("数据加载成功");
}
```

### 3. 跨Service数据访问
```csharp
// 从其他Service获取数据
var eventManager = EventManager.Instance;
var result = eventManager.TriggerEvent("GetServiceDataById", new List<object>
{
    "UIService",           // Service名称
    "UIComponents",        // 分类名称
    "TestPanel"           // 数据ID
});

if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
{
    var component = result[1] as UIComponent;
    // 使用获取到的数据
}
```

## 数据存储结构

### 1. Service内部存储
```csharp
// UIService._dataStorage 结构
{
    "UIComponents": {
        "Panel1": UIPanelComponent,
        "Button1": UIButtonComponent,
        "Text1": UITextComponent
    },
    "UIEntities": {
        "Panel1": UIPanelEntity,
        "Button1": UIButtonEntity,
        "Text1": UITextEntity
    }
}
```

### 2. 本地持久化格式
```json
{
    "Services": {
        "UIService": {
            "UIComponents": {
                "Panel1": { "Id": "Panel1", "Type": "Panel", ... },
                "Button1": { "Id": "Button1", "Type": "Button", ... }
            },
            "UIEntities": {
                "Panel1": { "GameObjectId": "Panel1", ... },
                "Button1": { "GameObjectId": "Button1", ... }
            }
        },
        "OtherService": {
            "Category1": { ... },
            "Category2": { ... }
        }
    }
}
```

## 事件系统

### 1. GetServiceDataById事件
```csharp
[Event("GetServiceDataById")]
public List<object> GetServiceDataById(List<object> data)
{
    // 参数: [serviceName, categoryName, dataId]
    // 返回: ["成功", dataObject] 或错误信息
}
```

### 2. SaveServiceCategory事件
```csharp
[Event("SaveServiceCategory")]
public List<object> SaveServiceCategory(List<object> data)
{
    // 参数: [serviceName, categoryName, categoryData]
    // 返回: ["成功"] 或错误信息
}
```

## 数据序列化

### 1. 支持的数据类型
- **基本类型**: int, float, bool, string, DateTime
- **Unity类型**: Vector2, Vector3, Color, Rect
- **可序列化对象**: 标记了[Serializable]的自定义类
- **集合**: List<T>, Dictionary<K,V>

### 2. 序列化配置
```csharp
public class DataManager : Manager<DataManager>
{
    [SerializeField] private string _dataPath = "GameData";
    [SerializeField] private bool _enableEncryption = false;
    [SerializeField] private bool _enableCompression = false;
    
    // 序列化设置
    private JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        IgnoreReadOnlyProperties = true
    };
}
```

## 最佳实践

### 1. 数据分类设计
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

### 2. 数据验证
```csharp
public void StoreData(string category, string key, object value)
{
    // 验证数据可序列化
    if (!IsDataSerializable(value))
    {
        LogWarning($"数据 '{key}' 包含不可序列化对象");
        return;
    }
    
    // 验证参数
    if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(key))
    {
        LogError("分类和键不能为空");
        return;
    }
    
    SetData(category, key, value);
}
```

### 3. 错误处理
```csharp
public T GetDataSafely<T>(string category, string key, T defaultValue = default)
{
    try
    {
        return GetData<T>(category, key);
    }
    catch (Exception ex)
    {
        LogError($"获取数据失败: {ex.Message}");
        return defaultValue;
    }
}
```

## 高级特性

### 1. 数据版本管理
```csharp
public class DataManager : Manager<DataManager>
{
    [SerializeField] private int _dataVersion = 1;
    
    private void CheckDataVersion()
    {
        var loadedVersion = GetData<int>("System", "DataVersion");
        if (loadedVersion < _dataVersion)
        {
            MigrateData(loadedVersion, _dataVersion);
            SetData("System", "DataVersion", _dataVersion);
        }
    }
    
    private void MigrateData(int fromVersion, int toVersion)
    {
        // 数据迁移逻辑
    }
}
```

### 2. 数据备份机制
```csharp
public class DataManager : Manager<DataManager>
{
    [SerializeField] private int _backupCount = 5;
    
    private void CreateBackup()
    {
        var backupPath = $"{_dataPath}_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
        File.Copy(_dataFilePath, backupPath);
        
        // 清理旧备份
        CleanupOldBackups();
    }
    
    private void CleanupOldBackups()
    {
        var backupFiles = Directory.GetFiles(Path.GetDirectoryName(_dataPath), 
            $"{Path.GetFileNameWithoutExtension(_dataPath)}_backup_*");
        
        if (backupFiles.Length > _backupCount)
        {
            Array.Sort(backupFiles);
            for (int i = 0; i < backupFiles.Length - _backupCount; i++)
            {
                File.Delete(backupFiles[i]);
            }
        }
    }
}
```

### 3. 数据压缩
```csharp
private byte[] CompressData(byte[] data)
{
    using (var memoryStream = new MemoryStream())
    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
    {
        gzipStream.Write(data, 0, data.Length);
        gzipStream.Close();
        return memoryStream.ToArray();
    }
}

private byte[] DecompressData(byte[] compressedData)
{
    using (var memoryStream = new MemoryStream(compressedData))
    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
    using (var resultStream = new MemoryStream())
    {
        gzipStream.CopyTo(resultStream);
        return resultStream.ToArray();
    }
}
```

## 性能优化

### 1. 延迟序列化
```csharp
private Dictionary<string, object> _serializationCache = new Dictionary<string, object>();

public void SetDataWithCache(string category, string key, object value)
{
    var cacheKey = $"{category}_{key}";
    _serializationCache[cacheKey] = value;
    SetData(category, key, value);
}
```

### 2. 批量操作
```csharp
public void SetDataBatch(string category, Dictionary<string, object> data)
{
    foreach (var kvp in data)
    {
        SetData(category, kvp.Key, kvp.Value);
    }
}

public Dictionary<string, T> GetDataBatch<T>(string category, IEnumerable<string> keys)
{
    var result = new Dictionary<string, T>();
    foreach (var key in keys)
    {
        result[key] = GetData<T>(category, key);
    }
    return result;
}
```

## 调试支持

### 1. 数据查看器
```csharp
[ContextMenu("Show All Data")]
public void ShowAllData()
{
    var allData = GetAllServiceData();
    foreach (var serviceKvp in allData)
    {
        Debug.Log($"=== {serviceKvp.Key} ===");
        foreach (var categoryKvp in serviceKvp.Value)
        {
            Debug.Log($"  {categoryKvp.Key}: {categoryKvp.Value.Count} items");
        }
    }
}
```

### 2. 数据验证
```csharp
[ContextMenu("Validate Data")]
public void ValidateData()
{
    var issues = new List<string>();
    
    foreach (var service in _serviceInstances)
    {
        var dataStorage = GetDataStorage(service);
        if (dataStorage != null)
        {
            ValidateServiceData(service.GetType().Name, dataStorage, issues);
        }
    }
    
    if (issues.Count == 0)
    {
        Log("数据验证通过", Color.green);
    }
    else
    {
        Log($"发现 {issues.Count} 个数据问题", Color.red);
        foreach (var issue in issues)
        {
            LogError(issue);
        }
    }
}
```

## 注意事项

1. **线程安全**: DataManager主要在主线程运行，多线程访问需要同步
2. **数据大小**: 避免存储过大的数据对象，影响序列化性能
3. **循环引用**: 确保存储的对象没有循环引用
4. **版本兼容**: 数据格式变更时需要考虑版本兼容性
5. **存储路径**: 确保数据存储路径有写入权限
