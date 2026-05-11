# EntityManager

## 概述

`EntityManager`（`[Manager(13)]`，薄门面）+ `EntityService`（业务核心 + 配置持久化）提供通用的 **游戏实体** 系统：

- 每个 Entity 是一个"**逻辑实体**"：位置 / 血量 / AI 等（持有权威状态）。
- 通过 `EntityConfig.CharacterConfigId` 指向一份 `CharacterConfig` —— 由 `CharacterService` 负责生成可视的 `Character`（多部件 + 动画）。
- Entity 层不直接管显示，只持有 Character 引用；销毁 Entity 会级联清理 Character GameObject。

## 目录

```
EntityManager/
├── EntityManager.cs              ← 门面（[Manager(13)]）：Inspector + 生命周期 + Tick 调度
├── EntityService.cs              ← 业务（Service<>）：Config / Instance / TryDamage / Tick
├── Agent.md                      ← 本文档
└── Dao/
    ├── Entity.cs                 ← 运行时实例（非持久化）+ 能力字典
    ├── EntityKind.cs             ← Static / Dynamic 枚举
    ├── DefaultEntityConfigs.cs   ← 内置示例模板
    ├── Config/
    │   └── EntityConfig.cs       ← 持久化配置（含 Kind + CharacterConfigId）
    └── Capabilities/             ← 能力系统
        ├── IEntityCapability.cs  ← 基接口（OnAttach / OnDetach）
        ├── IDamageable.cs        ← 可受伤（HP / TakeDamage / 事件）
        ├── IAttacker.cs          ← 可攻击（AttackPower / CanAttack / Attack）
        ├── IInvulnerable.cs      ← 不可被攻击（框架级伤害短路）
        ├── IStorage.cs           ← 可存储（InventoryManager 容器 ID）
        └── Default/              ← 最小默认实现（业务可直接换）
            ├── DamageableComponent.cs
            ├── AttackerComponent.cs
            ├── InvulnerableComponent.cs
            └── StorageComponent.cs
```

## 两个核心维度

### 1. EntityKind（静态 vs 动态）

| Kind | 适用 | Tick 行为 |
|---|---|---|
| `Static` | 树 / 矿石 / 建筑 / 场景道具 | 不做位置同步；业务也别挂移动 / AI |
| `Dynamic` | 动物 / 怪物 / NPC / 玩家 | 每帧把 `WorldPosition` 同步到 `Character.View.transform` |

在 `EntityConfig.Kind` 写死，创建时拷贝到 `Entity.Kind`（运行时可改，主要读源是这一份）。

### 2. Capability（能力 = 可插拔接口）

主键是**接口类型**，同一接口只能挂一个实例。用法：

```csharp
// 挂能力
entity.Add<IDamageable>(new DamageableComponent(maxHp: 100));
entity.Add<IAttacker>(new AttackerComponent(attackPower: 15, attackRange: 2f));
entity.Add<IInvulnerable>(new InvulnerableComponent("ScriptedCutscene"));
entity.Add<IStorage>(new StorageComponent("chest_001", capacity: 20));

// 查询
if (entity.Has<IDamageable>()) { /* 有血 */ }
var dmg = entity.Get<IDamageable>();       // null if not present

// 卸载
entity.Remove<IInvulnerable>();             // 限时无敌结束
```

**"不可被攻击"通过 `IInvulnerable` 表达，而不是移除 `IDamageable`** —— 这样能保留 HP 状态，只是暂时豁免伤害。

## 框架级伤害结算

统一走 `EntityService.TryDamage`（内部 API，只在 Manager / 能力组件内部使用），它会：
1. `IInvulnerable.Active == true` → 短路返回 0
2. 无 `IDamageable` → 返回 0
3. 否则调 `IDamageable.TakeDamage` 并返回实际伤害

`AttackerComponent.Attack` 已内置这套检查。业务自定义攻击逻辑也建议复用同一路径。

