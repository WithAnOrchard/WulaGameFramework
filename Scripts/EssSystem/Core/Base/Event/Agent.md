# Event 模块指南

## 概述

`EssSystem.Core.Event` 提供统一的事件中心 `EventProcessor`（已合并原 EventManager 的功能）。

`EventProcessor`（`[Manager(-30)]`，最先 Awake）同时承担：
1. **事件总线** — `AddListener` / `RemoveListener` / `TriggerEvent`
2. **属性扫描** — 启动时扫描所有 `[Event]` / `[EventListener]` 自动注册
3. **直接调用** — `TriggerEventMethod` 直接调 `[Event]` 标注的方法

## 三种使用模式

### 1. `[Event]` — 单点 RPC 调用

```csharp
public class PlayerManager : Manager<PlayerManager>
{
    public const string EVT_GET_DATA = "GetPlayerData";

    [Event(EVT_GET_DATA)]
    public List<object> GetPlayerData(List<object> data)
    {
        // ...
        return ResultCode.Ok(player);
    }
}

// 调用
var result = EventProcessor.Instance.TriggerEventMethod(
    PlayerManager.EVT_GET_DATA, new List<object> { id });
```

- 同名 `[Event]` 只能有一个
- 通过反射直接调用目标方法
- 适合 Manager/Service 暴露的 RPC 风格 API

### 2. `[EventListener]` — 广播订阅

```csharp
public class PlayerService : Service<PlayerService>
{
    public const string EVT_HEALTH_CHANGED = "OnPlayerHealthChanged";
}

public class HudManager : Manager<HudManager>
{
    [EventListener(PlayerService.EVT_HEALTH_CHANGED, Priority = 10)]
    public List<object> OnHealthChanged(string evt, List<object> data)
    {
        // ...
        return null;
    }
}

// 触发
EventProcessor.Instance.TriggerEvent(
    PlayerService.EVT_HEALTH_CHANGED, new List<object> { hp, maxHp });
```

- 同名事件可有多个监听器（按 `Priority` 排序，数值小先执行）
- 框架启动时自动注册到事件总线
- 适合发布-订阅场景

### 3. 手动监听（运行时动态绑定）

```csharp
EventProcessor.Instance.AddListener("MyEvent", (evt, data) => { /* ... */ return null; });
EventProcessor.Instance.RemoveListener("MyEvent", handler);
```

## EventDelegate / EventDataPool

- `EventDelegate` — `delegate List<object>(string eventName, List<object> data)`
- `EventDataPool` — `List<object>` 对象池，减少 GC（最大 50 个，容量上限 100）

```csharp
var data = EventDataPool.Rent();
data.Add(arg1); data.Add(arg2);
EventProcessor.Instance.TriggerEvent("X", data);
EventDataPool.Return(data);
```

## 内置事件

| 事件名 | 触发时机 | 监听者 |
|---|---|---|
| `OnServiceInitialized` | Service.Initialize() 完成 | DataService 自动注册 Service |

## 扫描规则

启动扫描会跳过系统/引擎程序集（`AssemblyUtils.IsSystemAssembly`），只扫用户代码。
- `[Event]` 方法：以方法名或 `[Event("...")]` 指定的名字注册到 `_eventMethods`
- `[EventListener]` 方法：注册到 `_listenerMethods` 并自动 `AddListener`

**Target 解析**：扫描期低优先级 Manager 可能尚未 Awake，`Target = null`。`ResolveTarget` 在调用时延迟解析（运行时再 `GetOrCreateInstance`）。

## 命名规范

- `[Event]`：动词开头（`GetUIEntity`, `OpenInventoryUI`）
- `[EventListener]`：`On` 开头（`OnPlayerDamage`, `OnSceneLoaded`）

## 事件名常量化（强制规则）

每个 `[Event]` 必须在所属类暴露 `public const string EVT_XXX = "...";`，**禁止**在 `[Event("...")]` 或调用方写裸字符串。

