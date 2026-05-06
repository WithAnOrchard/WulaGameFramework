# MapManager 指南

## 概述

`MapManager`（`[Manager(12)]`，薄门面）+ `MapService`（业务核心 + 配置持久化）提供**多模板**的 2D 地图系统：

- **通用层**：`Tile` / `Chunk` / `Map`（运行时容器与坐标） + `MapConfig` 抽象基类 + `IMapGenerator` 策略接口
- **模板层**：每种地形玩法实现一对 `XxxMapConfig` + `XxxMapGenerator`，互不耦合
- **持久化**：`MapConfig` 派生类走 `_dataStorage`（多态由 AQN 还原），运行时 `Map` 实例仅内存

## 文件结构

```
MapManager/
├── MapManager.cs                    薄门面：默认注册 + Update 驱动（spawn tick + auto-save tick）
├── MapService.cs                    业务核心 + MapView 工厂 + 持久化集成 + 8 类 API
├── Agent.md                         本文档
├── Runtime/
│   └── MapView.cs                   Tilemap 渲染 + 流式预加载（RenderRadius/PreloadRadius/KeepAliveRadius）
├── Persistence/                     ★ 区块级存档底层（v2 新增）
│   ├── MapPersistenceService.cs     路径管理 + 同步读 + 异步写（Task.Run）+ 原子 .tmp 重命名
│   └── Dao/
│       ├── ChunkSaveData.cs         单 chunk 差量存档（destroyed spawns + tile overrides + placed spawns）
│       ├── MapMetaSaveData.cs       Map 级元数据 + 配置 JSON 快照
│       ├── TileOverride.cs          struct{ LocalX, LocalY, TypeId }
│       └── PlacedSpawn.cs           v2 预留：玩家手动放置的实体（v1 写空 list）
├── Spawn/                           ★ 实体生成子系统（v2 新增）
│   ├── EntitySpawnService.cs        规则集持久化 + chunk 桶化 destroyed 集合 + 分帧 spawn 队列
│   ├── EntitySpawnDecorator.cs      IChunkDecorator：评估规则、入队 spawn 请求
│   ├── IMapMetaProvider.cs          可选接口：生成器暴露 (BiomeId, Elevation, Temp, Moist, Continentalness)
│   └── Dao/
│       ├── EntitySpawnRule.cs       单条规则（过滤器 + 密度 + cluster + Priority + MaxPerChunk）
│       ├── EntitySpawnRuleSet.cs    一组规则；显式按 mapConfigId 绑定
│       ├── FloatRange.cs            可选闭区间过滤器（HasValue/Min/Max）
│       └── TileMeta.cs              TryGetTileMeta 出参
└── Dao/
    ├── Tile.cs                      单格（TypeId 字符串 + Elevation/Temp/Moist/RiverFlow byte 缓存）
    ├── Chunk.cs                     ★ 增 IsDirty / OverrideTile / EnumerateOverrides / ApplyOverrides
    ├── Map.cs                       ★ 增 PostFillHook / ChunkUnloading（pre-event）
    ├── TileType.cs                  通用 TypeId（None/Ocean/Land + 默认 RuleTile 资源常量）+ TileTypeDef
    ├── Config/
    │   ├── MapConfig.cs             抽象基类
    │   └── PerlinMapConfig.cs       兼容旧 AQN 薄包装
    ├── Generator/
    │   ├── IMapGenerator.cs         通用策略接口：FillChunk + PrewarmAround
    │   └── IChunkDecorator.cs       装饰器接口
    ├── Util/
    │   └── ChunkSeed.cs             (mapId, chunkCoord, tag) → 确定性子种子 / System.Random
    └── Templates/                   ★ 模板策略层
        ├── IMapTemplate.cs              模板策略接口 (TileTypes / DefaultConfig / SpawnRules)
        ├── MapTemplateRegistry.cs       进程级模板注册表
        ├── TopDownRandom/               俯视 2D 随机大世界
        │   ├── TopDownRandomTemplate.cs     ★ IMapTemplate 实现
        │   ├── Dao/TopDownTileTypes.cs      ★ 低到高：DeepOcean/Beach/Hill/.../Forest/Rainforest 常量
        │   ├── Config/PerlinMapConfig.cs
        │   └── Generator/
        │       ├── PerlinMapGenerator.cs    同时实现 IMapMetaProvider
        │       ├── BiomeClassifier.cs
        │       └── RiverTracer.cs
        └── SideScrollerRandom/          ★ 横版 2D 随机地图（骨架）
            ├── SideScrollerRandomTemplate.cs
            ├── Dao/SideScrollerTileTypes.cs Sky/Grass/Dirt/Stone/Bedrock/Sand/Snow/Water/Lava
            ├── Config/SideScrollerMapConfig.cs   高度/振幅/频率/倍频
            └── Generator/SideScrollerMapGenerator.cs  fBm 水平地表线 + 土/石/基岩分层
```

