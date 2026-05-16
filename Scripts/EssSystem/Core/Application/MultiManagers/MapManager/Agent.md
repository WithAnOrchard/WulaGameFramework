# MapManager 指南

## 概述

`MapManager`（`[Manager(12)]`，薄门面）+ `MapService`（业务核心 + 配置持久化）提供**多模板**的 2D 地图系统：

- **通用层**：`Tile` / `Chunk` / `Map`（运行时容器与坐标） + `MapConfig` 抽象基类 + `IMapGenerator` 策略接口
- **模板层**：每种地形玩法实现一对 `XxxMapConfig` + `XxxMapGenerator`，互不耦合
- **持久化**：`MapConfig` 派生类走 `_dataStorage`（多态由 AQN 还原），运行时 `Map` 实例仅内存

## 3D Voxel（v5：完整 Manager/Service 框架）

`Voxel3D/` 目录是**与 2D Tile 系统并行**的 MC 风 3D 体素地图，**架构完全对齐 2D**：
`Voxel3DMapManager`（`[Manager(13)]`）+ `Voxel3DMapService` + `VoxelMap`/`VoxelChunk` Dao + `IVoxelMapTemplate` 模板层 + `IVoxelChunkDecorator` 装饰器。
当前定位：**heightmap-only**（无地下、无破坏），1 chunk = 1 GameObject + 1 Mesh，atlas 贴图采样。

```
MapManager/Voxel3D/
├── Voxel3DMapManager.cs        [Manager(13)] 门面：模板/默认 Config/默认 BlockType 注册
├── Voxel3DMapService.cs        Service：Config CRUD + BlockType palette + Map 实例 + MapView 工厂 + 装饰器 + 生成管线
├── Dao/
│   ├── VoxelBlockType.cs       方块定义（Id + 顶/侧 Color32 + atlas slot）
│   ├── VoxelBlockTypes.cs      内置常量 + DefaultPalette + VoxelAtlasSlots
│   ├── VoxelMapConfig.cs       ConfigId + Perlin / SeaLevel / SnowLine + CreateGenerator()
│   ├── VoxelChunk.cs           Heights + TopBlocks + SideBlocks 三个 byte[]
│   ├── VoxelMap.cs             运行时实例：GetOrGenerateChunk / PeekChunk / UnloadChunk + 事件
│   └── Templates/
│       ├── IVoxelMapTemplate.cs        模板策略接口
│       ├── VoxelMapTemplateRegistry.cs 进程级注册表
│       └── DefaultVoxel/
│           └── DefaultVoxelTemplate.cs "default_voxel_3d" 内置模板
├── Generator/
│   ├── IVoxelMapGenerator.cs       生成策略接口
│   ├── IVoxelChunkDecorator.cs     装饰器接口
│   ├── VoxelHeightmapGenerator.cs  fBm Perlin → heightmap
│   └── VoxelChunkMesher.cs         heightmap + 4 邻居 → Mesh + per-vertex UV
└── Runtime/
    ├── Voxel3DMapView.cs       MonoBehaviour：脏标记+双队列流式渲染 + Bind(VoxelMap)
    ├── VoxelTextureAtlas.cs    运行时拼 64×32 贴图集（8 slot × 16²）
    ├── VoxelTextured.shader    Wula/VoxelTextured —— atlas 采样 × 顶点色 × 方向光
    ├── VoxelMaterialFactory.cs 运行时建默认 Material（优先 VoxelTextured）
    └── VoxelVertexColor.shader Wula/VoxelVertexColor —— fallback 顶点色 shader
```

**用法（与 2D MapManager 同构）**：

```csharp
// ① Voxel3DMapManager 启动时已自动注册 default_voxel_3d 模板 + Config
//    （AbstractGameManager 子节点扫描；DayNight3DGameManager 用 EnsureSubManager 兜底添加）

// ② 创建 Map 数据实例 + MapView 渲染
var map = Voxel3DMapService.Instance.CreateMap("voxel_world", "default_voxel_3d");
var view = Voxel3DMapService.Instance.CreateMapView("voxel_world", parentTransform);

// ③ 配置流式参数
view.RenderRadius = 6;
view.KeepAliveRadius = 8;
view.PreloadExtraRadius = 1;
view.FollowTarget = playerTransform;

// 把玩家落地（推荐用 FindNearestLandSpawn 避免水底）：
var land = view.FindNearestLandSpawn(0, 0);
view.Warmup(new Vector3(land.x, 0, land.z), radius: 2);   // 同步预生成 mesh + collider
playerTransform.position = new Vector3(land.x + 0.5f, land.y + 0.05f, land.z + 0.5f);
```

