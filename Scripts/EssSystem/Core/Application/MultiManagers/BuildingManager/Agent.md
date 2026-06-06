# BuildingManager
## 概述
`BuildingManager`（`[Manager(14)]`，门面）+ `BuildingService`（业务核心）提供**通用建筑系统**。建筑被视为 `[Manager(13)] EntityManager` 之上的特化层：每座建筑 = 一个 `EntityKind.Static` 的 `Entity` + 建造材料账本 + 完成回调链。
> 设计选择：建筑系统**不替代** Demo 层的 `ConstructionManager`（后者负责放置 UI / 拖拽 / 占位检测）。BuildingManager 仅提供框架基础设施。
## 目录
```
BuildingManager/
├── BuildingManager.cs          ← 门面（[Manager(14)]）+ Event 入口
├── BuildingService.cs          ← 业务（Service<>）：Config / Instance / Supply / Complete
├── Agent.md                    ← 本文档
├── Dao/
│   ├── Building.cs             ← 运行时实例（含底层 Entity 引用 + 材料账本）
│   ├── BuildingState.cs        ← Constructing / Completed
│   ├── DefaultBuildingConfigs.cs ← 4 个示范模板
│   └── Config/
│       ├── BuildingConfig.cs   ← 模板（含链式 Builder + OnComplete 回调）
│       └── BuildingCost.cs     ← 单条材料需求
└── Runtime/
    └── BuildingCostHud.cs      ← 屏幕空间投影跟随的材料 HUD
```
## 核心心智模型
```
BuildingConfig                 BuildingService.PlaceBuilding
   │                                 │
   │  WithCollider().WithCost(...)   │
   │  .OnComplete(e => e.CanXxx(...))│
   ▼                                 ▼
              ┌──────────────┐
              │ EntityService│  ← 复用：建筑只是 Static Entity
              │ .CreateEntity│
              └──────────────┘
                     │
                     ▼
              ┌──────────────┐
              │   Entity     │  ← e.CanBeAttacked(HP).OnDied(→ DestroyBuilding)
              └──────────────┘
                     │
        materials? ──┴─── no → 完成态 → ApplyCapabilities(e)
              │
              yes
              │
              ▼
        BuildingCostHud      ← 注册 UIPanel 到 UIManager，每帧屏幕投影跟随
              │
        SupplyMaterial(...)
              │
        全部归零 → 完成态 → ApplyCapabilities(e) → 广播 EVT_COMPLETED
```
## 与 EntityManager 的关系
建筑天然是 Entity 的特化：
| 项 | Entity | Building |
|---|---|---|
| Kind | Static / Dynamic | 强制 Static |
| 持久化 | EntityConfig 进 JSON | **不持久化**（Action 不可序列化） |
| 创建入口 | `EVT_CREATE_ENTITY` | `EVT_PLACE_BUILDING`（内部仍调 EntityService） |
| 能力构造 | `entity.CanXxx(...)` 链 | 同上，但**收纳在** `BuildingConfig.OnComplete` lambda |
| HP 死亡 | 业务自己挂 OnDied | BuildingService 自动级联 `DestroyBuilding` |
## 链式 Builder
`OnComplete` 拿到的 `Entity` 已经创建好（含 Character + Collider + IDamageable），lambda 内继续用 Fluent API 加能力即可。要支持新建筑功能，无需改 BuildingManager —— 只在 lambda 里调用 `e.With<IMyNewCapability>(new MyComponent(...))` 即可。
## 4 个示范建筑（`DefaultBuildingConfigs`）
| ConfigId | 类型 | 能力 |
|---|---|---|
| `BarbedWire`   | 接触伤害 | `IContactDamage`（每秒 5 伤，半径 1） |
| `HealingTower` | 范围治疗 | `IAura`（每秒回血 5，半径 3.5） |
| `Wall`         | 阻挡     | 无能力，纯非 trigger 碰撞 |
| `Harvester`    | 周期产出 | `IHarvester`（每 5 秒丢一个 wood 到 `player`） |
`BuildingManager.Inspector` 上的 `_registerDebugTemplates` 控制是否启动时自动注册这 4 个。业务侧用同 ConfigId `RegisterConfig` 即可覆盖。
## 完整使用流程
## Event API

2026-06-06 update: Building runtime now stores `EntityInstanceId` plus cached `CharacterRoot`; it no longer keeps an `Entity` reference. Completion callbacks use `Action<string entityId>` and inject capabilities through EntityManager events.

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **BuildingManager Event**.

- `BuildingManager.EVT_COMPLETED`
- `BuildingManager.EVT_DESTROY_BUILDING`
- `BuildingManager.EVT_DESTROYED`
- `BuildingManager.EVT_PLACE_BUILDING`
- `BuildingManager.EVT_REGISTER_BUILDING_CONFIG`
- `BuildingManager.EVT_SUPPLY_BUILDING`
- `BuildingManager.EVT_SUPPLY_PROGRESS`
- `BuildingService.EVT_COMPLETED`
- `EVT_CREATE_CHARACTER`

## 注意事项 / 已知限制
1. **不持久化**：`BuildingConfig.ApplyCapabilities` 是 `Action<Entity>`，无法 JSON 序列化。每次游戏启动需重新注册。如需"上次玩家放过哪些建筑"持久化，业务侧用一个轻量 DTO `[configId, position, state, remaining]` 自己存档，启动时遍历重新调 `PlaceBuilding`。
2. **建造期外观切换**：`PendingCharacterConfigId` 字段已留好接口，但当前 `EnsureEntityConfig` 在 PlaceBuilding 那一次决定外观，**不会**在 completion 时重建 Character。需要"建造中半透明骨架 → 完成实体"的视觉切换，应该：(a) 完成时主动调 `EVT_DESTROY_CHARACTER` + `EVT_CREATE_CHARACTER` 重建，或 (b) 直接给 character 加个透明度组件随状态变。本框架不强制选择；写在 TODO.md。
3. **材料扣减**：`EVT_SUPPLY_BUILDING` **只更新账本**，**不**自动从玩家背包扣物品。业务侧（如 `ConstructionManager`）应先 `InventoryRemove` 再调 `SupplyBuilding`。否则会"白送"。
4. **HUD 同步**：`BuildingCostHud` 直接写 `RectTransform.anchoredPosition`，绕过 `EVT_DAO_PROPERTY_CHANGED` 高频事件链（参根 TODO.md item 3）。
## 优先级
`BuildingManager(14)` 依赖：
- `EntityManager(13)` — 底层 Entity 创建
- `CharacterManager(11)` — 通过 EntityManager 间接调用
- `UIManager(5)` — HUD 注册
- `InventoryManager(10)` — 业务侧扣材料（BuildingManager 自身**不**直接调用，由调用方负责）