```csharp
public class UIManager : Manager<UIManager>
{
    public const string EVT_GET_ENTITY = "GetUIEntity";

    [Event(EVT_GET_ENTITY)]
    public List<object> GetUIEntity(List<object> data) { ... }
}

// 调用
EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_GET_ENTITY, data);
```

**已应用范围**：全局索引及总数以项目根 `Agent.md` 的「全局 Event 索引」表为准（该表是唯一权威源，本节不再重复维护）。未遵守者会被 `tools/agent_lint.ps1` 检出。

## 返回值约定

`[Event]` 方法返回 `List<object>`，应使用 `ResultCode` 构造：

```csharp
return ResultCode.Ok();              // 成功无数据
return ResultCode.Ok(myData);        // 成功带数据
return ResultCode.Fail("参数无效");   // 失败
```

调用方用 `ResultCode.IsOk(result)` 判断。

## 监听器管理（Phase 1.3 优化）

### 获取监听器统计信息

```csharp
var stats = EventProcessor.Instance.GetListenerStats();
Debug.Log($"总事件数: {stats["TotalEventNames"]}");
Debug.Log($"总监听器数: {stats["TotalListeners"]}");
Debug.Log($"总监听器方法数: {stats["TotalListenerMethods"]}");
```

返回字典包含：
- `TotalEventNames` — 有监听器的事件名数量
- `TotalListeners` — 所有监听器总数
- `TotalListenerMethods` — 标注 `[EventListener]` 的方法总数

### 清理空监听器列表

```csharp
EventProcessor.Instance.CleanupEmptyListeners();
```

移除所有空的监听器列表（当所有监听器都被移除后），释放内存。

## 类型化事件处理器（Phase 2 性能优化）

`TypedEventProcessor` 提供高性能的类型化事件处理，避免 `List<object>` 装箱和拆箱开销。

### 使用场景

- 每帧多次触发的事件（坐标同步、状态更新等）
- 参数类型固定的事件
- 需要最小化 GC 的关键路径

### 基本用法

```csharp
// 注册监听器（单参数）
TypedEventProcessor.AddListener<Vector3>("OnPlayerMove", (evt, pos) =>
{
    Debug.Log($"玩家移动到: {pos}");
});

// 触发事件
TypedEventProcessor.TriggerEvent<Vector3>("OnPlayerMove", new Vector3(1, 2, 3));

// 移除监听器
TypedEventProcessor.RemoveListener<Vector3>("OnPlayerMove", handler);
```

### 支持的签名

- **无参数**: `EventAction` / `EventFunc<T>`
- **单参数**: `EventAction<T>` / `EventFunc<T, TResult>`
- **双参数**: `EventAction<T1, T2>` / `EventFunc<T1, T2, TResult>`
- **三参数**: `EventAction<T1, T2, T3>`

### 性能优势

- ✅ 无装箱开销（直接传递强类型参数）
- ✅ 无 `List<object>` 分配
- ✅ 直接委托调用（比反射快）
- ✅ 适合高频事件（每帧多次）

### 与 EventProcessor 的区别

| 特性 | EventProcessor | TypedEventProcessor |
|---|---|---|
| 参数类型 | `List<object>` | 强类型泛型 |
| 装箱开销 | 有 | 无 |
| 内存分配 | 每次触发 | 仅注册时 |
| 适用场景 | 通用事件 | 高频事件 |
| 返回值 | `List<object>` | 强类型 |

## 注意事项

- `Event` 类已重命名为 `EventBase`（避免与 namespace `EssSystem.Core.Event` 同名冲突）
- 监听器内异常已被 try-catch 隔离，不影响其他监听器
- `TriggerEvent` 找不到事件名只 LogWarning，不抛异常
- Target 缓存：第一次解析后缓存到 `_eventMethods` 中，后续调用零反射查找
- 监听器统计和清理用于调试和内存优化（Phase 1.3）
