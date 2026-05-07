using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Generator;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Persistence;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Persistence.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Runtime;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Spawn;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager
{
    /// <summary>
    /// 地图业务服务（骨架）。
    /// <list type="bullet">
    /// <item>所有持久化数据走 <c>_dataStorage</c>（由 DataService 自动扫描存档）</item>
    /// <item>运行时实例化的地图视图走内存字典，不参与序列化</item>
    /// <item>对外接口以纯 C# API 暴露；如需跨模块调度，再按需在方法上加 <c>[Event(EVT_XXX)]</c></item>
    /// </list>
    /// </summary>
    public class MapService : Service<MapService>
    {
        #region 数据分类（存储在 _dataStorage 中，自动持久化）

        /// <summary>地图配置（<see cref="MapConfig"/> 派生类，多态由 AQN 还原）。</summary>
        public const string CAT_CONFIGS = "Configs";

        #endregion

        /// <summary>运行时地图实例（不持久化）。MapId → <see cref="Map"/>。</summary>
        private readonly Dictionary<string, Map> _maps = new();

        /// <summary>底层持久化（懒缓存），由 <see cref="Initialize"/> 设置。</summary>
        private MapPersistenceService _persistence;

        /// <summary>Spawn 子系统（懒缓存）。</summary>
        private EntitySpawnService _spawnService;

        /// <summary>自动 flush 计时器。</summary>
        private float _autoSaveTimer;

        /// <summary>自动写盘间隔（秒）。&lt;= 0 关闭。默认 30s。由 <c>MapManager.Update</c> 驱动 <see cref="AutoSaveTick"/>。</summary>
        public float AutoSaveIntervalSec { get; set; } = 30f;

        /// <summary>每次自动 flush 最多写盘的 chunk 数（防 IO 雪崩）。默认 4。</summary>
        public int AutoSaveMaxChunksPerTick { get; set; } = 4;

        /// <summary>Tile 类型元数据注册表（不持久化）。TypeId → <see cref="TileTypeDef"/>。</summary>
        private readonly Dictionary<string, TileTypeDef> _tileTypes = new();

        /// <summary>已创建的 MapView（不持久化）。MapId → <see cref="MapView"/>。</summary>
        private readonly Dictionary<string, MapView> _mapViews = new();

        /// <summary>
        /// 全局区块装饰器列表（按 <see cref="IChunkDecorator.Priority"/> 升序）。
        /// 注册后对所有 <see cref="CreateMap"/> 之后创建的新 Map 都生效；
        /// 已经在运行的 Map 在其 <see cref="Map.ChunkGenerated"/> 触发时也会读到最新列表。
        /// </summary>
        private readonly List<IChunkDecorator> _decorators = new();

        // ─────────────────────────────────────────────────────────────
        #region 生成管线事件（装饰器跑完后对外广播的最终态）

        /// <summary>新区块生成 + 所有装饰器执行完毕后触发。业务层 spawn 植物/生物时订阅这里。</summary>
        public event Action<Map, Chunk> ChunkGenerated;

        /// <summary>区块被卸载后触发。业务层据此 despawn 之前 spawn 的实体。</summary>
        public event Action<Map, int, int> ChunkUnloaded;

        /// <summary><see cref="CreateMap"/> 新建一张地图后触发。</summary>
        public event Action<Map> MapCreated;

        /// <summary><see cref="DestroyMap"/> 销毁地图后触发（先于 MapView 销毁日志输出）。</summary>
        public event Action<string> MapDestroyed;

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            _persistence = MapPersistenceService.Instance;
            _spawnService = EntitySpawnService.Instance;
            // 让 EntitySpawnService 在改 destroyed 集合时反向标 chunk dirty（避免反向 using）
            _spawnService.DirtyChunkLookup = OnSpawnServiceRequestChunkDirty;
            Log("MapService 初始化完成", Color.green);
        }

        private void OnSpawnServiceRequestChunkDirty(string mapId, int cx, int cy)
        {
            var chunk = GetMap(mapId)?.PeekChunk(cx, cy);
            if (chunk != null) chunk.MarkDirty();
        }

        /// <summary>
        /// 热重载 Service 数据 — 仅清空配置部分，保留运行时地图实例。
        /// </summary>
        public void ReloadData()
        {
            // 只清空配置数据，保留运行时地图实例
            if (_dataStorage.TryGetValue(CAT_CONFIGS, out var cfg)) cfg.Clear();

            LoadData();
            // M2: 直接 mutate _dataStorage 了，手动标 Inspector dirty（与 Inventory/CharacterService 一致）。
            MarkInspectorDirty();

            Log("MapService 配置热重载完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region Config CRUD

        /// <summary>注册或覆盖地图配置（按 ConfigId 去重，写盘）。</summary>
        public void RegisterConfig(MapConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空配置或缺 ConfigId 的配置");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册地图配置: {config.ConfigId} ({config.DisplayName}) [{config.GetType().Name}]", Color.blue);
        }

        /// <summary>按 ConfigId 取配置（基类返回，按需向下转型）。</summary>
        public MapConfig GetConfig(string configId) =>
            GetData<MapConfig>(CAT_CONFIGS, configId);

        /// <summary>枚举所有配置。</summary>
        public IEnumerable<MapConfig> GetAllConfigs()
        {
            foreach (var key in GetKeys(CAT_CONFIGS))
            {
                var cfg = GetConfig(key);
                if (cfg != null) yield return cfg;
            }
        }

        /// <summary>删除配置。运行中的地图实例不受影响（已绑定生成器）。</summary>
        public bool RemoveConfig(string configId) => RemoveData(CAT_CONFIGS, configId);

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Map 实例管理（仅内存）

        /// <summary>
        /// 创建运行时地图实例。同 MapId 已存在时直接返回原实例（不重建生成器）。
        /// </summary>
        public Map CreateMap(string mapId, string configId)
        {
            if (string.IsNullOrEmpty(mapId))
            {
                LogWarning("CreateMap: mapId 为空");
                return null;
            }
            if (_maps.TryGetValue(mapId, out var existing)) return existing;

            var cfg = GetConfig(configId);
            if (cfg == null)
            {
                LogWarning($"CreateMap: 配置不存在 {configId}");
                return null;
            }

            var map = new Map(mapId, configId, cfg.ChunkSize, cfg.CreateGenerator());
            // 生成管线：FillChunk → PostFillHook(读盘+应用差量) → 装饰器 → 对外广播 ChunkGenerated
            map.PostFillHook = OnPostFillChunk;
            map.ChunkGenerated += OnMapChunkGenerated;
            map.ChunkUnloading += OnMapChunkUnloading;
            map.ChunkUnloaded += OnMapChunkUnloaded;
            _maps[mapId] = map;

            // 校验 / 写入 Meta（不一致仅警告，按用户决策保留旧 chunk 文件、新 chunk 用最新配置生成）
            EnsureMapMeta(mapId, configId, cfg);

            Log($"创建地图实例: {mapId} (config={configId}, chunkSize={cfg.ChunkSize})", Color.cyan);
            MapCreated?.Invoke(map);
            return map;
        }

        /// <summary>读取已有 Meta，与当前配置 JSON 比对；不一致仅 LogWarning，统一更新落盘。</summary>
        private void EnsureMapMeta(string mapId, string configId, MapConfig cfg)
        {
            if (_persistence == null) return;
            var currentJson = JsonUtility.ToJson(cfg);
            var existing = _persistence.LoadMeta(mapId);
            if (existing != null && !string.IsNullOrEmpty(existing.ConfigJsonSnapshot)
                && existing.ConfigJsonSnapshot != currentJson)
            {
                LogWarning($"地图 '{mapId}' 配置已变更（与 Meta 快照不一致）。已有 chunk 文件保留原内容；新 chunk 用最新配置生成（边界可能割裂）。");
            }
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var meta = existing ?? new MapMetaSaveData(mapId, configId, cfg.ChunkSize, ExtractSeed(cfg))
            {
                CreatedAtUnixMs = nowMs,
            };
            meta.MapId = mapId;
            meta.ConfigId = configId;
            meta.ChunkSize = cfg.ChunkSize;
            meta.Seed = ExtractSeed(cfg);
            meta.ConfigJsonSnapshot = currentJson;
            _persistence.SaveMetaAsync(meta);
        }

        /// <summary>反射读取 MapConfig 派生类的 public <c>Seed</c> int 字段（PerlinMapConfig 等通用约定）；找不到返回 0。</summary>
        private static int ExtractSeed(MapConfig cfg)
        {
            if (cfg == null) return 0;
            var t = cfg.GetType();
            var f = t.GetField("Seed", BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int))
            {
                try { return (int)f.GetValue(cfg); } catch { /* ignore */ }
            }
            return 0;
        }

        /// <summary>Map 端事件 → 跑装饰器 → 向业务层广播最终态。</summary>
        private void OnMapChunkGenerated(Map map, Chunk chunk)
        {
            // 先跑装饰器（植物 / 生物 / 结构）。每个装饰器异常隔离，不影响后续。
            // 迭代副本，允许装饰器在自己的回调里安全注册/注销其它装饰器。
            if (_decorators.Count > 0)
            {
                var snapshot = _decorators.ToArray();
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var d = snapshot[i];
                    try { d.Decorate(map, chunk); }
                    catch (Exception ex)
                    {
                        LogWarning($"IChunkDecorator '{d.Id}' 在 ({chunk.ChunkX},{chunk.ChunkY}) 抛异常: {ex.Message}");
                    }
                }
            }
            // 装饰完再对外广播 —— 业务层收到的一定是"地形 + 所有装饰"齐备的最终态。
            ChunkGenerated?.Invoke(map, chunk);
        }

        /// <summary>区块刚 FillChunk 完成、装饰器跑之前：读盘 + 应用 TileOverride + 注入 destroyed 集合。</summary>
        private void OnPostFillChunk(Map map, Chunk chunk)
        {
            if (_persistence == null) return;
            try
            {
                var save = _persistence.LoadChunk(map.MapId, chunk.ChunkX, chunk.ChunkY);
                if (save == null) return;
                if (save.TileOverrides != null && save.TileOverrides.Count > 0)
                    chunk.ApplyOverrides(save.TileOverrides);
                if (save.DestroyedSpawnIds != null && save.DestroyedSpawnIds.Count > 0
                    && _spawnService != null)
                    _spawnService.SeedDestroyed(map.MapId, chunk.ChunkX, chunk.ChunkY, save.DestroyedSpawnIds);
            }
            catch (Exception ex)
            {
                LogWarning($"OnPostFillChunk 失败 ({chunk.ChunkX},{chunk.ChunkY}): {ex.Message}");
            }
        }

        /// <summary>区块即将卸载（仍可访问）：dirty 即写盘 + 销毁 runtime 实体。</summary>
        private void OnMapChunkUnloading(Map map, Chunk chunk)
        {
            try
            {
                if (chunk.IsDirty) SaveChunkInternal(map.MapId, chunk, sync: false);
            }
            catch (Exception ex)
            {
                LogWarning($"unload save 失败 ({chunk.ChunkX},{chunk.ChunkY}): {ex.Message}");
            }
            // 在 chunk 仍有效时清 spawn 实体（runtime 桶按 chunkkey 索引；这里做销毁 GameObject）
            _spawnService?.DestroyChunkRuntimeEntities(map.MapId, chunk.ChunkX, chunk.ChunkY);
        }

        /// <summary>区块已被移出字典：丢 destroyed 桶 + 转发对外事件。</summary>
        private void OnMapChunkUnloaded(Map map, int cx, int cy)
        {
            _spawnService?.DropChunkBuckets(map.MapId, cx, cy);
            ChunkUnloaded?.Invoke(map, cx, cy);
        }

        /// <summary>按 MapId 查询运行时实例，不存在返回 null。</summary>
        public Map GetMap(string mapId) =>
            !string.IsNullOrEmpty(mapId) && _maps.TryGetValue(mapId, out var m) ? m : null;

        /// <summary>枚举所有运行时地图实例。</summary>
        public IEnumerable<Map> GetAllMaps() => _maps.Values;

        /// <summary>销毁运行时地图实例（不影响磁盘上的配置）。</summary>
        public bool DestroyMap(string mapId)
        {
            if (!_maps.TryGetValue(mapId, out var map)) return false;
            DestroyMapView(mapId);   // 关联视图先清，避免悬挂引用
            map.UnloadAll();         // ChunkUnloading 会逐块触发存盘 + 实体清理；ChunkUnloaded 丢桶
            map.PostFillHook = null;
            map.ChunkGenerated -= OnMapChunkGenerated;
            map.ChunkUnloading -= OnMapChunkUnloading;
            map.ChunkUnloaded -= OnMapChunkUnloaded;
            _maps.Remove(mapId);
            Log($"销毁地图实例: {mapId}", Color.yellow);
            MapDestroyed?.Invoke(mapId);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 区块装饰器注册（植物 / 生物 / 结构 / 道具 …）

        /// <summary>
        /// 注册一个区块装饰器。按 <see cref="IChunkDecorator.Priority"/> 升序插入，
        /// 同 <see cref="IChunkDecorator.Id"/> 已存在则覆盖（便于热替换调优）。
        /// 注册后：所有后续生成的新区块都会依次跑这些装饰器；已在内存里的老区块不会重跑。
        /// </summary>
        public void RegisterDecorator(IChunkDecorator decorator)
        {
            if (decorator == null || string.IsNullOrEmpty(decorator.Id))
            {
                LogWarning("RegisterDecorator: decorator 为空或缺 Id");
                return;
            }
            // 去重：同 Id 先移除再插入，保证覆盖而非重复注册
            for (var i = _decorators.Count - 1; i >= 0; i--)
                if (_decorators[i].Id == decorator.Id) _decorators.RemoveAt(i);

            // 按 Priority 升序插入
            var idx = 0;
            while (idx < _decorators.Count && _decorators[idx].Priority <= decorator.Priority) idx++;
            _decorators.Insert(idx, decorator);
            Log($"注册区块装饰器: {decorator.Id} (priority={decorator.Priority})", Color.blue);
        }

        /// <summary>按 Id 注销装饰器；返回是否命中。</summary>
        public bool UnregisterDecorator(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (var i = _decorators.Count - 1; i >= 0; i--)
            {
                if (_decorators[i].Id != id) continue;
                _decorators.RemoveAt(i);
                Log($"注销区块装饰器: {id}", Color.yellow);
                return true;
            }
            return false;
        }

        /// <summary>枚举当前已注册的装饰器（按 Priority 升序）。</summary>
        public IReadOnlyList<IChunkDecorator> GetDecorators() => _decorators;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 便利访问（透传到 Map 实例）

        /// <summary>取/生成区块（mapId 不存在或参数无效返回 null）。</summary>
        public Chunk GetOrGenerateChunk(string mapId, int chunkX, int chunkY) =>
            GetMap(mapId)?.GetOrGenerateChunk(chunkX, chunkY);

        /// <summary>取 Tile（必要时触发区块生成）。</summary>
        public Dao.Tile GetTile(string mapId, int tileX, int tileY) =>
            GetMap(mapId)?.GetTile(tileX, tileY);

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region TileType 注册表（仅内存）

        /// <summary>注册或覆盖 Tile 类型元数据（按 TypeId 去重）。</summary>
        public void RegisterTileType(TileTypeDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.TypeId))
            {
                LogWarning("忽略空 TileTypeDef 或缺 TypeId 的定义");
                return;
            }
            _tileTypes[def.TypeId] = def;
            Log($"注册 TileType: {def.TypeId} → {def.RuleTileResourceId}", Color.blue);
        }

        /// <summary>按 TypeId 取定义；未注册返回 null。</summary>
        public TileTypeDef GetTileType(string typeId) =>
            !string.IsNullOrEmpty(typeId) && _tileTypes.TryGetValue(typeId, out var d) ? d : null;

        /// <summary>枚举所有已注册 TileType。</summary>
        public IEnumerable<TileTypeDef> GetAllTileTypes() => _tileTypes.Values;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region MapView 工厂

        /// <summary>
        /// 为已创建的 <see cref="Map"/> 自动构建 Grid + Tilemap + <see cref="MapView"/> 树并绑定。
        /// 同 MapId 已存在视图时直接返回原视图（不重建）。
        /// </summary>
        /// <param name="mapId">必须先通过 <see cref="CreateMap"/> 创建过</param>
        /// <param name="parent">视图根的父级（null = 场景根）</param>
        /// <returns>绑定好的 <see cref="MapView"/>，地图不存在时返回 null</returns>
        public MapView CreateMapView(string mapId, Transform parent = null)
        {
            if (_mapViews.TryGetValue(mapId, out var existing) && existing != null)
                return existing;

            var map = GetMap(mapId);
            if (map == null)
            {
                LogWarning($"CreateMapView: 地图不存在 {mapId}");
                return null;
            }

            // 根: Grid + MapView
            var rootGo = new GameObject($"MapView_{mapId}");
            if (parent != null) rootGo.transform.SetParent(parent, false);
            rootGo.AddComponent<Grid>();

            // 子: Tilemap + TilemapRenderer
            var tilemapGo = new GameObject("Tilemap");
            tilemapGo.transform.SetParent(rootGo.transform, false);
            var tilemap = tilemapGo.AddComponent<Tilemap>();
            tilemapGo.AddComponent<TilemapRenderer>();

            // MapView (RequireComponent(Grid) 已满足)
            var view = rootGo.AddComponent<MapView>();
            view.Bind(map, tilemap);

            _mapViews[mapId] = view;
            Log($"创建 MapView: {mapId}", Color.cyan);
            return view;
        }

        /// <summary>查询已创建的 MapView，无返回 null。</summary>
        public MapView GetMapView(string mapId) =>
            !string.IsNullOrEmpty(mapId) && _mapViews.TryGetValue(mapId, out var v) ? v : null;

        /// <summary>销毁 MapView 的 GameObject 并清缓存（不影响 Map 数据）。</summary>
        public bool DestroyMapView(string mapId)
        {
            if (!_mapViews.TryGetValue(mapId, out var view)) return false;
            if (view != null && view.gameObject != null) UnityEngine.Object.Destroy(view.gameObject);
            _mapViews.Remove(mapId);
            Log($"销毁 MapView: {mapId}", Color.yellow);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Tile Override（玩家修改地块）

        /// <summary>
        /// 玩家显式覆盖某 Tile 的 TypeId（仅改 <see cref="Dao.Tile.TypeId"/>，保留 Elevation/Temperature/Moisture/RiverFlow）。
        /// <para>区块若未加载会先生成；写入会标 chunk dirty，由 AutoSave / Unload 触发写盘。</para>
        /// </summary>
        public bool SetTileOverride(string mapId, int worldX, int worldY, string typeId)
        {
            var map = GetMap(mapId);
            if (map == null) return false;
            var (cx, cy, lx, ly) = SplitWorldTile(worldX, worldY, map.ChunkSize);
            var chunk = map.GetOrGenerateChunk(cx, cy);
            chunk.OverrideTile(lx, ly, typeId);
            return true;
        }

        /// <summary>
        /// 取消某格的 override 记录（视觉上不立刻还原，区块下次重新加载时恢复生成器默认输出）。
        /// </summary>
        public bool ClearTileOverride(string mapId, int worldX, int worldY)
        {
            var map = GetMap(mapId);
            if (map == null) return false;
            var (cx, cy, lx, ly) = SplitWorldTile(worldX, worldY, map.ChunkSize);
            var chunk = map.PeekChunk(cx, cy);
            return chunk != null && chunk.ClearOverride(lx, ly);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Spawn Destroyed（已破坏 spawn 标记 — 转发到 EntitySpawnService）

        /// <summary>标记某 spawn 实体已永久破坏（业务砍树/采花前调；区块重新加载不会复活）。</summary>
        public bool MarkSpawnDestroyed(string mapId, string instanceId) =>
            _spawnService != null && _spawnService.MarkDestroyed(mapId, instanceId);

        /// <summary>移除"已破坏"标记，下次区块加载将恢复 spawn。</summary>
        public bool UnmarkSpawnDestroyed(string mapId, string instanceId) =>
            _spawnService != null && _spawnService.UnmarkDestroyed(mapId, instanceId);

        /// <summary>查询某 spawn 实体是否已被永久破坏。</summary>
        public bool IsSpawnDestroyed(string mapId, string instanceId) =>
            _spawnService != null && _spawnService.IsDestroyed(mapId, instanceId);

        /// <summary>清空指定区块所有"已破坏"记录（调试 / 重置生态用）。</summary>
        public void ClearDestroyedSpawnsInChunk(string mapId, int cx, int cy) =>
            _spawnService?.ClearDestroyedInChunk(mapId, cx, cy);

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 持久化控制

        /// <summary>强制写盘单个区块。chunk 未加载时无操作。</summary>
        public void SaveChunk(string mapId, int cx, int cy, bool sync = false)
        {
            var chunk = GetMap(mapId)?.PeekChunk(cx, cy);
            if (chunk == null) return;
            SaveChunkInternal(mapId, chunk, sync);
        }

        /// <summary>把指定地图所有 dirty chunk 全部写盘。<paramref name="sync"/> = true 会阻塞主线程。</summary>
        public int SaveAllDirtyChunks(string mapId, bool sync = false)
        {
            var map = GetMap(mapId);
            if (map == null) return 0;
            var written = 0;
            foreach (var kv in map.LoadedChunks)
            {
                var chunk = kv.Value;
                if (!chunk.IsDirty) continue;
                SaveChunkInternal(mapId, chunk, sync);
                written++;
            }
            return written;
        }

        /// <summary>遍历所有运行中地图，把所有 dirty chunk 写盘（应用退出兜底）。</summary>
        public int SaveAllDirtyChunksAllMaps(bool sync = false)
        {
            var total = 0;
            foreach (var map in _maps.Values)
                total += SaveAllDirtyChunks(map.MapId, sync);
            return total;
        }

        /// <summary>删除指定 MapId 的全部存档（Meta + Chunks）。运行中实例不变但下次进区块即"全新"。</summary>
        public bool DeleteMapData(string mapId) =>
            _persistence != null && _persistence.DeleteMapData(mapId);

        /// <summary>取地图存档目录绝对路径（调试 / 备份用）。</summary>
        public string GetMapDataPath(string mapId) =>
            _persistence?.MapDir(mapId);

        /// <summary>由 <c>MapManager.Update</c> 每帧调用：累加计时器，到点后异步 flush 一批 dirty chunk。</summary>
        public void AutoSaveTick(float deltaTime)
        {
            if (AutoSaveIntervalSec <= 0f) return;
            _autoSaveTimer += deltaTime;
            if (_autoSaveTimer < AutoSaveIntervalSec) return;
            _autoSaveTimer = 0f;
            FlushDirtyBudget(AutoSaveMaxChunksPerTick);
        }

        /// <summary>遍历所有运行中地图，最多写盘 <paramref name="maxChunks"/> 个 dirty chunk；返回实际写入数。</summary>
        public int FlushDirtyBudget(int maxChunks)
        {
            if (maxChunks <= 0) return 0;
            var written = 0;
            foreach (var map in _maps.Values)
            {
                if (written >= maxChunks) break;
                foreach (var kv in map.LoadedChunks)
                {
                    if (written >= maxChunks) break;
                    var chunk = kv.Value;
                    if (!chunk.IsDirty) continue;
                    SaveChunkInternal(map.MapId, chunk, sync: false);
                    written++;
                }
            }
            return written;
        }

        /// <summary>把 chunk 当前的 override + spawn destroyed 集合打包为 ChunkSaveData，按 sync 选择同/异步写盘。</summary>
        private void SaveChunkInternal(string mapId, Chunk chunk, bool sync)
        {
            if (_persistence == null || chunk == null) return;
            var save = new ChunkSaveData(mapId, chunk.ChunkX, chunk.ChunkY)
            {
                TileOverrides = chunk.EnumerateOverrides(),
            };
            var destroyed = _spawnService?.GetDestroyedIds(mapId, chunk.ChunkX, chunk.ChunkY);
            if (destroyed != null && destroyed.Count > 0)
            {
                save.DestroyedSpawnIds = new List<string>(destroyed);
            }
            if (sync) _persistence.SaveChunkSync(save);
            else _persistence.SaveChunkAsync(save);
            chunk.ClearDirty();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 工具

        private static (int cx, int cy, int lx, int ly) SplitWorldTile(int worldX, int worldY, int chunkSize)
        {
            var cx = FloorDiv(worldX, chunkSize);
            var cy = FloorDiv(worldY, chunkSize);
            return (cx, cy, worldX - cx * chunkSize, worldY - cy * chunkSize);
        }

        private static int FloorDiv(int a, int b)
        {
            var q = a / b;
            if ((a % b) != 0 && ((a < 0) ^ (b < 0))) q--;
            return q;
        }

        #endregion
    }
}