## 模板策略层（v3）

所有**顶视 vs 横版**之类的风格差异都装进 `IMapTemplate` 实现，以避免 MapManager 本体写死某种群系名：

```csharp
public interface IMapTemplate {
    string TemplateId { get; }                         // "top_down_random" / "side_scroller_random" / ...
    string DisplayName { get; }
    string DefaultConfigId { get; }                      // 默认 ConfigId
    void RegisterDefaultTileTypes(MapService service);   // TileTypeDef 一量表
    MapConfig CreateDefaultConfig();                     // 默认 Config 实例
    void RegisterDefaultSpawnRules(EntitySpawnService); // 可选
}
```

内置模板（启动时自动登记）：

| TemplateId | 说明 | 默认 ConfigId |
|---|---|---|
| `top_down_random` | 俯视 2D 随机大世界（Perlin + 群系 + 河流 + 默认树 spawn） | `PerlinIsland` |
| `side_scroller_random` | 横版 2D 随机地图（骨架） | `SideScrollerWorld` |

业务侧添加自定义模板：

```csharp
// 在自身 Manager.Initialize 里（于 MapManager 之前起来，或后补也可，后补需手动 RegisterDefaultTileTypes）
MapTemplateRegistry.Register(new MyCustomTemplate());
// 然后在 MapManager Inspector 把 Template Id 填入 "my_custom" 即可
```

## 坐标系约定

- **世界 Tile 坐标** `(tileX, tileY)`：整数，全局唯一。
- **区块坐标** `(chunkX, chunkY)`：`chunkX = floor(tileX / Size)`，向下取整支持负坐标。
- **本地 Tile 坐标** `(lx, ly)`：`lx = tileX - chunkX * Size`，范围 `[0, Size)`。
- **数组排布**：`Chunk.Tiles[ly * Size + lx]`（行主序）。

## 内置默认配置

`MapManager` 启动时按当前 Template 自动注册（同 ConfigId 已存在则跳过；top-down 模板优先使用 Inspector 的 `_defaultConfig`，其他走 `Template.CreateDefaultConfig()`）：

| TemplateId | 默认 ConfigId | 类型 | 说明 |
|---|---|---|---|
| `top_down_random` | `"PerlinIsland"` | `TopDownRandom.PerlinMapConfig` | 16×16 区块，MC-like 四参数地形 + Biome + River |
| `side_scroller_random` | `"SideScrollerWorld"` | `SideScrollerRandom.SideScrollerMapConfig` | fBm 水平地表线 + 土/石/基岩分层（骨架） |

**top-down 预设：大块大陆 + 大片海洋**（`SeaLevel=0.58` / `ContinentalnessScale=0.0008` / `ContinentScale=0.0008` / `ContinentalnessWeight=0.88`）。

业务侧可用同 ConfigId 覆盖默认，或关闭 `MapManager._registerDebugTemplates` 自行从零构建。

## Inspector 结构（已折叠）

`MapManager` 的 Inspector **不再镜像每个调参字段**。目前只有 3 组：

1. `_registerDebugTemplates` (bool) —— 启动注册开关
2. `_defaultConfig` (`PerlinMapConfig`) —— **展开此对象直接编辑所有 Perlin / Continent / Elevation / Temperature / Moisture / River 参数**
3. `_renderRadius` (int) —— 流式视图半径

新增调参字段的正确做法：
- 在 `Dao/Templates/TopDownRandom/Config/PerlinMapConfig.cs` 加 public 字段 + `[InspectorHelp("…")]` + `[Range]/[Min]`
- 可选：加链式 `With*` setter
- **不用再改 `MapManager.cs`**

### 默认参数一览
所有默认值以 `PerlinMapConfig` 字段初始化器为准（不再由 `MapManager` 覆写）。关键调优项：

