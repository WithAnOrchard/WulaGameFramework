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
├── EntityService.cs              ← 业务（Service<>）：Config / Instance / TryDamage / Tick / ApplyCollider*
├── CharacterViewBridge.cs        ← 跨模块（→ CharacterManager）bare-string 事件类型安全封装
├── Agent.md                      ← 本文档
├── Runtime/
│   └── EntityHandle.cs           ← GameObject ↔ Entity 反查桥（碰撞回调 / TakeDamage 入口）
├── Capabilities/                 ← **能力层 = 组件**（数据 + 原子操作；接口主键 = 类型）
│   ├── IEntityCapability.cs           ← 基接口（OnAttach / OnDetach）
│   ├── ITickableCapability.cs         ← 每帧 Tick 标记
│   ├── IBrain.cs                      ← 思维能力**接口**（默认实现在 `Brain/BrainComponent.cs`）
│   ├── Combat/                        伤害结算与范围伤害
│   │   ├── IAttacker / AttackerComponent
│   │   ├── IDamageable / DamageableComponent
│   │   ├── IInvulnerable / InvulnerableComponent
│   │   ├── IContactDamage / ContactDamageComponent
│   │   ├── IAura / AuraComponent
│   │   └── EntityAreaScanner          (Combat 共享 OverlapCircle 辅助)
│   ├── Effect/                        视觉/物理反馈
│   │   ├── IFlashEffect / FlashEffectComponent
│   │   └── IKnockbackEffect / KnockbackEffectComponent
│   ├── Movement/                      移动 / 朝向 / 跳跃 / 穿墙 / 地检
│   │   ├── IMovable / MovableComponent / Rigidbody2DMoverComponent
│   │   ├── IFacing / FacingComponent
│   │   ├── IPatrol / HorizontalPatrolComponent
│   │   ├── IJumpable / Rigidbody2DJumpableComponent
│   │   ├── IPhaseThrough / ColliderPhaseThroughComponent
│   │   └── IGroundSensor / Raycast2DGroundSensorComponent
│   └── Resource/                      存储 / 采集 / 需求
│       ├── IStorage / StorageComponent
│       ├── IHarvester / HarvesterComponent
│       └── INeeds / NeedsComponent
├── Brain/                        ← **思维层 = 控制器**（Utility AI；调用 Capabilities 执行）
│   ├── BrainComponent.cs              ← `IBrain` 默认实现，调度 Sensor / Consideration / Action
│   ├── ISensor / RangeSensor
│   ├── IBrainAction
│   ├── BrainContext / BrainStatus / Consideration
│   └── Actions/                       一次决策周期的执行体（调能力，不实现能力）
│       ├── AttackAction / ChaseAction / FleeAction / MoveToAction / PatrolAction
│       └── EatAction / IdleAction / SequenceAction
└── Dao/
    ├── Entity.cs                 ← 运行时实例（非持久化）+ 能力字典 + Fluent API
    ├── EntityKind.cs             ← Static / Dynamic 枚举
    ├── DefaultEntityConfigs.cs   ← 内置示例模板（Warrior / Mage / Tree）
    └── Config/
        ├── EntityConfig.cs                ← 持久化配置（Kind + CharacterConfigId + Collider + SpawnOffset）
        ├── EntityColliderConfig.cs        ← 2D collider 描述
        ├── EntityColliderShape.cs         ← Box / Circle / None
        └── EntityRuntimeDefinition.cs     ← 场景 Entity 即时注册描述
