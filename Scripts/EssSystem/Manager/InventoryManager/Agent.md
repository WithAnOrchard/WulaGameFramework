# InventoryManager 机制 Agent 指南

## 概述

InventoryManager 是 EssSystem 的背包管理系统，提供统一的物品背包管理、物品模板、堆叠等功能。本指南面向 AI Agent，说明如何使用 InventoryManager 和 InventoryService 进行背包管理。

## 核心组件

### 1. InventoryManager
```csharp
[Manager(5)]
public class InventoryManager : Manager<InventoryManager>
```

**用途**: Unity MonoBehaviour 背包管理器，提供对外的 Event 接口

**特性**:
- 继承自 Manager<InventoryManager>
- 所有公开方法标记 `[Event]` 特性
- 直接调用本地 InventoryService
- 优先级设置为 5（业务 Manager 推荐值）
- 提供玩家背包便利 API

### 2. InventoryService
```csharp
public class InventoryService : Service<InventoryService>
```

**用途**: 背包服务，实现具体的背包管理逻辑

**特性**:
- 继承自 Service<InventoryService>
- 所有公开方法标记 `[Event]` 特性
- 内置分层数据存储
- 自动数据持久化
- Entity 注册表（运行时内存）
- 初始化时自动触发 OnServiceInitialized 事件
- DataService 自动注册此 Service

### 3. 数据类（Dao）
- **Inventory** - 背包容器，包含槽位列表
- **InventorySlot** - 槽位单元，包含物品、锁定状态
- **InventoryItem** - 物品数据，支持堆叠、链式构造

### 4. 实体类（Entity）
- **InventoryEntity** - 背包 Unity 实体，绑定 Inventory Dao 到 GameObject
- **InventoryItemEntity** - 物品 Unity 实体，绑定 Item Dao 到 GameObject

## 使用方法

### 1. 创建背包

```csharp
// 通过 InventoryService 创建
var service = InventoryService.Instance;
var inventory = service.CreateInventory("player", "玩家背包", 30);
```

### 2. 注册物品模板

```csharp
// 链式构造物品模板
service.RegisterTemplate(new InventoryItem("potion_heal")
    .WithName("治疗药水")
    .WithDescription("恢复 50 HP")
    .WithType(InventoryItemType.Consumable)
    .WithWeight(0.5f)
    .WithValue(25)
    .WithMaxStack(99));
```

### 3. 给玩家发放物品

```csharp
// 通过 Event 调用
var result = EventProcessor.Instance.TriggerEventMethod("InventoryGivePlayer", 
    new List<object> { "potion_heal", 10 });

if (result != null && result[0].ToString() == "成功")
{
    var inventoryResult = result[1] as InventoryResult?;
    Log($"发放成功: {inventoryResult}");
}
```

### 4. 从玩家拿走物品

```csharp
var result = EventProcessor.Instance.TriggerEventMethod("InventoryTakeFromPlayer", 
    new List<object> { "potion_heal", 5 });

if (result != null && result[0].ToString() == "成功")
{
    Log($"移除成功");
}
```

### 5. 查询玩家物品数量

```csharp
var result = EventProcessor.Instance.TriggerEventMethod("InventoryPlayerHas", 
    new List<object> { "potion_heal" });

if (result != null && result[0].ToString() == "成功")
{
    int count = (int)result[1];
    Log($"玩家有 {count} 个治疗药水");
}
```

## 内部 Event 方法

### InventoryManager Event 方法
- `InventoryGivePlayer(templateId, count)` - 给玩家发放物品
- `InventoryTakeFromPlayer(itemId, count)` - 从玩家拿走物品
- `InventoryPlayerHas(itemId)` - 查询玩家物品数量

### InventoryService Event 方法
- `InventoryAdd` - 添加物品到背包
- `InventoryRemove` - 从背包移除物品
- `InventoryMove` - 在背包内移动物品
- `InventoryQuery` - 查询背包信息
- `InventoryChanged` - 背包变化通知

## 数据存储结构

### Service 内部存储格式
```
Dictionary<string, Dictionary<string, object>>
{
    "Inventories": {
        "player": Inventory { Id, Name, Slots },
        "chest_001": Inventory { ... }
    },
    "Templates": {
        "potion_heal": InventoryItem { ... },
        "sword_iron": InventoryItem { ... }
    }
}
```

### 数据分类
- **CAT_INVENTORIES** - 背包数据（自动持久化）
- **CAT_TEMPLATES** - 物品模板数据（自动持久化）

## 物品堆叠机制

### 可堆叠物品
- `MaxStack > 1` 表示可堆叠
- 自动堆叠到现有相同物品槽位
- 堆叠满后自动填入空槽

### 堆叠操作
```csharp
// 判断是否可堆叠
bool canStack = item.CanStackWith(otherItem);

// 尝试添加到堆叠
int added = item.TryAdd(amount);

// 尝试从堆叠移除
int removed = item.TryRemove(amount);

// 拆分堆叠
var piece = item.Split(amount);
```

## 使用示例

### 示例 1: 在 GameplayManager 中给玩家发放物品

```csharp
[Manager(10)]
public class GameplayManager : Manager<GameplayManager>
{
    [Event("OnEnemyKilled")]
    public List<object> OnEnemyKilled(List<object> data)
    {
        string enemyType = data[0] as string;

        // 根据敌人类型给玩家奖励
        if (enemyType == "boss")
        {
            var result = EventProcessor.Instance.TriggerEventMethod("InventoryGivePlayer", 
                new List<object> { "legendary_sword", 1 });
        }

        return new List<object> { "成功" };
    }
}
```

