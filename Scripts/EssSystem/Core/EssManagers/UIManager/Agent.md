# UIManager 机制 Agent 指南

## 概述

UIManager 是 EssSystem 的 UI 管理系统，提供统一的 UI 实体注册、获取和注销功能。本指南面向 AI Agent，说明如何使用 UIManager 和 UIService 进行 UI 管理。

## 核心组件

### 1. UIManager
```csharp
[Manager(5)]
public class UIManager : Manager<UIManager>
```

**用途**: Unity MonoBehaviour UI 管理器，提供对外的 Event 接口

**特性**:
- 继承自 Manager<UIManager>
- 所有公开方法标记 `[Event]` 特性
- 通过 Event 调用本地 UIService
- 优先级设置为 5（业务 Manager 推荐值）

### 2. UIService
```csharp
public class UIService : Service<UIService>
```

**用途**: UI 服务，实现具体的 UI 实体管理逻辑

**特性**:
- 继承自 Service<UIService>
- 所有公开方法标记 `[Event]` 特性
- 内置分层数据存储
- 自动数据持久化

### 3. UIEntity
UI 实体基类，用于表示 UI 组件的数据结构

## 使用方法

### 1. 注册 UI 实体

```csharp
// 创建 UI 实体
var uiEntity = new UIEntity
{
    // 设置实体属性
};

// 注册实体
var result = EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity", 
    new List<object> { "entity_id_001", uiEntity });

if (result != null && result.Count >= 1 && result[0].ToString() == "成功")
{
    Log("UI 实体注册成功");
}
```

### 2. 获取 UI 实体

```csharp
// 获取实体
var result = EventProcessor.Instance.TriggerEventMethod("GetUIEntity", 
    new List<object> { "entity_id_001" });

if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
{
    var entity = result[1] as UIEntity;
    // 使用实体
}
```

### 3. 注销 UI 实体

```csharp
// 注销实体
var result = EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", 
    new List<object> { "entity_id_001" });

if (result != null && result.Count >= 1 && result[0].ToString() == "成功")
{
    Log("UI 实体注销成功");
}
```

## 内部 Event 方法

### UIManager Event 方法
- `RegisterUIEntity(daoId, entity)` - 注册 UI 实体
- `GetUIEntity(daoId)` - 获取 UI 实体
- `UnregisterUIEntity(daoId)` - 注销 UI 实体

### UIService Event 方法
- `ServiceRegisterUIEntity(daoId, entity)` - 注册 UI 实体（内部实现）
- `ServiceGetUIEntity(daoId)` - 获取 UI 实体（内部实现）
- `ServiceUnregisterUIEntity(daoId)` - 注销 UI 实体（内部实现）

## 数据存储结构

### Service 内部存储格式
```
Dictionary<string, Dictionary<string, object>>
{
    "UIComponents": {
        "Component1": Value1,
        "Component2": Value2
    },
    "UIEntities": {
        "entity_id_001": UIEntity1,
        "entity_id_002": UIEntity2
    }
}
```

### 数据分类
- **UI_COMPONENTS_CATEGORY** - UI 组件数据
- **UI_ENTITIES_CATEGORY** - UI 实体数据

## 使用示例

### 示例 1: 在 GameplayManager 中注册 UI 实体

```csharp
[Manager(10)]
public class GameplayManager : Manager<GameplayManager>
{
    protected override void Initialize()
    {
        base.Initialize();
        // 初始化逻辑
    }

    [Event("ShowPlayerHUD")]
    public List<object> ShowPlayerHUD(List<object> data)
    {
        string playerId = data[0] as string;

        // 创建 UI 实体
        var hudEntity = new UIEntity
        {
            // 设置 HUD 属性
        };

        // 注册 UI 实体
        var result = EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity", 
            new List<object> { $"hud_{playerId}", hudEntity });

        return result ?? new List<object> { "调用失败" };
    }
}
```

### 示例 2: 在其他 Manager 中获取 UI 实体

```csharp
[Manager(6)]
public class AudioManager : Manager<AudioManager>
{
    [EventListener("OnUIEntityCreated")]
    public List<object> OnUIEntityCreated(string eventName, List<object> data)
    {
        string entityId = data[0] as string;

        // 获取 UI 实体
        var result = EventProcessor.Instance.TriggerEventMethod("GetUIEntity", 
            new List<object> { entityId });

        if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
        {
            var entity = result[1] as UIEntity;
            // 根据实体类型播放音效
        }

        return new List<object>();
    }
}
```

### 示例 3: 跨 Manager 通信