**流式调度**（与 2D MapView 同构）：
- 焦点变化 / 半径变化 → `_streamingDirty=true`
- `RebuildStreamingState`：算 desired chunks、卸载越 KeepAlive 的（走 `VoxelMap.UnloadChunk` → 触发事件链 → View 销毁 GO）、按 Chebyshev 距离重排数据/网格队列
- 每帧消费 `DataPerFrame` 个数据生成 + `MeshPerFrame` 个 Mesh 烘焙
- 数据队列覆盖 `RenderRadius + 1 + PreloadExtraRadius` 圈，Mesh 队列只覆盖 `RenderRadius` 圈

**性能**：每 chunk 单 Mesh + 单 Material（共享 atlas Texture），跨 chunk SRP Batcher 合批；侧面 per-voxel unit quad（per-tile 贴图正确，不串色）；半径 6 时 ~169 个 GO。

**业务侧扩展**：
- 自定义模板：`VoxelMapTemplateRegistry.Register(new MyVoxelTemplate())`，Inspector 切 TemplateId
- 自定义 Config：派生 `VoxelMapConfig` 并 override `CreateGenerator()` 返回自家生成器
- 装饰器（树/怪物 spawn）：`Voxel3DMapService.Instance.RegisterDecorator(new MyDecorator())`，与 2D `IChunkDecorator` 同样的 `(Id, Priority, Decorate)` 协议

**Phase 4 全部完成**：chunk 持久化（见下"3D 体素持久化"）+ spawn 子系统（见下"3D 体素 Spawn"）均已接入，与 2D 架构完全对齐。

### 3D 体素持久化（Phase 4a）

与 2D `MapPersistenceService` 平行的独立 IO 层。**不存地形**（生成器确定性派生），只存 column 差量。

```
{persistentDataPath}/VoxelMapData/{MapId}/
├── Meta.json                       VoxelMapMetaSaveData：MapId / ConfigId / ChunkSize / Seed / ConfigJsonSnapshot
└── Chunks/
    └── r_{rx}_{rz}.json            VoxelRegionSaveData：10×10 chunk 聚合
                                       Chunks[] → VoxelChunkSaveData → ColumnOverrides[]
```

**ColumnOverride** 结构：`{ LocalX, LocalZ, TopBlock, SideBlock, Height }` —— 一次记录玩家改过的 column 三值。

**业务 API**（在 `Voxel3DMapService` 上）：

```csharp
// 玩家把 (10, 0, 5) 处的 column 改为 stone（顶+侧）+ 高度 25
Voxel3DMapService.Instance.SetVoxelColumnOverride(
    "voxel_world", wx: 10, wz: 5,
    topBlock: VoxelBlockTypes.Stone,
    sideBlock: VoxelBlockTypes.Stone,
    height: 25);

// 重置某列（视觉不立刻还原 —— 下次卸载 + 重新加载才恢复生成器默认）
Voxel3DMapService.Instance.ClearVoxelColumnOverride("voxel_world", 10, 5);

// 立即写盘（dirty 才写）
Voxel3DMapService.Instance.SaveChunk("voxel_world", cx: 0, cz: 0);

// 清存档重开
Voxel3DMapService.Instance.DeleteMapData("voxel_world");
```

**写盘时机**：

| 时机 | 行为 |
|---|---|
| `OverrideColumn` / `ClearOverride` | 标 `chunk.IsDirty=true` |
| 区块卸载（`VoxelMap.ChunkUnloading` pre-event） | dirty → 异步写盘 |
| 自动 flush（`Voxel3DMapManager.Update` → `AutoSaveTick`） | 计时到 `AutoSaveIntervalSec`（默认 30s）→ 异步写最多 `AutoSaveMaxChunksPerTick`（默认 4）个 dirty chunk |
| 应用退出（`OnApplicationQuit`） | **同步**写全部 dirty chunk |
| 应用切后台（`OnApplicationFocus(false)`） | 异步 flush 一次防数据丢失 |

**线程模型**：JSON 序列化在主线程（小开销），文件 IO 走 `Task.Run` 后台线程；`_writeLock` 串行化；`.tmp` 原子重命名。

**Config 漂移**：`Meta.ConfigJsonSnapshot` 在 `CreateMap` 时与当前 `JsonUtility.ToJson` 比对。不一致仅 LogWarning，已有 chunk 文件保留原内容；新 chunk 用最新 Seed 生成。

### 3D 体素 Spawn（Phase 4b）

与 2D `EntitySpawnService` 平行的独立 spawn 子系统。**不与 2D 共享队列/destroyed 桶**，避免 mapId 冲突 + IsChunkLoaded 路由复杂化。

