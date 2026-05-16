# Manager / Service 基类指南

## 概述

`EssSystem.Core.Base.Manager` 命名空间提供 Manager 系统的基类。

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
- 写入时机：`SetData` 立即保存；`RemoveData` 仅在真删时写盘；`Application.quitting` 时 DataService 调 `SaveAllCategories` 兜底
- Value 编码：简单类型（string / 数字 / bool）直接存字面值；其它对象走 `JsonUtility.ToJson` 后以 JSON 字符串形式存。老存档（Dict 形式）仍能读取兼容。

### 批量写盘：`BeginBatch()` / `FlushPendingWrites()`

需要在一段代码里连续 `SetData` 大量 key 时，避免每次都写盘 → 用 `BeginBatch()` 包起来：

```csharp
using (MyService.Instance.BeginBatch())
{
    MyService.Instance.SetData("A", "k1", v1);
    MyService.Instance.SetData("A", "k2", v2);
    MyService.Instance.SetData("B", "k3", v3);
}   // Dispose 时一次性 flush：A 写 1 次，B 写 1 次（而非 3 次）
```

支持嵌套（外层 Dispose 才真正 flush）。也可手动 `service.FlushPendingWrites()`。`Application.quitting` 时框架自动 flush。

### 跨版本兼容：`[FormerName]`

类型被搬迁 / 重命名后，旧存档里写入的 AQN 会指向不存在的类型，反序列化失败。给新类挂 `[FormerName]` 即可平滑迁移：

```csharp
using EssSystem.Core.Util;

[FormerName("EssSystem.EssManager.MapManager.Dao.Config.PerlinMapConfig")]
[FormerName("EssSystem.OldNamespace.PerlinMapConfig")]   // 多次重命名都列上
[Serializable]
public class PerlinMapConfig : MapConfig { ... }
```

`Service<T>.DeserializeValue` 通过 `LegacyTypeResolver.Resolve` 查表：先按当前 AQN，命中失败则截掉 `, AssemblyName` 后缀按 FullName 查 `[FormerName]` 注册表，最后兜底 `Type.GetType(fullName)`。注册表懒加载，扫描所有用户程序集。

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