| 字段 | 默认 | 作用 |
|---|---:|---|
| `Seed` | 20240501 | 随机种子（换地图只改这个） |
| `SeaLevel` | **0.58** | 大片海洋 + 成片大陆 |
| `ContinentalnessScale` | **0.0008** | 行星级大陆，单大陆 ~1300 Tile |
| `ContinentScale` | **0.0008** | 同上，分层掩膜同步放大 |
| `ContinentalnessWeight` | **0.88** | 大陆骨架主导，海岸线干净 |
| `NoiseScale` | 0.02 | 细节尺度 ≈ 岛屿级 |
| `Persistence` | 0.4 | 海岸线平滑自然 |

## 默认 TileType 映射

TileType 注册现在由**当前 Template** 在启动时代理（`IMapTemplate.RegisterDefaultTileTypes`）。通用部分仍由 `Dao.TileTypes` 提供：

| 通用 TypeId | DisplayName | RuleTileResourceId | 对应资产 |
|---|---|---|---|
| `TileTypes.Ocean` = `"ocean"` | 海洋 | `"GrasslandsWaterTiles"` | `Resources/Tiles/GrasslandsWaterTiles.asset` |
| `TileTypes.Land` = `"land"` | 陆地 | `"GrasslandsGround"` | `Resources/Tiles/GrasslandsGround.asset` |

模板专有群系 / 方块常量：

- top-down：`TopDownTileTypes.{DeepOcean, ShallowOcean, River, Lake, Beach, Hill, Mountain, SnowPeak, Tundra, Taiga, Grassland, Forest, Swamp, Desert, Savanna, Rainforest}`（在 `Dao/Templates/TopDownRandom/Dao/`）
- side-scroller：`SideScrollerTileTypes.{Sky, Grass, Dirt, Stone, Bedrock, Sand, Snow, Water, Lava}`（在 `Dao/Templates/SideScrollerRandom/Dao/`）

**不要**把模板专有 ID 加到通用 `Dao.TileTypes`；跨模板股代会让代码迫使看到不属于自己的群系。

渲染层据此把 `Tile.TypeId` 翻译为 RuleTile：

```csharp
var def = MapService.Instance.GetTileType(tile.TypeId);
var r = EventProcessor.Instance.TriggerEventMethod(
    ResourceManager.EVT_GET_RULE_TILE,
    new List<object> { def.RuleTileResourceId });
if (ResultCode.IsOk(r)) {
    var ruleTile = r[1] as UnityEngine.Tilemaps.RuleTile;
    tilemap.SetTile(new Vector3Int(x, y, 0), ruleTile);
}
```

业务侧可用同 TypeId 调 `MapService.RegisterTileType` 覆盖默认映射，或新增其它 TypeId（如 `"forest"` → 自定义 RuleTile）。

## 数据分类

| 常量 | 用途 | 持久化 |
|---|---|---|
| `MapService.CAT_CONFIGS` = `"Configs"` | `MapConfig` 派生类 | ✅（AQN 多态） |
| `_maps`（私有字典） | 运行时 `Map` 实例 | ❌（仅内存） |

## Event API

**本模块当前不暴露任何 EVT_ 常量。** 业务层直接调用 `MapService.Instance` 的 C# API 即可：

- 配置：`RegisterConfig(MapConfig)` / `GetConfig(id)` / `GetAllConfigs()` / `RemoveConfig(id)`
- 实例：`CreateMap(mapId, configId)` / `GetMap(id)` / `DestroyMap(id)` / `GetAllMaps()`
- 访问：`GetTile(mapId, x, y)` / `GetOrGenerateChunk(mapId, cx, cy)`，或直接拿 `Map` 对象调同名方法
- TileType：`RegisterTileType(TileTypeDef)` / `GetTileType(typeId)` / `GetAllTileTypes()`
- 视图：`CreateMapView(mapId, parent?)` / `GetMapView(id)` / `DestroyMapView(id)`
- 装饰器：`RegisterDecorator(IChunkDecorator)` / `UnregisterDecorator(id)` / `GetDecorators()`
- C# event（非 EVT_ 常量，直接订阅）：`ChunkGenerated` / `ChunkUnloaded` / `MapCreated` / `MapDestroyed`
- **持久化**：`SetTileOverride` / `ClearTileOverride` / `SaveChunk` / `SaveAllDirtyChunks` / `SaveAllDirtyChunksAllMaps` / `DeleteMapData` / `GetMapDataPath` / `AutoSaveTick` / `FlushDirtyBudget`（详见下方"区块级持久化"）
- **Spawn 已破坏标记**：`MarkSpawnDestroyed` / `UnmarkSpawnDestroyed` / `IsSpawnDestroyed` / `ClearDestroyedSpawnsInChunk`（详见下方"实体生成 (Spawn)"）

