# DayNight 求生 Demo 指南

> Demo 级业务集合，演示 **昼夜循环 + 波次防守 + 据点防御 + 建造** 玩法骨架。
> 不属于框架核心；命名空间 `Demo.DayNight.*`，目录 `Assets/Scripts/Demo/DayNight/`。

## 文件结构

```
Demo/DayNight/
├── DayNightGameManager.cs          总控（继承 AbstractGameManager），驱动昼夜循环 + 广播 EVT_PHASE_CHANGED
├── Agent.md                         本文档
├── WaveSpawn/                       夜晚波次刷怪
│   ├── WaveSpawnManager.cs         [Manager(20)] 监听昼夜，触发 Service.StartWave
│   ├── WaveSpawnService.cs         Tick + 配置持久化 + 通过 EntityManager.EVT_CREATE_ENTITY 刷敌
│   └── Dao/
│       ├── WaveConfig.cs            一波的总配置（回合范围、是否 boss、entries 列表）
│       └── WaveEntry.cs             单种敌人的刷怪条目（EntityConfigId / 数量 / 间隔 / 延迟）
├── BaseDefense/                     据点防御
│   ├── BaseDefenseManager.cs       [Manager(21)] 暴露 EVT_DAMAGE_BASE / EVT_RESET_BASE
│   └── BaseDefenseService.cs       HP 持久化（CAT_STATE）+ 广播 HP 变更 / 据点击毁
├── Construction/                    白天建造
│   ├── ConstructionManager.cs      [Manager(22)] 暴露 EVT_PLACE / EVT_REMOVE，仅白天允许
│   ├── ConstructionService.cs      Placement 持久化 + 广播 EVT_PLACED / EVT_REMOVED
│   └── Dao/
│       └── ConstructionPlacement.cs struct{ InstanceId, TypeId, Position, Rotation, Hp }
├── Hud/
│   └── DayNightHudManager.cs       [Manager(23)] 订阅以上广播 → UIManager DAO 树
└── Map/                             ★ 海岛地图模板（IMapTemplate）
    ├── IslandSurvivalTemplate.cs   注册到 MapTemplateRegistry，TemplateId="day_night_island"
    ├── Config/
    │   └── IslandSurvivalMapConfig.cs  WorldSizeChunks(默认 20) + Origin + Seed + 群系频率
    └── Generator/
        └── IslandSurvivalGenerator.cs  距离场 + 海岸扰动 + 温度/湿度 2D 决策表派生群系
```

## 优先级

| Manager | 优先级 |
|---|---:|
| `WaveSpawnManager` | 20 |
| `BaseDefenseManager` | 21 |
| `ConstructionManager` | 22 |
| `DayNightHudManager` | 23 |

> 均位于框架核心 Manager（≤ 13）之后；后续业务 Demo 默认起步 14+，按依赖顺序往后排。

## 通信图

```
DayNightGameManager.EVT_PHASE_CHANGED  (broadcast)
        ├── WaveSpawnManager.OnPhaseChanged    → Service.StartWaveForRound / CancelActiveWave
        ├── ConstructionManager.OnPhaseChanged → 切换 CanPlace
        └── DayNightHudManager.OnPhase         → SetText

WaveSpawnService:
        EVT_WAVE_STARTED  (broadcast) ─── HudManager.OnWaveStarted
        EVT_WAVE_CLEARED  (broadcast) ─── HudManager.OnWaveCleared
        Tick → EntityManager.EVT_CREATE_ENTITY (cross-module command, 不 using EntityManager)

BaseDefenseService:
        EVT_HP_CHANGED   (broadcast) ─── HudManager.OnBaseHp
        EVT_DESTROYED    (broadcast) ─── HudManager.OnBaseDestroyed

ConstructionService:
        EVT_PLACED   (broadcast)
        EVT_REMOVED  (broadcast)
```

## Event API

### `DayNightGameManager.EVT_PHASE_CHANGED` — 昼夜阶段切换（广播）

- **常量**: `DayNightGameManager.EVT_PHASE_CHANGED` = `"DayNightPhaseChanged"`
- **参数**: `[bool isNight, int round, bool isBossNight]`
- **返回**: 广播事件，无返回
- **副作用**: 仅广播；调用方更新自身状态

### `WaveSpawnService.EVT_WAVE_STARTED` — 波次开始（广播）

- **常量**: `WaveSpawnService.EVT_WAVE_STARTED` = `"OnWaveStarted"`
- **参数**: `[int round, int waveIndex, int totalEnemies]`
- **返回**: 广播
- **副作用**: 由 `WaveSpawnService.StartWave` 在内部触发