```csharp
// UIManager 监听游戏状态变化
[Manager(5)]
public class UIManager : Manager<UIManager>
{
    [EventListener("OnPlayerHealthChanged")]
    public List<object> UpdateHealthBar(string eventName, List<object> data)
    {
        float health = (float)data[0];
        float maxHealth = (float)data[1];

        // 获取血条 UI 实体
        var result = EventProcessor.Instance.TriggerEventMethod("GetUIEntity", 
            new List<object> { "health_bar" });

        if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
        {
            var healthBar = result[1] as UIEntity;
            // 更新血条显示
            healthBar.SetValue(health / maxHealth);
        }

        return new List<object>();
    }
}
```

## 最佳实践

### 1. 实体 ID 管理
```csharp
public class UIEntityIds
{
    public const string MAIN_MENU = "main_menu";
    public const string HUD = "hud";
    public const string SETTINGS_PANEL = "settings_panel";
}

// 使用常量
var result = EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity", 
    new List<object> { UIEntityIds.HUD, hudEntity });
```

### 2. 实体生命周期管理
```csharp
// 场景加载时注册 UI 实体
[Event("OnSceneLoaded")]
public List<object> OnSceneLoaded(List<object> data)
{
    string sceneName = data[0] as string;
    
    if (sceneName == "Gameplay")
    {
        // 注册游戏 UI 实体
        RegisterGameUI();
    }
    
    return new List<object> { "成功" };
}

// 场景卸载时注销 UI 实体
[Event("OnSceneUnload")]
public List<object> OnSceneUnload(List<object> data)
{
    string sceneName = data[0] as string;
    
    if (sceneName == "Gameplay")
    {
        // 注销游戏 UI 实体
        UnregisterGameUI();
    }
    
    return new List<object> { "成功" };
}
```

### 3. 错误处理
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetUIEntity", 
    new List<object> { entityId });

if (result == null || result.Count == 0)
{
    LogError("获取 UI 实体失败：返回结果为空");
}
else if (result[0].ToString() != "成功")
{
    LogWarning($"获取 UI 实体失败：{result[0]}");
}
else if (result.Count < 2)
{
    LogError("获取 UI 实体失败：返回数据不完整");
}
else
{
    var entity = result[1] as UIEntity;
    if (entity == null)
    {
        LogError("获取 UI 实体失败：实体为 null");
    }
}
```

## 注意事项

1. **架构规范**: UIManager 只能通过 Event 调用本地 UIService，不能直接访问
2. **实体 ID**: 使用有意义的实体 ID，避免重复
3. **生命周期**: 及时注销不再使用的 UI 实体，避免内存泄漏
4. **数据持久化**: UIService 的数据会自动被 DataService 持久化
5. **线程安全**: UIManager 主要在主线程运行
6. **序列化要求**: UIEntity 必须标记 `[Serializable]` 属性以支持数据持久化
7. **跨 Manager 通信**: 使用 EventManager 触发事件，不要直接访问其他 Manager

## 常见问题

### Q: 如何注册 UI 实体？
A: 使用 Event 调用 RegisterUIEntity 方法：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity", 
    new List<object> { entityId, uiEntity });
```

### Q: 如何获取 UI 实体？
A: 使用 Event 调用 GetUIEntity 方法：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetUIEntity", 
    new List<object> { entityId });
```

### Q: UI 实体数据会持久化吗？
A: 会。UIService 继承自 Service，其数据会自动被 DataService 持久化到本地文件。

### Q: UIManager 和 UIService 有什么区别？
A:
- **UIManager**: 对外的 Event 接口，符合架构规范
- **UIService**: 内部实现，处理具体的 UI 实体管理逻辑
- UIManager 通过 Event 调用 UIService

### Q: 如何在场景切换时管理 UI 实体？
A: 监听场景加载和卸载事件，在适当的时候注册和注销 UI 实体：
```csharp
[EventListener("OnSceneLoaded")]
public List<object> OnSceneLoaded(string eventName, List<object> data)
{
    // 注册场景 UI 实体
}

[EventListener("OnSceneUnload")]
public List<object> OnSceneUnload(string eventName, List<object> data)
{
    // 注销场景 UI 实体
}
```

### Q: UIEntity 需要什么要求？
A: UIEntity 必须标记 `[Serializable]` 属性以支持数据持久化，并且应该是可序列化的类。

### Q: 如何检查 UI 实体是否存在？
A: 获取实体后检查返回结果：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("GetUIEntity", 
    new List<object> { entityId });

if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
{
    var entity = result[1] as UIEntity;
    // 实体存在
}
else
{
    // 实体不存在
}
```

### Q: 可以在其他 Manager 中直接访问 UIManager 吗？
A: 不可以。必须通过 EventManager 触发事件或 EventProcessor 调用 Event 方法。