## 区块级持久化（v2 新增）

每张地图按 **chunk 一文件** 存储**差量数据**（不存地形 —— 地形由生成器确定性派生）。文件路径：

```
{persistentDataPath}/MapData/{mapId}/
├── Meta.json                       MapMetaSaveData：MapId / ConfigId / ChunkSize / Seed / ConfigJsonSnapshot
└── Chunks/
    ├── 0_0.json                    ChunkSaveData：DestroyedSpawnIds + TileOverrides + PlacedSpawns
    ├── 0_-1.json
    └── ...
```

### 加载流程

```
Map.GetOrGenerateChunk(cx, cy):
  ① IMapGenerator.FillChunk(chunk)           ← 永远确定性重跑
  ② Map.PostFillHook(chunk)                  ← MapService 安装：
       LoadChunk → ApplyOverrides → SeedDestroyed
  ③ map._chunks[key] = chunk
  ④ ChunkGenerated 事件 → MapService 跑 IChunkDecorator
        EntitySpawnDecorator 内部 IsSpawnDestroyed 跳过被破坏项
  ⑤ MapService.ChunkGenerated 对外广播
```

### 写盘流程

| 时机 | 行为 |
|---|---|
| `MarkSpawnDestroyed` / `OverrideTile` / `ClearOverride` | 标 `chunk.IsDirty = true` |
| 区块卸载（`Map.ChunkUnloading` pre-event） | dirty → 异步写盘 + 销毁 runtime 实体 |
| 自动 flush（`MapManager.Update` 每帧 → `AutoSaveTick`） | 计时到 `AutoSaveIntervalSec`（默认 30s）→ 异步写最多 `AutoSaveMaxChunksPerTick`（默认 4）个 dirty chunk |
| 应用退出（`OnApplicationQuit`） | **同步**写全部 dirty chunk（后台 Task 可能被进程杀死） |
| 应用切后台（`OnApplicationFocus(false)`） | 异步写一次防数据丢失 |

### 线程模型

- **读盘**：主线程同步（小文件 + ChunksPerFrame 限速）
- **写盘**：JSON 序列化在主线程，文件 IO 走 `Task.Run` 后台线程
- **串行化**：所有写入持有 `_writeLock`，原子 `.tmp` 重命名（断电不会半截 JSON）

### 配置变更兼容

`Meta.ConfigJsonSnapshot` 在 `CreateMap` 时与当前 MapConfig 的 `JsonUtility.ToJson` 比对。**不一致仅 LogWarning**，已有 chunk 文件保留原内容；新 chunk 用最新配置生成（边界可能割裂，按用户决策接受）。

### Tile Override

```csharp
// 玩家挖矿
MapService.Instance.SetTileOverride(mapId, worldX, worldY, "stone");

// 重置某格（视觉不立刻还原；区块下次重新加载时恢复生成器默认）
MapService.Instance.ClearTileOverride(mapId, worldX, worldY);
```

`Tile.Elevation/Temperature/Moisture/RiverFlow` 不受影响 —— 仅替换 `TypeId`。

## 实体生成 (Spawn) 子系统（v2 新增）

### 规则驱动

每个 `EntitySpawnRule` 描述：**过滤器 + 密度 + cluster + 调度** 四组参数。规则集（`EntitySpawnRuleSet`）显式绑定到 `MapConfigId`：

```csharp
EntitySpawnService.Instance.RegisterRuleSet("PerlinIsland",
    new EntitySpawnRuleSet("default-flora")
        .WithRule(new EntitySpawnRule("tree_oak", "env_tree_oak")
            .WithTileTypes(TopDownTileTypes.Forest)
            .WithMoistureRange(0.3f, 1.0f)
            .WithDensity(0.06f)
            .WithMaxPerChunk(12)
            .WithRngTag("flora_tree"))
        .WithRule(new EntitySpawnRule("grass_tuft", "env_grass")
            .WithTileTypes(TopDownTileTypes.Grassland, TopDownTileTypes.Forest)
            .WithDensity(0.18f)
            .WithCluster(2, 4, 2)
            .WithRngTag("flora_grass")));
```

