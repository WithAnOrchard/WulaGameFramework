using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.EssManager.MapManager.Dao;
using EssSystem.EssManager.MapManager.Dao.Config;
using EssSystem.EssManager.MapManager.Dao.Generator;
using EssSystem.EssManager.MapManager.Runtime;

namespace EssSystem.EssManager.MapManager
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
            Log("MapService 初始化完成", Color.green);
        }

        /// <summary>
        /// 热重载 Service 数据 — 仅清空配置部分，保留运行时地图实例。
        /// </summary>
        public void ReloadData()
        {
            if (_dataStorage.ContainsKey(CAT_CONFIGS))
            {
                _dataStorage[CAT_CONFIGS].Clear();
            }

            LoadData();

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
            // 生成管线：FillChunk → 装饰器 → 对外广播 ChunkGenerated
            map.ChunkGenerated += OnMapChunkGenerated;
            map.ChunkUnloaded += OnMapChunkUnloaded;
            _maps[mapId] = map;
            Log($"创建地图实例: {mapId} (config={configId}, chunkSize={cfg.ChunkSize})", Color.cyan);
            MapCreated?.Invoke(map);
            return map;
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

        private void OnMapChunkUnloaded(Map map, int cx, int cy)
        {
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
            map.UnloadAll();         // 会逐个区块触发 ChunkUnloaded，业务层可清 spawn 实体
            map.ChunkGenerated -= OnMapChunkGenerated;
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
    }
}