```
Voxel3D/Spawn/
├── VoxelEntitySpawnService.cs       Service：rule 注册 / destroyed 桶 / runtime 桶 / 分帧队列
├── VoxelEntitySpawnDecorator.cs     IVoxelChunkDecorator (priority=300，由 Voxel3DMapManager 自动注册)
└── Dao/
    ├── VoxelEntitySpawnRule.cs       TopBlockIds + SideBlockIds + HeightRange + 密度/cluster/spacing
    ├── VoxelEntitySpawnRuleSet.cs    一组规则；显式按 ConfigId 绑定
    └── IntRange.cs                   可选闭区间过滤器（高度等整数）
```

**用法**（玩家在草地上随机种树）：

```csharp
// 在业务 Manager.Initialize 里注册
VoxelEntitySpawnService.Instance.RegisterRuleSet(
    DefaultVoxelTemplate.Id,
    new VoxelEntitySpawnRuleSet("default-flora")
        .WithRule(new VoxelEntitySpawnRule("tree_oak", "env_tree_oak")
            .WithTopBlocks(VoxelBlockTypes.Grass)
            .WithHeightRange(13, 50)            // 海平面以上、雪线以下
            .WithDensity(0.04f)
            .WithMaxPerChunk(8)
            .WithMinSpacing(2)
            .WithRngTag("flora_tree")));
```

**spawn 落地点**：`(wx + 0.5, height + 1, wz + 0.5)` —— 站在 column 顶面之上一格。
**确定性**：与 2D 同构。所有 RNG 走 `ChunkSeed.Rng(mapId, cx, cz, rule.TileRngTag)`，规则按 (Priority asc, RuleId asc) 排序，区块内 (lz, lx) 行主序遍历。

**已破坏 spawn 持久化**（玩家砍树）：

```csharp
// 业务层砍树流程：先标记，再销毁 GameObject
Voxel3DMapService.Instance.MarkSpawnDestroyed(mapId, instanceId);
EventProcessor.Instance.TriggerEventMethod("DestroyEntity", new List<object> { instanceId });
```

`MarkSpawnDestroyed` 内部反向标 chunk dirty（`DirtyChunkLookup` 钩子），下次 unload / AutoSaveTick 时随 chunk 写盘到 `VoxelChunkSaveData.DestroyedSpawnIds`。重新加载时 `OnPostFillChunk` 调 `SeedDestroyed` 注入桶；装饰器查 `IsDestroyedInChunk` 命中即跳过。

**性能护栏**（Inspector 可调）：

| 参数 | 默认 | 作用 |
|---|---:|---|
| `VoxelEntitySpawnRule.MaxPerChunk` | 16 | 单规则单区块上限 |
| `VoxelEntitySpawnDecorator.GlobalMaxPerChunk` | 32 | 跨规则单区块总上限 |
| `Voxel3DMapManager._spawnEntitiesPerFrame` | 8 | spawn 队列每帧消费数 |

**InstanceId 格式**：
- 主体：`vspawn:{mapId}:{cx}:{cz}:{ruleId}:{lx}_{lz}`
- cluster：`vspawn:{mapId}:{cx}:{cz}:{ruleId}:{seedLx}_{seedLz}#{candLx}_{candLz}`

前缀 `vspawn:` 与 2D 的 `spawn:` 区分，便于离线工具一眼分辨。

详见各源文件顶部 XML doc。

## 文件结构

**v4 重构**：MapManager 内分为 `Common/` `TopDown2D/` `Voxel3D/` 三块，命名空间同步：
- 2D 内容：`EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.*`
- 3D 内容：`EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.*`
- 共享工具：`EssSystem.Core.Application.MultiManagers.MapManager.Common.*`