业务侧需先在 `EntityService` 注册对应 `EntityConfig`（推荐 `EntityKind.Static` + `CharacterConfig` 单 Static Part 的轻量实体）。

### 过滤器

| 字段 | 说明 |
|---|---|
| `TileTypeIds` | post-river 的 `Tile.TypeId` 命中（最常用，如 `"land"` / `TopDownTileTypes.Forest`） |
| `BiomeIds` | pre-river 群系（需 `IMapMetaProvider`，PerlinMapGenerator 已实现） |
| `ElevationRange` / `TemperatureRange` / `MoistureRange` | 直接读 Tile 上的 byte 缓存，无需 fBm 重采样 |

### 确定性

所有 RNG 走 `ChunkSeed.Rng(mapId, cx, cy, rule.TileRngTag)`。同 (Seed, MapId, MapConfig, RuleSet) → spawn 位置/类型完全一致：
- 规则按 `(Priority asc, RuleId asc)` 排序
- 区块内格子按行主序 `(ly, lx)` 遍历
- 密度掷骰**先消耗 RNG**，再过 filter（保证序列稳定）
- cluster 候选按 `(dy, dx)` 字典序排序后再用 RNG 抽取

### 复现 / 持久化

InstanceId 由格子坐标稳定派生：
- 主体：`spawn:{mapId}:{cx}:{cy}:{ruleId}:{lx}_{ly}`
- cluster：`spawn:{mapId}:{cx}:{cy}:{ruleId}:{seedLx}_{seedLy}#{candLx}_{candLy}`

```csharp
// 砍树（业务层）：先标记，再销毁 GameObject
MapService.Instance.MarkSpawnDestroyed(mapId, instanceId);
EventProcessor.Instance.TriggerEventMethod(EntityManager.EVT_DESTROY_ENTITY,
    new List<object> { instanceId });

// 复活
MapService.Instance.UnmarkSpawnDestroyed(mapId, instanceId);

// 调试 / 重置生态
MapService.Instance.ClearDestroyedSpawnsInChunk(mapId, cx, cy);
```

`MarkSpawnDestroyed` 内部解析 instanceId 推算 `(cx, cy)` → 写入 chunk 桶 → 标 chunk dirty。下次该区块加载时，装饰器查 `IsSpawnDestroyed` 命中即跳过 spawn → **不会复活**。

### 性能护栏

| 参数 | 默认 | 作用 |
|---|---:|---|
| `EntitySpawnRule.MaxPerChunk` | 16 | 单规则单区块上限 |
| `EntitySpawnDecorator.GlobalMaxPerChunk` | 32 | 跨规则单区块总上限 |
| `EntitySpawnService.EntitiesPerFrame` | 8 | spawn 队列每帧消费数（分帧防 spike） |
| `MapManager._spawnEntitiesPerFrame` | 8 | 上述运行时同步源 |

### 流式预加载（`MapView.PreloadRadius`）

- `RenderRadius` (默认 4)：渲染 Tilemap 的圈层
- `PreloadRadius` (默认 `RenderRadius + 2`)：仅 `GetOrGenerateChunk`（触发读盘 + 装饰器 + spawn 入队），不画 Tilemap
- `KeepAliveRadius` (默认 100)：已生成 chunk 在此范围内不卸载

玩家走入渲染圈层时，spawn 实体已在外圈预加载好 → **无生成卡顿**。预加载严格限速：每帧最多 1 个 chunk。

### IChunkDecorator vs EntitySpawnDecorator

- `EntitySpawnDecorator` 已自动注册（priority=300）。
- 仍可写自定义 `IChunkDecorator` 处理 spawn 系统覆盖不到的需求（如村庄结构、跨 chunk 路径），通过 `MapService.RegisterDecorator` 注册即可。两条管线互不干扰。

## 生成管线钩子（植物 / 生物 / 结构 / 道具 …）

地图的"内容扩展"（spawn 树、怪物、宝箱、传送点…）通过两条入口接入，**不需要改 MapManager / MapService 本体**：

### ① `IChunkDecorator` — 装饰器（推荐）

