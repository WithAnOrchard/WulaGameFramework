using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Persistence;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Persistence.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Runtime;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Spawn;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D
{
    /// <summary>
    /// 3D 体素地图业务服务（与 2D <c>MapService</c> 平行）。
    /// <list type="bullet">
    /// <item>持久化数据：<see cref="VoxelMapConfig"/> 注册表（CAT_CONFIGS）走 <c>_dataStorage</c></item>
    /// <item>运行时数据：<see cref="VoxelMap"/> 实例 / <see cref="VoxelBlockType"/> 调色板 / 装饰器列表 仅内存</item>
    /// <item>对外接口：纯 C# API；跨模块需要时再按需加 Event 特性</item>
    /// </list>
    /// <para>**Phase 2 范围**：Config CRUD + Map 实例管理 + BlockType palette + 装饰器注册。
    /// 持久化（chunk 写盘 / spawn 差量）留 Phase 3 接入，与 2D <c>MapPersistenceService</c> 平行实现。</para>
    /// </summary>
    public class Voxel3DMapService : Service<Voxel3DMapService>
    {
        // ─── 数据分类（持久化）─────────────────────────────────────
        public const string CAT_CONFIGS = "Configs";

        // ─── 运行时（不持久化）─────────────────────────────────────
        private readonly Dictionary<string, VoxelMap> _maps = new();
        private readonly Dictionary<byte, VoxelBlockType> _blockTypes = new();
        /// <summary>调色板缓存，按 ID 升序密集排列。<see cref="RegisterBlockType"/> 后惰性重建。</summary>
        private VoxelBlockType[] _paletteCache;
        private readonly List<IVoxelChunkDecorator> _decorators = new();
        /// <summary>已创建的 MapView（不持久化）。MapId → <see cref="Voxel3DMapView"/>。</summary>
        private readonly Dictionary<string, Voxel3DMapView> _mapViews = new();

        /// <summary>底层持久化（懒缓存），由 <see cref="Initialize"/> 设置。</summary>
        private VoxelMapPersistenceService _persistence;

        /// <summary>自动 flush 计时器。</summary>
        private float _autoSaveTimer;

        /// <summary>自动写盘间隔（秒）。&lt;= 0 关闭。默认 30s。由 <c>Voxel3DMapManager.Update</c> 驱动 <see cref="AutoSaveTick"/>。</summary>
        public float AutoSaveIntervalSec { get; set; } = 30f;

        /// <summary>每次自动 flush 最多写盘的 chunk 数（防 IO 雪崩）。默认 4。</summary>
        public int AutoSaveMaxChunksPerTick { get; set; } = 4;

        /// <summary>Spawn 子系统（懒缓存，由 <see cref="Initialize"/> 设置）。</summary>
        private VoxelEntitySpawnService _spawnService;

        // ─── 生成管线事件（装饰器跑完后对外广播的最终态）──────────
        public event Action<VoxelMap, VoxelChunk> ChunkGenerated;
        public event Action<VoxelMap, int, int>   ChunkUnloaded;
        public event Action<VoxelMap>             MapCreated;
        public event Action<string>               MapDestroyed;

        protected override void Initialize()
        {
            base.Initialize();
            _persistence = VoxelMapPersistenceService.Instance;
            _spawnService = VoxelEntitySpawnService.Instance;
            // 让 spawn service 在改 destroyed 集合时反向标 chunk dirty（避免反向 using）
            _spawnService.DirtyChunkLookup = OnSpawnServiceRequestChunkDirty;
            Log("Voxel3DMapService 初始化完成", Color.green);
        }

        private void OnSpawnServiceRequestChunkDirty(string mapId, int cx, int cz)
        {
            var chunk = GetMap(mapId)?.PeekChunk(cx, cz);
            if (chunk != null) chunk.MarkDirty();
        }

        // ─────────────────────────────────────────────────────────────
        #region Config CRUD

        /// <summary>注册或覆盖体素地图配置（按 ConfigId 去重，写盘）。</summary>
        public void RegisterConfig(VoxelMapConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空配置或缺 ConfigId 的 VoxelMapConfig");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册体素地图配置: {config.ConfigId} ({config.DisplayName})", Color.blue);
        }

        public VoxelMapConfig GetConfig(string configId) =>
            GetData<VoxelMapConfig>(CAT_CONFIGS, configId);

        public IEnumerable<VoxelMapConfig> GetAllConfigs()
        {
            foreach (var key in GetKeys(CAT_CONFIGS))
            {
                var cfg = GetConfig(key);
                if (cfg != null) yield return cfg;
            }
        }

        public bool RemoveConfig(string configId) => RemoveData(CAT_CONFIGS, configId);

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region BlockType Palette

        /// <summary>注册或覆盖 BlockType。同 ID 重复时覆盖（便于热替换调色）。</summary>
        public void RegisterBlockType(VoxelBlockType type)
        {
            if (type == null) { LogWarning("RegisterBlockType: type=null"); return; }
            _blockTypes[type.Id] = type;
            _paletteCache = null;   // 惰性重建
        }

        public VoxelBlockType GetBlockType(byte id) =>
            _blockTypes.TryGetValue(id, out var t) ? t : null;

        /// <summary>
        /// 取调色板（密集数组，索引 = BlockId）。空槽位用 Air 占位。
        /// <para>用法：<c>palette[chunk.TopBlocks[idx]]</c> O(1) 命中，无字典开销。</para>
        /// </summary>
        public VoxelBlockType[] GetPalette()
        {
            if (_paletteCache != null) return _paletteCache;
            if (_blockTypes.Count == 0) return _paletteCache = System.Array.Empty<VoxelBlockType>();

            byte maxId = 0;
            foreach (var k in _blockTypes.Keys) if (k > maxId) maxId = k;

            var arr = new VoxelBlockType[maxId + 1];
            // Air 占位（避免 null 解引用）
            var air = new VoxelBlockType(0, "Air", default, default, solid: false);
            for (var i = 0; i < arr.Length; i++) arr[i] = air;
            foreach (var kv in _blockTypes) arr[kv.Key] = kv.Value;
            _paletteCache = arr;
            return arr;
        }

        public IEnumerable<VoxelBlockType> GetAllBlockTypes() => _blockTypes.Values;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Map 实例管理（仅内存）

        /// <summary>创建运行时体素地图实例。同 MapId 已存在时直接返回原实例。</summary>
        public VoxelMap CreateMap(string mapId, string configId)
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

            var map = new VoxelMap(mapId, configId, cfg.ChunkSize, cfg.CreateGenerator());
            // 装配生成管线：FillChunk → PostFillHook(读盘 + ApplyOverrides) → 装饰器 → 对外广播 ChunkGenerated
            map.PostFillHook = OnPostFillChunk;
            map.ChunkGenerated += OnMapChunkGenerated;
            map.ChunkUnloading += OnMapChunkUnloading;
            map.ChunkUnloaded  += OnMapChunkUnloaded;
            _maps[mapId] = map;

            // 校验 / 写入 Meta（不一致仅警告，按用户决策保留旧 chunk 文件、新 chunk 用最新配置生成）
            EnsureMapMeta(mapId, configId, cfg);

            Log($"创建体素地图实例: {mapId} (config={configId}, chunkSize={cfg.ChunkSize})", Color.cyan);
            MapCreated?.Invoke(map);
            return map;
        }

        /// <summary>读取已有 Meta，与当前配置 JSON 比对；不一致仅 LogWarning，统一更新落盘。</summary>
        private void EnsureMapMeta(string mapId, string configId, VoxelMapConfig cfg)
        {
            if (_persistence == null) return;
            var currentJson = JsonUtility.ToJson(cfg);
            var existing = _persistence.LoadMeta(mapId);
            if (existing != null && !string.IsNullOrEmpty(existing.ConfigJsonSnapshot)
                && existing.ConfigJsonSnapshot != currentJson)
            {
                LogWarning($"体素地图 '{mapId}' 配置已变更（与 Meta 快照不一致）。已有 chunk 文件保留原内容；新 chunk 用最新配置生成（边界可能割裂）。");
            }
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var meta = existing ?? new VoxelMapMetaSaveData(mapId, configId, cfg.ChunkSize, ExtractSeed(cfg))
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

        /// <summary>反射读取 VoxelMapConfig 派生类的 public Seed int 字段；找不到返回 0。</summary>
        private static int ExtractSeed(VoxelMapConfig cfg)
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

        public VoxelMap GetMap(string mapId) =>
            !string.IsNullOrEmpty(mapId) && _maps.TryGetValue(mapId, out var m) ? m : null;

        public IEnumerable<VoxelMap> GetAllMaps() => _maps.Values;

        public bool DestroyMap(string mapId)
        {
            if (!_maps.TryGetValue(mapId, out var map)) return false;
            DestroyMapView(mapId);   // 关联视图先清，避免悬挂引用
            map.UnloadAll();   // ChunkUnloading/Unloaded 会逐块触发
            map.PostFillHook = null;
            map.ChunkGenerated -= OnMapChunkGenerated;
            map.ChunkUnloading -= OnMapChunkUnloading;
            map.ChunkUnloaded  -= OnMapChunkUnloaded;
            _maps.Remove(mapId);
            Log($"销毁体素地图实例: {mapId}", Color.yellow);
            MapDestroyed?.Invoke(mapId);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region MapView 工厂

        /// <summary>
        /// 创建（或返回已有的）<see cref="Voxel3DMapView"/>，并自动 <see cref="Voxel3DMapView.Bind"/>
        /// 到指定 <see cref="VoxelMap"/>。视图持有一个独立 GameObject，由 Service 集中管理生命周期。
        /// <para>同 <paramref name="mapId"/> 已有视图时直接返回，不重复创建。</para>
        /// </summary>
        public Voxel3DMapView CreateMapView(string mapId, Transform parent = null)
        {
            var map = GetMap(mapId);
            if (map == null) { LogWarning($"CreateMapView: map '{mapId}' 不存在"); return null; }
            if (_mapViews.TryGetValue(mapId, out var existing) && existing != null) return existing;

            var go = new GameObject($"VoxelMapView_{mapId}");
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);
            var view = go.AddComponent<Voxel3DMapView>();
            view.Bind(map);
            _mapViews[mapId] = view;
            Log($"创建 Voxel3DMapView: {mapId}", Color.cyan);
            return view;
        }

        public Voxel3DMapView GetMapView(string mapId) =>
            !string.IsNullOrEmpty(mapId) && _mapViews.TryGetValue(mapId, out var v) ? v : null;

        public bool DestroyMapView(string mapId)
        {
            if (!_mapViews.TryGetValue(mapId, out var view)) return false;
            if (view != null)
            {
                view.Unbind();
                UnityEngine.Object.Destroy(view.gameObject);
            }
            _mapViews.Remove(mapId);
            Log($"销毁 Voxel3DMapView: {mapId}", Color.yellow);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 区块装饰器注册

        /// <summary>注册装饰器。按 <see cref="IVoxelChunkDecorator.Priority"/> 升序插入；
        /// 同 Id 已存在则覆盖（便于热替换调优）。</summary>
        public void RegisterDecorator(IVoxelChunkDecorator decorator)
        {
            if (decorator == null || string.IsNullOrEmpty(decorator.Id))
            {
                LogWarning("RegisterDecorator: decorator 为空或缺 Id");
                return;
            }
            for (var i = _decorators.Count - 1; i >= 0; i--)
                if (_decorators[i].Id == decorator.Id) _decorators.RemoveAt(i);

            var idx = 0;
            while (idx < _decorators.Count && _decorators[idx].Priority <= decorator.Priority) idx++;
            _decorators.Insert(idx, decorator);
            Log($"注册体素装饰器: {decorator.Id} (priority={decorator.Priority})", Color.blue);
        }

        public bool UnregisterDecorator(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (var i = _decorators.Count - 1; i >= 0; i--)
            {
                if (_decorators[i].Id != id) continue;
                _decorators.RemoveAt(i);
                Log($"注销体素装饰器: {id}", Color.yellow);
                return true;
            }
            return false;
        }

        public IReadOnlyList<IVoxelChunkDecorator> GetDecorators() => _decorators;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 内部钩子：生成管线

        /// <summary>区块刚 FillChunk 完成、装饰器跑之前：读盘 + 应用 ColumnOverride + 注入 destroyed 集合。</summary>
        private void OnPostFillChunk(VoxelMap map, VoxelChunk chunk)
        {
            if (_persistence == null) return;
            try
            {
                var save = _persistence.LoadChunk(map.MapId, chunk.ChunkX, chunk.ChunkZ);
                if (save == null) return;
                if (save.ColumnOverrides != null && save.ColumnOverrides.Count > 0)
                    chunk.ApplyOverrides(save.ColumnOverrides);
                if (save.DestroyedSpawnIds != null && save.DestroyedSpawnIds.Count > 0
                    && _spawnService != null)
                    _spawnService.SeedDestroyed(map.MapId, chunk.ChunkX, chunk.ChunkZ, save.DestroyedSpawnIds);
            }
            catch (Exception ex)
            {
                LogWarning($"OnPostFillChunk 失败 ({chunk.ChunkX},{chunk.ChunkZ}): {ex.Message}");
            }
        }

        /// <summary>VoxelMap 端事件 → 跑装饰器 → 对外广播最终态。</summary>
        private void OnMapChunkGenerated(VoxelMap map, VoxelChunk chunk)
        {
            if (_decorators.Count > 0)
            {
                // 迭代副本：允许装饰器在自己回调里安全注册/注销其它装饰器
                var snapshot = _decorators.ToArray();
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var d = snapshot[i];
                    try { d.Decorate(map, chunk); }
                    catch (Exception ex)
                    {
                        LogWarning($"IVoxelChunkDecorator '{d.Id}' 在 ({chunk.ChunkX},{chunk.ChunkZ}) 抛异常: {ex.Message}");
                    }
                }
            }
            ChunkGenerated?.Invoke(map, chunk);
        }

        /// <summary>区块即将卸载（仍可访问）：dirty 即写盘 + 销毁 runtime 实体。</summary>
        private void OnMapChunkUnloading(VoxelMap map, VoxelChunk chunk)
        {
            try
            {
                if (chunk.IsDirty) SaveChunkInternal(map.MapId, chunk, sync: false);
            }
            catch (Exception ex)
            {
                LogWarning($"unload save 失败 ({chunk.ChunkX},{chunk.ChunkZ}): {ex.Message}");
            }
            // chunk 仍有效时清 spawn runtime 实体（runtime 桶按 chunkkey 索引，这里做 GameObject 销毁）
            _spawnService?.DestroyChunkRuntimeEntities(map.MapId, chunk.ChunkX, chunk.ChunkZ);
        }

        /// <summary>区块已被移出字典：丢 destroyed 桶 + 转发对外事件。</summary>
        private void OnMapChunkUnloaded(VoxelMap map, int cx, int cz)
        {
            _spawnService?.DropChunkBuckets(map.MapId, cx, cz);
            ChunkUnloaded?.Invoke(map, cx, cz);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 持久化（业务 API）

        /// <summary>**业务层入口** —— 在 (worldX, worldZ) 处覆盖 column 三值（顶/侧/高度）。
        /// 如该 chunk 还未生成会先生成；写盘走 dirty 链路（卸载或 AutoSaveTick）。</summary>
        public void SetVoxelColumnOverride(string mapId, int wx, int wz,
                                           byte topBlock, byte sideBlock, byte height)
        {
            var map = GetMap(mapId);
            if (map == null) { LogWarning($"SetVoxelColumnOverride: map '{mapId}' 不存在"); return; }
            var size = map.ChunkSize;
            var cx = FloorDiv(wx, size); var cz = FloorDiv(wz, size);
            var lx = wx - cx * size;     var lz = wz - cz * size;
            var chunk = map.GetOrGenerateChunk(cx, cz);
            chunk.OverrideColumn(lx, lz, topBlock, sideBlock, height);
        }

        public bool ClearVoxelColumnOverride(string mapId, int wx, int wz)
        {
            var map = GetMap(mapId);
            if (map == null) return false;
            var size = map.ChunkSize;
            var cx = FloorDiv(wx, size); var cz = FloorDiv(wz, size);
            var lx = wx - cx * size;     var lz = wz - cz * size;
            var chunk = map.PeekChunk(cx, cz);
            return chunk != null && chunk.ClearOverride(lx, lz);
        }

        /// <summary>立即写盘指定 chunk 的差量（dirty 才写）。</summary>
        public void SaveChunk(string mapId, int cx, int cz, bool sync = false)
        {
            var chunk = GetMap(mapId)?.PeekChunk(cx, cz);
            if (chunk == null || !chunk.IsDirty) return;
            SaveChunkInternal(mapId, chunk, sync);
        }

        private void SaveChunkInternal(string mapId, VoxelChunk chunk, bool sync)
        {
            if (_persistence == null) return;
            var save = new VoxelChunkSaveData(mapId, chunk.ChunkX, chunk.ChunkZ)
            {
                ColumnOverrides = chunk.EnumerateOverrides(),
            };
            if (_spawnService != null)
            {
                var destroyed = _spawnService.GetDestroyedIds(mapId, chunk.ChunkX, chunk.ChunkZ);
                if (destroyed.Count > 0)
                {
                    save.DestroyedSpawnIds = new List<string>(destroyed.Count);
                    foreach (var id in destroyed) save.DestroyedSpawnIds.Add(id);
                }
            }
            if (sync) _persistence.SaveChunkSync(save);
            else _persistence.SaveChunkAsync(save);
            chunk.ClearDirty();
        }

        // ─────────────────────────────────────────────────────────────
        // Spawn 已破坏标记业务 API（透传到 VoxelEntitySpawnService，供业务侧使用）

        /// <summary>标记某 spawn 实体已永久破坏（玩家砍树/杀怪）。
        /// 后续区块重新加载时装饰器会查 IsSpawnDestroyed 跳过重生。</summary>
        public bool MarkSpawnDestroyed(string mapId, string instanceId) =>
            _spawnService != null && _spawnService.MarkDestroyed(mapId, instanceId);

        public bool UnmarkSpawnDestroyed(string mapId, string instanceId) =>
            _spawnService != null && _spawnService.UnmarkDestroyed(mapId, instanceId);

        public bool IsSpawnDestroyed(string mapId, string instanceId) =>
            _spawnService != null && _spawnService.IsDestroyed(mapId, instanceId);

        public void ClearDestroyedSpawnsInChunk(string mapId, int cx, int cz) =>
            _spawnService?.ClearDestroyedInChunk(mapId, cx, cz);

        /// <summary>把指定 map 当前内存里所有 dirty chunk 立即写盘。</summary>
        public void SaveAllDirtyChunks(string mapId, bool sync = false)
        {
            var map = GetMap(mapId);
            if (map == null) return;
            foreach (var kv in map.LoadedChunks)
            {
                if (kv.Value.IsDirty) SaveChunkInternal(mapId, kv.Value, sync);
            }
        }

        /// <summary>把所有 map 的所有 dirty chunk 立即写盘（应用退出 / 切后台用）。</summary>
        public void SaveAllDirtyChunksAllMaps(bool sync)
        {
            foreach (var map in _maps.Values)
            {
                if (map == null) continue;
                SaveAllDirtyChunks(map.MapId, sync);
            }
        }

        /// <summary>每帧由 <c>Voxel3DMapManager.Update</c> 调用：到达 <see cref="AutoSaveIntervalSec"/> 后
        /// 异步 flush 最多 <see cref="AutoSaveMaxChunksPerTick"/> 个 dirty chunk。</summary>
        public void AutoSaveTick(float deltaTime)
        {
            if (AutoSaveIntervalSec <= 0f) return;
            _autoSaveTimer += deltaTime;
            if (_autoSaveTimer < AutoSaveIntervalSec) return;
            _autoSaveTimer = 0f;
            FlushDirtyBudget(AutoSaveMaxChunksPerTick);
        }

        /// <summary>flush 最多 N 个 dirty chunk（跨所有 map 共享预算，避免 IO 雪崩）。</summary>
        public int FlushDirtyBudget(int maxChunks)
        {
            if (maxChunks <= 0 || _persistence == null) return 0;
            var flushed = 0;
            foreach (var map in _maps.Values)
            {
                if (map == null) continue;
                foreach (var kv in map.LoadedChunks)
                {
                    if (!kv.Value.IsDirty) continue;
                    SaveChunkInternal(map.MapId, kv.Value, sync: false);
                    flushed++;
                    if (flushed >= maxChunks) return flushed;
                }
            }
            return flushed;
        }

        /// <summary>删除指定 map 的全部存档（Meta + 所有 region 文件 + 目录）。返回是否成功。</summary>
        public bool DeleteMapData(string mapId) => _persistence?.DeleteMapData(mapId) ?? false;

        /// <summary>该 map 的存档目录（绝对路径，调试用）。</summary>
        public string GetMapDataPath(string mapId) => _persistence?.MapDir(mapId);

        private static int FloorDiv(int a, int b)
        {
            var q = a / b;
            if ((a % b) != 0 && ((a < 0) ^ (b < 0))) q--;
            return q;
        }

        #endregion
    }
}
