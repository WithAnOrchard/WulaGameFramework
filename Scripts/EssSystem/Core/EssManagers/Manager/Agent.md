# Manager / Service 基类指南

## 概述

`EssSystem.Core.EssManagers.Manager` 命名空间提供 Manager 系统的基类。

## ManagerAttribute

```csharp
[Manager(priority)]   // 等价于 Unity 的 [DefaultExecutionOrder(priority)]
```

数值越小越先 `Awake`。

## Manager<T>（MonoBehaviour 单例）

```csharp
[Manager(10)]
public class MyManager : Manager<MyManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        // 在 Awake 后调用
    }

    protected override void UpdateServiceInspectorInfo()  // 可选
    {
        if (Service == null) return;
        Service.UpdateInspectorInfo();
        _serviceInspectorInfo = Service.InspectorInfo;
    }

    protected override void SyncServiceLoggingSettings()  // 可选
    {
        if (Service != null) Service.EnableLogging = _serviceEnableLogging;
    }

    // 子类如需 Start/FixedUpdate/LateUpdate 等，直接声明即可
    private void Start() { /* ... */ }
}
```

**只保留必要的 virtual 钩子**：
- `Awake()` — 调用 `Initialize()`
- `Update()` — 每帧驱动 Inspector 同步
- `Initialize()` — 子类初始化入口
- `UpdateServiceInspectorInfo()` — 子类同步 Service 数据到 Inspector
- `SyncServiceLoggingSettings()` — 子类同步日志开关到 Service

> 之前的空生命周期占位（FixedUpdate/LateUpdate/OnEnable/OnDisable/Cleanup 等）已删除——子类需要时直接声明，Unity 反射自动调用。

**Inspector 字段**（自动暴露）
- `_showServiceDataInInspector` — 是否每帧更新数据摘要
- `_serviceInspectorInfo` — Service 数据的只读快照
- `_serviceEnableLogging` — 日志开关

## Service<T>（普通 C# 单例）

```csharp
public class MyService : Service<MyService>
{
    public const string CAT_DATA = "Data";

    protected override void Initialize()
    {
        base.Initialize();   // 触发 OnServiceInitialized 事件
        // 自定义初始化
    }
}
```

继承 `IServicePersistence` + `IDisposable`。

### 数据 API

```csharp
SetData(category, key, value);          // 写入并立即保存该分类
GetData<T>(category, key);              // 强类型读取
GetData(category, key);                 // object 读取
HasData(category, key);                 // 检查是否存在
RemoveData(category, key);              // 移除（分类空后一并移除）
GetKeys(category);                      // 分类下所有键
GetCategories();                        // 所有分类名
GetCategoryData(category);              // 分类下的全部 dict
SaveAllCategories();                    // 持久化全部（IServicePersistence）
```

### 持久化

- 路径：`Application.persistentDataPath/ServiceData/{TypeName}/{Category}.json`
- 格式：MiniJson（pretty）+ `AssemblyQualifiedName` 类型标注，支持嵌套对象还原
- 写入时机：`SetData` / `RemoveData` 立即保存；`Application.quitting` 时 DataService 调 `SaveAllCategories` 兜底

### Inspector 信息

`UpdateInspectorInfo()` 重建 `InspectorInfo`（`ServiceDataInspectorInfo`），由关联 Manager 每帧调用。

## Event API

### `EVT_INITIALIZED` — Service 初始化完成（广播）
- **常量**: `Service<T>.EVT_INITIALIZED` = `"OnServiceInitialized"`
- **触发条件**: `Service<T>.Initialize()` 末尾自动触发（任何 Service 首次访问 `Instance` 时）
- **参数**: `[Service<T> serviceInstance]`
- **典型订阅**: `DataService` 监听用于把 Service 加入 `_serviceInstances` 列表（驱动 `Application.quitting` 时的批量持久化）
- **类别**: 广播（用 `[EventListener(Service<T>.EVT_INITIALIZED)]` 订阅）
- **副作用**: 无（仅通知；持久化由订阅者完成）

> 业务代码一般**不需要**手动触发或监听这个事件 —— 它是 DataService ↔ Service 自动注册的内部协议，已被框架使用。

## 注意事项

- 重写 `Initialize` 必须调 `base.Initialize()`（Service 在此触发 `Service<T>.EVT_INITIALIZED` = `"OnServiceInitialized"` 事件，DataService 监听用于自动注册）
- `EnableLogging` 是自动属性，会被持久化到 `Settings/EnableLogging`
- `OnDestroy` 由 `SingletonMono` 处理，重写时记得 `base.OnDestroy()`