```
MapManager/
├── Agent.md                         本文档
│
├── Common/                          ★ 2D / 3D 共享工具
│   └── Util/
│       └── ChunkSeed.cs             (mapId, chunkCoord, tag) → 确定性子种子 / System.Random
│
├── TopDown2D/                       俯视 2D Tilemap 子系统（原 MapManager 全部）
│   ├── MapManager.cs                薄门面：默认注册 + Update 驱动（spawn tick + auto-save tick）
│   ├── MapService.cs                业务核心 + MapView 工厂 + 持久化集成 + 8 类 API
│   ├── Runtime/
│   │   └── MapView.cs               Tilemap 渲染 + 流式预加载
│   ├── Persistence/                 区块级存档底层（10×10 region 聚合）
│   │   ├── MapPersistenceService.cs path 管理 + region 缓存 + 异步/同步写
│   │   └── Dao/                     ChunkSaveData / RegionSaveData / MapMetaSaveData / TileOverride / PlacedSpawn
│   ├── Spawn/                       实体生成子系统
│   │   ├── EntitySpawnService.cs    规则集 + destroyed 桶 + 分帧 spawn 队列
│   │   ├── EntitySpawnDecorator.cs  IChunkDecorator
│   │   ├── IMapMetaProvider.cs      可选 (BiomeId / Elevation / Temp / Moist) 元数据
│   │   └── Dao/                     EntitySpawnRule / RuleSet / FloatRange / TileMeta
│   ├── Editor/
│   │   └── MapManagerEditor.cs      Inspector 自定义
│   └── Dao/
│       ├── Tile.cs / Chunk.cs / Map.cs / TileType.cs
│       ├── Config/                  MapConfig (抽象) + PerlinMapConfig (旧 AQN 兼容)
│       ├── Generator/               IMapGenerator + IChunkDecorator 接口
│       └── Templates/               模板策略层（IMapTemplate + Registry）
│           ├── TopDownRandom/       俯视随机大世界（Perlin + 群系 + 河流）
│           └── SideScrollerRandom/  横版骨架（fBm 水平地表 + 土/石/基岩分层）
│
└── Voxel3D/                         ★ 体素 3D 子系统（与 TopDown2D 平行）
    ├── Voxel3DMapManager.cs         薄门面（[Manager(13)]）
    ├── Voxel3DMapService.cs         业务核心 + MapView 工厂 + 持久化 + spawn 集成
    ├── Runtime/
    │   ├── Voxel3DMapView.cs        chunk GO 池 + 单 mesh 渲染 + 流式
    │   ├── VoxelMaterialFactory.cs  共享 Material + atlas
    │   └── VoxelTextureAtlas.cs     6×6 atlas
    ├── Generator/
    │   ├── IVoxelMapGenerator.cs / IVoxelChunkDecorator.cs
    │   ├── VoxelHeightmapGenerator.cs   Perlin 多倍频 height field
    │   └── VoxelChunkMesher.cs           greedy + per-voxel side quads
    ├── Persistence/
    │   ├── VoxelMapPersistenceService.cs region 聚合（不存地形，只存 column 差量）
    │   └── Dao/                     VoxelChunkSaveData / VoxelRegionSaveData / VoxelMapMetaSaveData / VoxelColumnOverride
    ├── Spawn/
    │   ├── VoxelEntitySpawnService.cs    独立队列/destroyed/runtime 桶
    │   ├── VoxelEntitySpawnDecorator.cs  IVoxelChunkDecorator
    │   └── Dao/                     VoxelEntitySpawnRule / RuleSet / IntRange
    └── Dao/
        ├── VoxelMap.cs / VoxelChunk.cs       3D 数据结构 + IsDirty + OverrideColumn
        ├── VoxelMapConfig.cs                  ChunkSize/Seed/SeaLevel/SnowLine 等
        ├── VoxelBlockType.cs / VoxelBlockTypes.cs   palette + 7 默认方块常量
        └── Templates/
            ├── IVoxelMapTemplate.cs / VoxelMapTemplateRegistry.cs
            └── DefaultVoxel/DefaultVoxelTemplate.cs  默认模板
```

**注意事项**：
- C# 类名 `MapManager` 与命名空间末段 `MapManager.TopDown2D` 不冲突（class `MapManager` ∈ ns `MapManager.TopDown2D`），仅 FQN 看起来递归
- 现有 `{persistentDataPath}/ServiceData/*.json` 里的 AQN 仍指向旧命名空间（如 `MapManager.Dao.Templates.TopDownRandom.Config.PerlinMapConfig`），**重构后无法反序列化**。开发期建议直接清理 `{persistentDataPath}/ServiceData/MapService/` 与 `MapData/` 目录重新生成；生产环境需要在 `LegacyTypeResolver` 加 FormerName 映射

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
// §4.1 跨模块 bare-string：ResourceManager.EVT_GET_RULE_TILE
var r = EventProcessor.Instance.TriggerEventMethod(
    "GetRuleTile",
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

## 区块级持久化（v3：region 聚合）

每张地图按 **10×10 chunk** 联合存储**差量数据**（不存地形 —— 地形由生成器确定性派生）。比 v2 “一 chunk 一文件”大幅减少小文件 + inode 压力：

```
{persistentDataPath}/MapData/{mapId}/
├── Meta.json                       MapMetaSaveData：MapId / ConfigId / ChunkSize / Seed / ConfigJsonSnapshot
└── Chunks/
    ├── r_0_0.json                  RegionSaveData：Chunks[] 中含该 region 内需存的所有 ChunkSaveData
    ├── r_0_-1.json
    └── ...
```

区域坐标 `(rx, ry) = (floor(cx/10), floor(cy/10))`。`MapPersistenceService.REGION_SIZE = 10` 可调。
运行期用 `_regionCache` 避免每次 chunk 读/写都重加载文件。LoadChunk 首次同步读盘加载所在 region，
后续同 region 的 chunk 访问都走内存；SaveChunk 更新内存中的 region 后同步生成 JSON，异步原子写盘。

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
// §4.1 跨模块 bare-string：EntityManager.EVT_DESTROY_ENTITY
EventProcessor.Instance.TriggerEventMethod("DestroyEntity",
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