### `WaveSpawnService.EVT_WAVE_CLEARED` — 波次清完（广播）

- **常量**: `WaveSpawnService.EVT_WAVE_CLEARED` = `"OnWaveCleared"`
- **参数**: `[int round, int waveIndex]`
- **返回**: 广播
- **副作用**: 当全部 entry 已生成且 `_aliveInstanceIds` 为空时触发

### `BaseDefenseManager.EVT_DAMAGE_BASE` — 对据点造成伤害（命令）

- **常量**: `BaseDefenseManager.EVT_DAMAGE_BASE` = `"DamageBase"`
- **参数**: `[int amount]`（必须 > 0）
- **返回**: `ResultCode.Ok()` / `ResultCode.Fail(msg)`
- **副作用**: 改 HP → 触发 `EVT_HP_CHANGED`；若 HP 归零 → 触发 `EVT_DESTROYED`

### `BaseDefenseManager.EVT_RESET_BASE` — 重置据点 HP（命令）

- **常量**: `BaseDefenseManager.EVT_RESET_BASE` = `"ResetBase"`
- **参数**: `[]`
- **返回**: `ResultCode.Ok()`
- **副作用**: HP = MaxHp；触发 `EVT_HP_CHANGED`（delta = 0）

### `BaseDefenseService.EVT_HP_CHANGED` — HP 变更（广播）

- **常量**: `BaseDefenseService.EVT_HP_CHANGED` = `"OnBaseHpChanged"`
- **参数**: `[int currentHp, int maxHp, int delta]`（delta 正数为受伤）
- **返回**: 广播

### `BaseDefenseService.EVT_DESTROYED` — 据点击毁（广播）

- **常量**: `BaseDefenseService.EVT_DESTROYED` = `"OnBaseDestroyed"`
- **参数**: `[]`
- **返回**: 广播

### `ConstructionManager.EVT_PLACE` — 放置工事（命令）

- **常量**: `ConstructionManager.EVT_PLACE` = `"PlaceConstruction"`
- **参数**: `[string typeId, Vector3 position, float rotation?]`
- **返回**: `Ok(string instanceId)` / `Fail(msg)`
- **副作用**: 持久化 placement；触发 `EVT_PLACED`；夜晚（`_onlyAllowAtDay = true`）会拒绝

### `ConstructionManager.EVT_REMOVE` — 移除工事（命令）

- **常量**: `ConstructionManager.EVT_REMOVE` = `"RemoveConstruction"`
- **参数**: `[string instanceId]`
- **返回**: `Ok()` / `Fail(msg)`
- **副作用**: 删除持久化；触发 `EVT_REMOVED`

### `ConstructionService.EVT_PLACED` — 工事已放置（广播）

- **常量**: `ConstructionService.EVT_PLACED` = `"OnConstructionPlaced"`
- **参数**: `[string instanceId, string typeId, Vector3 position]`
- **返回**: 广播

### `ConstructionService.EVT_REMOVED` — 工事已移除（广播）

- **常量**: `ConstructionService.EVT_REMOVED` = `"OnConstructionRemoved"`
- **参数**: `[string instanceId]`
- **返回**: 广播

## 调用示例

```csharp
// 注册一波夜晚刷怪配置
WaveSpawnService.Instance.RegisterConfig(new WaveConfig
{
    ConfigId = "default_round_1",
    MinRound = 1, MaxRound = 0, IsBossWave = false,
    Entries = new()
    {
        new() { EntityConfigId = "zombie_basic", Count = 8, SpawnInterval = 0.6f },
        new() { EntityConfigId = "zombie_fast",  Count = 3, SpawnInterval = 1.5f, StartDelay = 5f },
    }
});

// 通过事件对据点造成伤害（怪物近战触发）
EventProcessor.Instance.TriggerEventMethod(BaseDefenseManager.EVT_DAMAGE_BASE,
    new List<object> { 25 });

// 玩家点击放置炮塔
EventProcessor.Instance.TriggerEventMethod(ConstructionManager.EVT_PLACE,
    new List<object> { "turret", clickWorldPos, 0f });
```

## 数据持久化路径

| Service | 路径 | 内容 |
|---|---|---|
| `WaveSpawnService` | `{persistentDataPath}/ServiceData/WaveSpawnService/Configs.json` | `WaveConfig` 列表 |
| `BaseDefenseService` | `{persistentDataPath}/ServiceData/BaseDefenseService/State.json` | `MaxHp` / `CurrentHp` |
| `ConstructionService` | `{persistentDataPath}/ServiceData/ConstructionService/Placements.json` | `ConstructionPlacement` 列表 |

