# MapManager 指南

## 概述

`MapManager`（`[Manager(12)]`，薄门面）+ `MapService`（业务核心 + 配置持久化）提供**多模板**的 2D 地图系统：

- **通用层**：`Tile` / `Chunk` / `Map`（运行时容器与坐标） + `MapConfig` 抽象基类 + `IMapGenerator` 策略接口
- **模板层**：每种地形玩法实现一对 `XxxMapConfig` + `XxxMapGenerator`，互不耦合
- **持久化**：`MapConfig` 派生类走 `_dataStorage`（多态由 AQN 还原），运行时 `Map` 实例仅内存

## 文件结构

```
MapManager/
├── MapManager.cs                    薄门面：默认注册 + 生命周期
├── MapService.cs                    业务核心 + MapView 工厂（纯 C# API；按需再补 [Event] 包装）
├── Agent.md                         本文档
├── Runtime/
│   └── MapView.cs                   MonoBehaviour：Map → Grid + Tilemap 渲染（含 TypeId→RuleTile 缓存）
└── Dao/
    ├── Tile.cs                      单格（TypeId 字符串）
    ├── Chunk.cs                     Size×Size 扁平数组容器 + 区块坐标
    ├── Map.cs                       运行时实例：MapId/ConfigId/ChunkSize + 懒生成区块字典
    ├── TileType.cs                  TypeId 常量 + 默认 RuleTile 常量 + TileTypeDef 元数据类
    ├── Config/
    │   ├── MapConfig.cs             抽象基类：ConfigId + DisplayName + ChunkSize + CreateGenerator()
    │   └── PerlinMapConfig.cs       兼容旧 AQN 的薄包装，实际逻辑在 TopDownRandom
    └── Generator/
    │   ├── IMapGenerator.cs         通用策略接口：FillChunk(chunk)
    │   └── IChunkDecorator.cs       装饰器接口：FillChunk 后的植物/生物/结构派生
    ├── Util/
    │   └── ChunkSeed.cs             (mapId, chunkCoord, tag) → 确定性子种子 / System.Random
    └── Templates/
        └── TopDownRandom/           2D 平面随机生成模板（MC-like 四参数 + Biome + River）
            ├── Config/
            │   └── PerlinMapConfig.cs
            └── Generator/
                ├── PerlinMapGenerator.cs
                ├── BiomeClassifier.cs
                └── RiverTracer.cs
```

## 坐标系约定

- **世界 Tile 坐标** `(tileX, tileY)`：整数，全局唯一。
- **区块坐标** `(chunkX, chunkY)`：`chunkX = floor(tileX / Size)`，向下取整支持负坐标。
- **本地 Tile 坐标** `(lx, ly)`：`lx = tileX - chunkX * Size`，范围 `[0, Size)`。
- **数组排布**：`Chunk.Tiles[ly * Size + lx]`（行主序）。

## 内置默认配置

`MapManager` 启动时自动注册（同 ConfigId 已存在则跳过；注册前经 `JsonUtility` 克隆解耦 Inspector）：

| ConfigId | 类型 | 说明 |
|---|---|---|
| `"PerlinIsland"` | `TopDownRandom.PerlinMapConfig` | 16×16 区块，Seed=20240501，MC-like 四参数地形 + Biome + River |

**默认值技术预设：大块大陆 + 大片海洋**（`SeaLevel=0.58` / `ContinentalnessScale=0.0008` / `ContinentScale=0.0008` / `ContinentalnessWeight=0.88`），想再减少小岛、让大陆更连成片：继续同向调（seaLevel↑ / continent↓ / weight↑）即可。

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

`MapManager.RegisterDefaultTileTypes`（启动时**总是**调用，不受 `_registerDebugTemplates` 控制）：

| TypeId | DisplayName | RuleTileResourceId | 对应资产 |
|---|---|---|---|
| `TileTypes.Ocean` = `"ocean"` | 海洋 | `"GrasslandsWaterTiles"` | `Resources/Tiles/GrasslandsWaterTiles.asset` |
| `TileTypes.Land` = `"land"` | 陆地 | `"GrasslandsGround"` | `Resources/Tiles/GrasslandsGround.asset` |

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
            if (t.TypeId != TileTypes.Forest) continue;
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