```

> **物理目录 = 命名空间**（重组后已对齐）：
> - `Capabilities/**/*.cs` → `...EntityManager.Capabilities`（子目录不引入命名空间层级）
> - `Brain/*.cs` → `...EntityManager.Brain`；`Brain/Actions/*.cs` → `...EntityManager.Brain.Actions`
> - `Dao/**/*.cs` → `...EntityManager.Dao[.Config]`
>
> **思维 vs 组件的分工**：
> - **Capability（组件）**：能做什么。提供数据（攻击力、移动速度）+ 原子操作（`Attack(target)` / `Velocity = ...`）。任意调用方（玩家输入、AI、剧情）共用。
> - **Brain（思维）**：决定做什么。Sensor 感知 → Consideration 打分 → Action 执行。Action 内部**只调用 Capability**，不重复实现能力。
> - 接口 `IBrain` 放 Capabilities/（它对 Entity 而言就是一个能力槽 `entity.Has<IBrain>()`），实现 `BrainComponent` 放 Brain/（控制器实现属于思维层）。

## 两个核心维度

### 1. EntityKind（静态 vs 动态）

| Kind | 适用 | Tick 行为 |
|---|---|---|
| `Static` | 树 / 矿石 / 建筑 / 场景道具 | 不做位置同步；业务也别挂移动 / AI |
| `Dynamic` | 动物 / 怪物 / NPC / 玩家 | 每帧把 `WorldPosition` 同步到 `Character.View.transform` |

在 `EntityConfig.Kind` 写死，创建时拷贝到 `Entity.Kind`（运行时可改，主要读源是这一份）。

### 2. Capability（能力 = 可插拔接口）

主键是**接口类型**，同一接口只能挂一个实例。

**推荐用法 —— 链式 Fluent API**（参 `Dao/Entity.cs` 末尾的 "Fluent API" 段）：

```csharp
entity
    .CanMove(moveSpeed: 3f)
    .CanAttack(attackPower: 15, attackRange: 2f, attackCooldown: 0.6f)
    .CanBeAttacked(maxHp: 100)
        .OnDied((self, killer) => Debug.Log($"{self.InstanceId} 死了"))
        .OnDamaged((self, src, dmg, type) => Debug.Log($"扣 {dmg}"))
    .CanFlash(entity.CharacterRoot)
    .CanKnockback(rb, force: 5f);

entity.CannotBeAttacked("ScriptedCutscene");   // 限时无敌
entity.Without<IInvulnerable>();                // 解除无敌
```

每个 `CanXxx` 方法等价于挂"默认实现"组件并返回 `this`。要替换为自定义实现，仍走通用 API：

```csharp
entity
    .With<IDamageable>(new MyArmoredDamageable(maxHp: 200, armor: 10))  // 自定义组件
    .With<IStorage>(new StorageComponent("chest_001", capacity: 20))
    .CanAttack(20f);   // 后续仍可继续链
```

**底层 API**（仍保留，向后兼容）：

```csharp
entity.Add<IDamageable>(new DamageableComponent(100));   // 返回组件实例
var dmg = entity.Get<IDamageable>();                     // null if not present
if (entity.Has<IDamageable>()) { /* 有血 */ }
entity.Remove<IInvulnerable>();
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
| `EntityManager.EVT_REGISTER_ENTITY_CONFIG` | `[EntityConfig config]` | `Ok(configId)` / `Fail(msg)` |
| `EntityManager.EVT_GET_ENTITY` | `[instanceId]` | `Ok(Entity)` / `Fail(msg)` |
| `EntityManager.EVT_APPLY_COLLIDER` | `[GameObject host, EntityColliderConfig cfg]` | `Ok(host)` / `Fail(msg)` |
| `EntityManager.EVT_ATTACH_ENTITY_HANDLE` | `[GameObject host, Entity entity]` | `Ok(host)` / `Fail(msg)` |

> **§2 协议解耦**：`EVT_CREATE_ENTITY` 返回 Unity 原生 `Transform`（CharacterRoot），不暴露模块私有 `Entity` 类型。需要 `Entity` 逻辑实例（挂能力/查询 HP）走 `EVT_GET_ENTITY` 或 `EntityService.Instance.GetEntity(instanceId)`。
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

## 全量 Capability 速查表