### 示例 2: 在 UIManager 中显示物品数量

```csharp
[Manager(5)]
public class UIManager : Manager<UIManager>
{
    [EventListener("InventoryChanged")]
    public List<object> OnInventoryChanged(string eventName, List<object> data)
    {
        string inventoryId = data[0] as string;
        string operation = data[1] as string;
        string itemId = data[2] as string;
        int amount = (int)data[3];

        // 更新 UI 显示
        UpdateItemCountDisplay(itemId);

        return new List<object>();
    }

    private void UpdateItemCountDisplay(string itemId)
    {
        var result = EventProcessor.Instance.TriggerEventMethod("InventoryPlayerHas", 
            new List<object> { itemId });

        if (result != null && result[0].ToString() == "成功")
        {
            int count = (int)result[1];
            itemCounter.text = count.ToString();
        }
    }
}
```

### 示例 3: 使用 InventoryService 直接操作

```csharp
// 在本地 Manager 中直接调用本地 Service
[Manager(10)]
public class ShopManager : Manager<ShopManager>
{
    private InventoryService _inventoryService;

    protected override void Initialize()
    {
        base.Initialize();
        _inventoryService = InventoryService.Instance;
    }

    [Event("BuyItem")]
    public List<object> BuyItem(List<object> data)
    {
        string templateId = data[0] as string;
        int quantity = (int)data[1];

        // 直接调用本地 Service
        var item = _inventoryService.InstantiateTemplate(templateId, quantity);
        var result = _inventoryService.AddItem("player", item, quantity);

        return new List<object> { result.Success ? "成功" : "失败", result };
    }
}
```

## 最佳实践

### 1. 物品模板管理
```csharp
public class ItemTemplates
{
    public const string POTION_HEAL = "potion_heal";
    public const string SWORD_IRON = "sword_iron";
    public const string ARMOR_LEATHER = "armor_leather";
}

// 使用常量
var result = EventProcessor.Instance.TriggerEventMethod("InventoryGivePlayer", 
    new List<object> { ItemTemplates.POTION_HEAL, 10 });
```


### 3. 槽位锁定
```csharp
// 锁定槽位（业务自定义）
var slot = inventory.GetSlot(0);
slot.Locked = true;

// 操作时跳过锁定槽位
var emptySlots = inventory.GetEmptySlots(); // 自动过滤锁定槽位
```

### 4. 错误处理
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("InventoryGivePlayer", 
    new List<object> { templateId, count });

if (result == null || result.Count == 0)
{
    LogError("发放物品失败：返回结果为空");
}
else if (result[0].ToString() != "成功")
{
    LogWarning($"发放物品失败：{result[1]}");
}
else
{
    var inventoryResult = result[1] as InventoryResult?;
    if (inventoryResult.HasValue)
    {
        Log($"发放成功：+{inventoryResult.Value.Amount}");
    }
}
```

## 注意事项

1. **架构规范**: InventoryManager 可以直接调用本地 InventoryService，其他 Manager 必须通过 Event 调用
2. **文件组织**: 数据类放在 Dao 文件夹，GameObject 放在 Entity 文件夹
3. **序列化要求**: 所有 Dao 类必须标记 `[Serializable]` 属性
4. **堆叠规则**: 只有相同 ID 且 MaxStack > 1 的物品才能堆叠
5. **Entity 注册**: InventoryEntity 在 Awake 时自动注册，OnDestroy 时自动注销
6. **数据持久化**: 背包和模板数据自动持久化，Entity 注册表不持久化
7. **事件通知**: 背包操作会触发 `InventoryChanged` 事件

## 常见问题

### Q: 如何给玩家发放物品？
A: 使用 Event 调用 InventoryGivePlayer：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("InventoryGivePlayer", 
    new List<object> { templateId, count });
```

### Q: 如何查询玩家物品数量？
A: 使用 Event 调用 InventoryPlayerHas：
```csharp
var result = EventProcessor.Instance.TriggerEventMethod("InventoryPlayerHas", 
    new List<object> { itemId });
```

### Q: 物品如何堆叠？
A: 设置物品的 MaxStack > 1，系统会自动堆叠到相同物品的槽位。

### Q: 如何限制背包容量？
A: 创建背包时设置 MaxSlots：
```csharp
service.CreateInventory(id, name, maxSlots);
```

### Q: InventoryManager 和 InventoryService 有什么区别？
A:
- **InventoryManager**: 对外的 Event 接口，符合架构规范，提供玩家便利 API
- **InventoryService**: 内部实现，处理具体的背包管理逻辑
- InventoryManager 可以直接调用 InventoryService

### Q: 如何监听背包变化？
A: 监听 InventoryChanged 事件：
```csharp
[EventListener("InventoryChanged")]
public List<object> OnInventoryChanged(string eventName, List<object> data)
{
    // 处理背包变化
}
```

### Q: Entity 会持久化吗？
A: 不会。Entity 注册表只存在于运行时内存，不参与序列化持久化。

### Q: 如何锁定槽位？
A: 设置 InventorySlot.Locked = true，业务自定义锁定逻辑。

### Q: 背包数据会自动保存吗？
A: 会。InventoryService 继承自 Service，其数据会自动被 DataService 持久化到本地文件。