```csharp
public class FloraDecorator : IChunkDecorator
{
    public string Id => "flora";
    public int Priority => 200;    // 地面(100) → 植被(200) → 生物(300) → 结构(400)

    public void Decorate(Map map, Chunk chunk)
    {
        var rng = ChunkSeed.Rng(map.MapId, chunk.ChunkX, chunk.ChunkY, "flora");
        for (var ly = 0; ly < chunk.Size; ly++)
        for (var lx = 0; lx < chunk.Size; lx++)
        {
            var t = chunk.GetTile(lx, ly);
            if (t.TypeId != TopDownTileTypes.Forest) continue;
            if (rng.NextDouble() > 0.08) continue;
            var wx = chunk.WorldOriginX + lx;
            var wy = chunk.WorldOriginY + ly;
            SpawnTreePrefab(wx, wy);           // 业务代码
        }
    }
}

// 启动时一行注册，对所有后续 Map 生效
MapService.Instance.RegisterDecorator(new FloraDecorator());
```

**管线顺序**：`IMapGenerator.FillChunk` → 所有装饰器按 Priority 升序依次跑 → `MapService.ChunkGenerated` 对外广播。业务层订阅 `ChunkGenerated` 得到的一定是**地形 + 全部装饰**的最终态。

**确定性**：用 `ChunkSeed.Rng(mapId, cx, cy, tag)` 派生独立 `System.Random`。同一区块卸载后再进入，spawn 分布保持一致。`tag` 让同一区块内多条管线（flora/fauna/loot）互不相关。

### ② `MapService.ChunkGenerated` / `ChunkUnloaded` C# event — 纯监听

不想写装饰器就直接订阅事件（比如只想"记录区块已生成"或"把 spawn 的 GameObject 挂到某父节点"）：

```csharp
MapService.Instance.ChunkGenerated += (map, chunk) =>
{
    // 装饰器跑完后到这里，chunk 已是最终态
};
MapService.Instance.ChunkUnloaded += (map, cx, cy) =>
{
    // 业务层据此 despawn 之前 spawn 的实体
};
```

> **despawn 约定**：装饰器 spawn 实体时，业务层需要用 `(mapId, chunkX, chunkY)` 做索引记录。区块被流式卸载或 `DestroyMap` 时会逐块触发 `ChunkUnloaded`，按索引一次性清理即可。装饰器本身不持运行时状态，便于热插拔。

### 关键类型速查

| 类型 | 路径 | 作用 |
|---|---|---|
| `IChunkDecorator` | `Dao/Generator/IChunkDecorator.cs` | 装饰器接口（Id / Priority / Decorate） |
| `ChunkSeed` | `Dao/Util/ChunkSeed.cs` | `For(...)` → int 种子 / `Rng(...)` → System.Random |
| `Map.ChunkGenerated` | `Dao/Map.cs` | 底层事件：FillChunk 完成即触发（装饰器前） |
| `MapService.ChunkGenerated` | `MapService.cs` | 业务事件：装饰器全部跑完才触发 |

后续若需要跨模块通过 `EventProcessor` 调度（如"加载区块完成"广播），再按需在 Service 上补 `[Event(EVT_XXX)]` 包装即可。届时本节将补充常量列表，并同步更新 `Assets/Agent.md` 全局事件索引。

## 用法示例

### 1）使用默认 Perlin 模板 + 流式渲染（跟随玩家）

```csharp
// ① 创建 Map 数据实例
var map = MapService.Instance.CreateMap("world1", "PerlinIsland");

// ② 创建视图（自动 new 出 Grid + Tilemap GameObject 树并绑定）
var view = MapService.Instance.CreateMapView("world1");

// ③ 配置流式参数后，view.Update() 自动按焦点位置渲染/卸载区块
view.RenderRadius = 5;                  // (2*5+1)² = 11×11 = 121 个 16×16 区块
view.ChunksPerFrame = 2;                // 每帧最多渲染 2 块，平摊首次构建 spike
view.FollowTarget = playerTransform;    // 自动跟随；玩家移动跨区块时无缝重算

// 也可以手动 / 一次性渲染（同步、立即返回）
view.RenderChunk(0, 0);
view.RenderRegion(0, 0, radius: 2);     // 中心 5×5 区块
view.RenderAll();                        // 当前 Map.LoadedChunks 已生成的全部
```

#### 流式 API 速查
| 成员 | 用途 |
|---|---|
| `RenderRadius` | 渲染半径，运行时改自动重算 |
| `ChunksPerFrame` | 每帧最多渲染区块数（异步预算） |
| `FollowTarget` | 跟随的 `Transform`；置 null 用 `SetFocus*` 手动控制 |
| `SetFocusWorldPosition(Vector3)` | 用 Unity 世界坐标设焦点 |
| `SetFocusTile(x, y)` | 用世界 Tile 坐标设焦点 |
| `SetFocusChunk(cx, cy)` | 直接用区块坐标设焦点 |

