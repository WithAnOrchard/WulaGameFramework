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

## 注意事项

- `Event` 类已重命名为 `EventBase`（避免与 namespace `EssSystem.Core.Event` 同名冲突）
- 监听器内异常已被 try-catch 隔离，不影响其他监听器
- `TriggerEvent` 找不到事件名只 LogWarning，不抛异常
- Target 缓存：第一次解析后缓存到 `_eventMethods` 中，后续调用零反射查找