> **注意**：`EntityService` 是 Manager 内部实现，**外部模块禁止直接 `EntityService.Instance.*`**。一律通过下方 Event API 调用。

## 加载顺序

`EntityManager(13)` 依赖：
- `CharacterManager(11)` —— 创建显示用 Character
- `MapManager(12)` —— （可选）若 Entity 绑定地图坐标 / 区块事件

## Event API

| Event 常量 | data 参数 | 返回（`ResultCode`） |
|---|---|---|
| `EntityManager.EVT_CREATE_ENTITY` | `[configId, instanceId, parent:Transform?, worldPosition:Vector3?]` | `Ok(Transform CharacterRoot)` / `Fail(msg)` |
| `EntityManager.EVT_DESTROY_ENTITY` | `[instanceId]` | `Ok(instanceId)` / `Fail(msg)` |
| `EntityManager.EVT_REGISTER_SCENE_ENTITY` | `[instanceId, GameObject host, EntityRuntimeDefinition definition]` | `Ok(instanceId)` / `Fail(msg)` |
| `EntityManager.EVT_DAMAGE_ENTITY` | `[instanceId, damage:float, damageType?:string]` | `Ok(actualDamage)` / `Fail(msg)` |

> **§2 协议解耦**：`EVT_CREATE_ENTITY` 返回 Unity 原生 `Transform`（CharacterRoot），不暴露模块私有 `Entity` 类型。业务侧需获得 `Entity` 逻辑实例（为挂能力/查询 HP 等）请用 `EntityService.Instance.GetEntity(instanceId)` —— 全局唯一入口。
>
> **§4.1 跨模块 bare-string**：调用方不 `using EntityManager` ，直接传字符串 `"CreateEntity"` / `"DestroyEntity"`。

### 调用示例

```csharp
// 创建一个史莱姆 §4.1 跨模块 bare-string
var result = EventProcessor.Instance.TriggerEventMethod(
    "CreateEntity",
    new List<object> {
        "Slime",                           // configId
        "slime_001",                       // instanceId
        null,                              // parent (Transform?)
        new Vector3(5, 5, 0),              // worldPosition (Vector3?)
    });

if (ResultCode.IsOk(result))
{
    var charRoot = result[1] as Transform;   // E2：返 Unity 原生 Transform。静态 entity 可能为 null。
    // 需要拿 Entity 运行时状态 走 EntityService。
    var slime = EntityService.Instance.GetEntity("slime_001");
    if (slime != null) slime.WorldPosition += new Vector3(1, 0, 0);
}

// 销毁
EventProcessor.Instance.TriggerEventMethod(
    "DestroyEntity",
    new List<object> { "slime_001" });
```

### 能力交互（Entity 本身就是纯数据 / 行为聚合）

拿到返回的 `Entity` 后，能力的挂 / 取 / 移除仍走 `Entity` 自身 API（`Add<T>` / `Get<T>` / `Has<T>`）。这属于"业务在 Entity 上写数据"，不涉及跨 Manager 调用，因此无需走 Event。

```csharp
slime.Add<IDamageable>(new DamageableComponent(maxHp: 100));
if (slime.Has<IDamageable>() && !slime.Has<IInvulnerable>())
{
    slime.Get<IDamageable>().TakeDamage(10, source: attacker);
}
```

## 扩展点（后续补充）

- `EntityConfig`：加字段（MoveSpeed、MaxHp、AiProfileId、LootTableIds…）。
- `Entity`：加运行时状态字段（CurrentHp、Velocity、AiState…）。
- `EntityService.Tick`：加 AI 驱动 / 物理 / 状态机推进 / 事件触发。
- 新的对外能力：在 `EntityManager` 加 `EVT_*` 常量 + `[Event]` 方法，内部委托 `EntityService`。
- 广播事件（`EntitySpawned` / `EntityDied` 等）：在 `EntityService` 加常量并 `EventProcessor.Instance.TriggerEvent(...)` 触发，监听方用 `[EventListener]`。