## 海岛地图模板（Map/）

新增 `IMapTemplate` 实现 `IslandSurvivalTemplate`（`TemplateId = "day_night_island"`），通过
`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` 在 Unity 启动早期自动注册到
`MapTemplateRegistry`，**业务侧零代码**。

### 与 TopDownRandomTemplate 的差异

| 维度 | `top_down_random` | `day_night_island` |
|---|---|---|
| 世界大小 | 无限延伸 | **有界 20×20 chunks**（可调 `WorldSizeChunks`）|
| 形状 | 大陆轮廓由 Perlin 海拔决定 | 距离场 + Perlin 海岸扰动 → 圆形海岛 |
| 越界区块 | 仍生成 | 全 `DeepOcean`（不会无限刷新生物群系） |
| 群系决策 | 海拔 + 温度 + 湿度（含河流） | 温度 × 湿度 2D 决策表（无河流） |
| 山地 | 高海拔自然出现 | 中央半径内 + 低温 → 山地（中央山脊） |
| Spawn 规则 | 内置 forest/grass 等默认规则集 | 不内建 —— 走 `WaveSpawnService` 波次 |

### 默认配置字段（`IslandSurvivalMapConfig`）

```csharp
ConfigId = "DayNightIsland";
ChunkSize = 16;
Seed = 1337;
WorldSizeChunks = 20;            // 20×20 = 400 chunks 上限
OriginChunkX = -10;              // 世界 chunk 范围 [-10, 9]
OriginChunkY = -10;
ShorelineNoise = 0.18f;          // 海岸扰动强度
ShorelineFrequency = 0.04f;      // 海岸 Perlin 频率
BiomeFrequency = 0.025f;         // 群系噪声频率
MountainCenterRatio = 0.18f;     // 中心 18% 半径内升级山地
DeepOceanThreshold = 1.0f;
ShallowOceanThreshold = 0.92f;
BeachThreshold = 0.85f;
```

### 启用步骤

1. `DayNightGameManager` Inspector：`_mapMode` 选择 `Island`（默认值，开箱即用）
   - 想换 Perlin 大世界 → 选 `PerlinIsland`
   - 想用自定义 ConfigId → 选 `Custom` + 填 `_customMapConfigId`
2. 场景 `MapManager` Inspector：`_registerDebugTemplates` 勾上（让 Template 自动注册默认 Config）
3. **不需要手动改** MapManager 的 `_templateId`：`DayNightGameManager.Awake()` 在 MapManager.Initialize 之前调用 `MapManager.SetTemplateId(...)` 自动同步
4. Play —— 自动出现一座圆形海岛，越界即深海
5. 自定义参数：`MapService.Instance.RegisterConfig(new IslandSurvivalMapConfig("MyIsland", "更大海岛") { WorldSizeChunks = 30, Seed = 99 })` 注册新 Config，再用 `Custom` 模式 + `MyIsland` 即可

## 场景接入步骤

1. 场景 root 上挂 `DayNightGameManager`（继承 AbstractGameManager，会自动接管子节点上的所有 Manager）
2. 在 root 或其子节点 `AddComponent`：`WaveSpawnManager` / `BaseDefenseManager` / `ConstructionManager` / `DayNightHudManager`
3. 在 `WaveSpawnManager` Inspector 拖一个 `_spawnCenter`（或留空用其自身 transform）
4. 用 `WaveSpawnService.Instance.RegisterConfig(...)` 注册若干 `WaveConfig`，确保 `WaveEntry.EntityConfigId` 已经存在于 `EntityService` 中
5. 玩法接入：UI/输入触发 `EVT_PLACE`；敌人 NPC 在攻击据点时触发 `EVT_DAMAGE_BASE`；敌人 OnDeath 时调 `WaveSpawnService.Instance.NotifyEntityDied(instanceId)` 让波次能正确清完

## TODO

- [ ] `WaveSpawnService.SpawnOne` 当前用纯随机方位，可以接入 MapManager 的 spawn rule 选择更合理的刷点
- [ ] `BaseDefenseService` 缺少视觉 root（Transform）以便 NPC 寻路；可加 `IBaseLocator` 接口或事件查询位置
- [ ] `ConstructionManager` 没有"建材点"消耗；后续可以接 `InventoryManager`
- [ ] HUD 当前用绝对坐标布局；如果需要自适应需要 UIManager 后续支持 LayoutGroup