#### 流式行为
- 焦点跨区块时：新进入半径的区块按到中心的曼哈顿距离**近的先渲染**；离开半径的区块从 Tilemap 清除（`Map` 中的 `Chunk` 数据保留，再次进入时不需重新生成）。
- 同一帧内只切焦点不消耗预算 —— 队列按 `ChunksPerFrame` 平摊到后续帧。

> **相机提示**：默认 Camera 的 `orthographicSize=5` 只能看到 ~10×10 单位。设 `RenderRadius=5` 时可见范围达 176×176 tile，需把 `orthographicSize` 调到 50 左右。

### 2）注册自定义 TopDownRandom 配置

```csharp
MapService.Instance.RegisterConfig(new PerlinMapConfig("Archipelago", "群岛")
    .WithChunkSize(16)
    .WithSeed(42)
    .WithNoiseScale(0.08f)
    .WithOctaves(5)
    .WithPersistence(0.45f)
    .WithLacunarity(2.1f)
    .WithContinentalnessScale(0.0015f)
    .WithContinentalnessWeight(0.85f)
    .WithErosionScale(0.004f)
    .WithErosionWeight(0.55f)
    .WithRidgesScale(0.006f)
    .WithRidgesWeight(0.65f)
    .WithVerticalScale(1.0f)
    .WithClimateCoupledToTerrain(false) // false = 魔幻独立气候；true = 大陆性/水循环因果耦合
    .WithBaseTemperature(0.85f)
    .WithBaseMoisture(0.5f)
    .WithSeaLevel(0.55f));   // 海平面提高 → 陆地比例减少

// 已移除的 With* setter（1.x 时期 MC-like 河流未使用的参数，调用会编译报错）：
//   WithRiverSeedSearchRadius / WithRiverSurfaceMountainWeight / WithRiverSourceElevationMin
//   WithRiverMinContinentTiles / WithRiverMaxPerContinent / WithRiverMaxSteps / WithRiverMaxLakesPerRiver

var map = MapService.Instance.CreateMap("save_a", "Archipelago");
```

### 3）扩展新模板（伪代码）

新模板 = 一对 `Config` + `Generator`：

```csharp
[Serializable]
public class WfcMapConfig : MapConfig
{
    public string PatternsAsset;
    public WfcMapConfig() {}
    public WfcMapConfig(string id, string name) : base(id, name) {}
    public override IMapGenerator CreateGenerator() => new WfcMapGenerator(this);
}

public class WfcMapGenerator : IMapGenerator
{
    public WfcMapGenerator(WfcMapConfig cfg) { /* ... */ }
    public void FillChunk(Chunk chunk) { /* ... */ }
}
```

注册后 `CreateMap` 可直接使用。无需改动 `Map` / `MapService` / `MapManager`。

## 设计要点

- **生成器与配置分离**：`MapConfig` 只描述参数，`IMapGenerator` 才持有运行状态/缓存。
- **生成器确定性**：相同配置（含 Seed）+ 相同区块坐标 → 永远相同结果；区块卸载后再访问可重建。
- **懒生成**：`Map.GetOrGenerateChunk` 字典懒填，外部需要预热时显式调用即可。
- **多态持久化**：`MapConfig` 派生字段必须 `[Serializable]` + 公开字段 + 默认构造函数（`UnityEngine.JsonUtility` 兼容）。

## 待补充 / TODO

- [x] ~~地图视图层（`MapView` MonoBehaviour 渲染：SpriteRenderer 或 Tilemap）~~ → 已实现（Tilemap）
- [x] ~~`TileTypeDef` 元数据~~ → 已实现（DisplayName + RuleTileResourceId）
- [ ] 视野 / 分块加载（按相机/玩家位置流式 Load/Unload Chunk）
- [ ] 寻路与碰撞查询接口
- [ ] `TileTypeDef` 扩展：通行性 / 伤害 / 声音 / 颜色染色等
- [ ] 编辑器：地图预览面板（区块缩略图，类似 `CharacterPreviewPanel`）
- [ ] 更多模板：纯随机、WFC、手绘读图、洞穴元胞自动机 …