| 接口 | 默认实现 | 链式方法 | 用途 |
|---|---|---|---|
| `IMovable` | `MovableComponent` | `entity.CanMove(speed)` | 移动 |
| `IAttacker` | `AttackerComponent` | `entity.CanAttack(power, range, cd)` | 攻击 |
| `IDamageable` | `DamageableComponent` | `entity.CanBeAttacked(maxHp)` | 受伤 / HP |
| `IInvulnerable` | `InvulnerableComponent` | `entity.CannotBeAttacked(reason)` | 免疫伤害 |
| `IFlashEffect` | `FlashEffectComponent` | `entity.CanFlash(root, dur, color)` | 受伤白闪 |
| `IKnockbackEffect` | `KnockbackEffectComponent` | `entity.CanKnockback(rb, force, dur)` | 击退 |
| `IFacing` | `FacingComponent` | — | 朝向翻转 |
| `IPatrol` | `HorizontalPatrolComponent` | — | 左右巡逻 |
| `IGroundSensor` | `Raycast2DGroundSensorComponent` | — | 地面检测 |
| `IJumpable` | `Rigidbody2DJumpableComponent` | — | 跳跃 |
| `IRigidbody2DMover` | `Rigidbody2DMoverComponent` | — | Rigidbody 移动 |
| `IStorage` | `StorageComponent` | — | 容器/存储 |
| `IColliderPhaseThrough` | `ColliderPhaseThroughComponent` | — | 碰撞穿透 |
| **`IContactDamage`** | **`ContactDamageComponent`** | **`entity.CanDamageOnContact(dmg, radius, interval, type, mask)`** | 接触伤害（铁丝网） |
| **`IAura`** | **`AuraComponent`** | **`entity.EmitAura(heal, radius, interval, mask, self)`** | 范围治疗/毒气 |
| **`IHarvester`** | **`HarvesterComponent`** | **`entity.Harvest(itemId, amount, interval, invId)`** | 周期采集 |

> 加粗 = 本次新增。详细构造参数见各接口 xmldoc。

### 新增 Capability 详情

#### IContactDamage — 接触伤害
- **文件**: `Dao/Capabilities/IContactDamage.cs` + `Default/ContactDamageComponent.cs`
- **机制**: `ITickableCapability`；每 `TickInterval` 秒用 `Physics2D.OverlapCircle(CharacterRoot.position, Radius, LayerMask)` 扫描，对非自身且有 `IDamageable` 的 Entity 调 `EntityService.TryDamage`
- **链式**: `entity.CanDamageOnContact(5f, 1f, 1f, "BarbedWire")`
- **用例**: 铁丝网、火焰陷阱、荆棘地

#### IAura — 光环
- **文件**: `Dao/Capabilities/IAura.cs` + `Default/AuraComponent.cs`
- **机制**: 同上 OverlapCircle，`HealPerTick >= 0` → 调 `Heal`，`< 0` → 调 `TryDamage`
- **链式**: `entity.EmitAura(healPerTick: 5f, radius: 3.5f)`
- **用例**: 治疗塔、buff 图腾、毒气云

#### IHarvester — 周期采集
- **文件**: `Dao/Capabilities/IHarvester.cs` + `Default/HarvesterComponent.cs`
- **机制**: `ITickableCapability`；每 `Interval` 秒通过 bare-string `"InventoryAdd"`（= `InventoryService.EVT_ADD`）往 `TargetInventoryId` 丢 `Amount` 个 `ItemId`
- **链式**: `entity.Harvest("wood", 1, 5f, "player")`
- **用例**: 自动农场、矿石钻头

## 扩展点（后续补充）

- `EntityConfig`：加字段（MoveSpeed、MaxHp、AiProfileId、LootTableIds…）。
- `Entity`：加运行时状态字段（CurrentHp、Velocity、AiState…）。
- `EntityService.Tick`：加 AI 驱动 / 物理 / 状态机推进 / 事件触发。
- 新的对外能力：在 `EntityManager` 加 `EVT_*` 常量 + `[Event]` 方法，内部委托 `EntityService`。
- 广播事件（`EntitySpawned` / `EntityDied` 等）：在 `EntityService` 加常量并 `EventProcessor.Instance.TriggerEvent(...)` 触发，监听方用 `[EventListener]`。
