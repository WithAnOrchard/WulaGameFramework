using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Persistence.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Persistence
{
    /// <summary>
    /// 区块级存档底层 IO（路径管理 + 同步读 + 异步写 + 损坏容错）。
    /// <para>
    /// 不参与 <see cref="Service{T}"/> 的 <c>_dataStorage</c> 持久化体系 —— 自管文件
    /// （路径在 <c>{persistentDataPath}/MapData/</c>，与 <c>ServiceData/</c> 平级）。
    /// </para>
    /// <para>**线程模型**：</para>
    /// <list type="bullet">
    /// <item>读盘：调用方主线程同步执行（小文件 + ChunksPerFrame 已限速）</item>
    /// <item>写盘：JSON 序列化在主线程（小开销），文件 IO 在 ThreadPool（<see cref="Task.Run"/>）</item>
    /// <item>所有写入持有 <c>_writeLock</c> 串行化，避免同 chunk 并发竞争</item>
    /// <item>原子写：先写 <c>.tmp</c>，再删原文件并 Move，断电时不会出现半截 JSON</item>
    /// </list>
    /// <para>**调用入口**：通常由 <c>MapService</c> 间接调度；业务侧不直接使用本服务。</para>
    /// </summary>
    public class MapPersistenceService : Service<MapPersistenceService>
    {
        private const string ROOT_DIR_NAME = "MapData";
        private const string CHUNKS_DIR_NAME = "Chunks";
        private const string META_FILE_NAME = "Meta.json";
        private const string TMP_SUFFIX = ".tmp";

        /// <summary>区域边长（单位：chunk）。每个区域文件存 <c>RegionSize²</c> 个 chunk 的差量。</summary>
        public const int REGION_SIZE = 10;

        /// <summary>串行化所有文件 IO（同 region 多次写不会并发竞争）。</summary>
        private readonly object _writeLock = new();

        /// <summary>运行期 region 缓存：避免每次 chunk 读/写都重新加载整个区域文件。</summary>
        private readonly Dictionary<RegionKey, RegionSaveData> _regionCache = new();

        /// <summary>chunk 坐标 → region 坐标（向下取整支持负数）。</summary>
        public static int ToRegion(int chunkCoord) =>
            chunkCoord >= 0 ? chunkCoord / REGION_SIZE : (chunkCoord - REGION_SIZE + 1) / REGION_SIZE;

        protected override void Initialize()
        {
            base.Initialize();
            Log($"MapPersistenceService 初始化完成 (root={RootDir})", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region 路径

        /// <summary>所有地图存档的根目录。</summary>
        public string RootDir => Path.Combine(UnityEngine.Application.persistentDataPath, ROOT_DIR_NAME);

        /// <summary>单张地图的目录（mapId 必须非空）。</summary>
        public string MapDir(string mapId) => Path.Combine(RootDir, SanitizeMapId(mapId));

        /// <summary>该地图的 chunk（region）子目录。</summary>
        public string ChunksDir(string mapId) => Path.Combine(MapDir(mapId), CHUNKS_DIR_NAME);

        /// <summary>该地图的 Meta.json 路径。</summary>
        public string MetaPath(string mapId) => Path.Combine(MapDir(mapId), META_FILE_NAME);

        /// <summary>区域文件路径 (10×10 chunks/file)。</summary>
        public string RegionPath(string mapId, int rx, int ry) =>
            Path.Combine(ChunksDir(mapId), $"r_{rx}_{ry}.json");

        /// <summary>替换文件名里非法字符为下划线。仅做最小清洗，调用方仍要避免传 path traversal。</summary>
        private static string SanitizeMapId(string mapId)
        {
            if (string.IsNullOrEmpty(mapId)) return "_default";
            var invalid = Path.GetInvalidFileNameChars();
            var arr = mapId.ToCharArray();
            for (var i = 0; i < arr.Length; i++)
                if (Array.IndexOf(invalid, arr[i]) >= 0) arr[i] = '_';
            return new string(arr);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 读

        /// <summary>同步读取 Meta；不存在返回 null；损坏返回 null 并告警。</summary>
        public MapMetaSaveData LoadMeta(string mapId)
        {
            var path = MetaPath(mapId);
            return ReadJsonOrNull<MapMetaSaveData>(path);
        }

        /// <summary>同步读取 ChunkSaveData；不存在返回 null；损坏返回 null 并告警。
        /// 实现上会加载整个 region 文件到内存缓存。</summary>
        public ChunkSaveData LoadChunk(string mapId, int cx, int cy)
        {
            var region = GetOrLoadRegion(mapId, ToRegion(cx), ToRegion(cy));
            if (region == null || region.Chunks == null) return null;
            for (var i = 0; i < region.Chunks.Count; i++)
            {
                var c = region.Chunks[i];
                if (c != null && c.ChunkX == cx && c.ChunkY == cy) return c;
            }
            return null;
        }

        /// <summary>判断 chunk 是否已有存档（走 region 缓存，不额外读盘）。</summary>
        public bool HasChunkFile(string mapId, int cx, int cy) =>
            LoadChunk(mapId, cx, cy) != null;

        /// <summary>取或加载 region；返回的对象会被缓存。不存在时创建空 region。</summary>
        private RegionSaveData GetOrLoadRegion(string mapId, int rx, int ry)
        {
            var key = new RegionKey(mapId, rx, ry);
            lock (_writeLock)
            {
                if (_regionCache.TryGetValue(key, out var cached)) return cached;
                var path = RegionPath(mapId, rx, ry);
                var loaded = ReadJsonOrNull<RegionSaveData>(path) ?? new RegionSaveData(mapId, rx, ry);
                // 防御：修复反序列化后可能全为 null 的字段
                loaded.MapId ??= mapId;
                loaded.Chunks ??= new List<ChunkSaveData>();
                _regionCache[key] = loaded;
                return loaded;
            }
        }

        /// <summary>判断 Meta.json 是否存在。</summary>
        public bool HasMeta(string mapId) => File.Exists(MetaPath(mapId));

        private T ReadJsonOrNull<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return null;
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                LogWarning($"读取存档失败（视为不存在重新生成）：{path} : {ex.Message}");
                return null;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 写（异步 + 同步两条路径）

        /// <summary>
        /// 异步写入 Meta（序列化在调用线程，IO 在 ThreadPool，不阻塞主线程）。
        /// </summary>
        public Task SaveMetaAsync(MapMetaSaveData meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.MapId))
            {
                LogWarning("SaveMetaAsync: meta 为空或缺 MapId");
                return Task.CompletedTask;
            }
            meta.LastSavedAtUnixMs = NowUnixMs();
            var json = JsonUtility.ToJson(meta);
            var path = MetaPath(meta.MapId);
            return Task.Run(() => WriteFileAtomic(path, json));
        }

        /// <summary>
        /// 异步写入 ChunkSaveData（实际上写所在 region 整个文件）。
        /// 空差量时从 region 里移除对应 chunk；region 变空则删除该 region 文件。
        /// </summary>
        public Task SaveChunkAsync(ChunkSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.MapId))
            {
                LogWarning("SaveChunkAsync: data 为空或缺 MapId");
                return Task.CompletedTask;
            }
            var (path, json) = PrepareRegionWrite(data, out var deleteOnly);
            if (deleteOnly) return Task.Run(() => DeleteFileSafe(path));
            if (json == null) return Task.CompletedTask;   // region 仍有数据 但准备阶段异常
            return Task.Run(() => WriteFileAtomic(path, json));
        }

        /// <summary>同步写入 Meta（应用退出路径用，避免后台任务被进程杀死）。</summary>
        public void SaveMetaSync(MapMetaSaveData meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.MapId)) return;
            meta.LastSavedAtUnixMs = NowUnixMs();
            var json = JsonUtility.ToJson(meta);
            WriteFileAtomic(MetaPath(meta.MapId), json);
        }

        /// <summary>同步写入 ChunkSaveData（应用退出路径用）。</summary>
        public void SaveChunkSync(ChunkSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.MapId)) return;
            var (path, json) = PrepareRegionWrite(data, out var deleteOnly);
            if (deleteOnly) { DeleteFileSafe(path); return; }
            if (json == null) return;
            WriteFileAtomic(path, json);
        }

        /// <summary>把传入的 chunk 差量 upsert 到所属 region，返回待写入的路径 + JSON（或者告知调用方仅需删除 region 文件）。
        /// JSON 在处于主线程锁下生成，以避免 region 列表被后续 Save 修改后产生不一致的序列化状态。</summary>
        private (string path, string json) PrepareRegionWrite(ChunkSaveData data, out bool deleteOnly)
        {
            deleteOnly = false;
            var rx = ToRegion(data.ChunkX);
            var ry = ToRegion(data.ChunkY);
            var path = RegionPath(data.MapId, rx, ry);

            lock (_writeLock)
            {
                var region = GetOrLoadRegion(data.MapId, rx, ry);
                UpsertChunkInRegion(region, data);
                if (region.IsEmpty)
                {
                    // 区域主动脱离缓存，下次访问会读盘（不存在则按空 region 重新创建）
                    _regionCache.Remove(new RegionKey(data.MapId, rx, ry));
                    deleteOnly = true;
                    return (path, null);
                }
                region.SavedAtUnixMs = NowUnixMs();
                var json = JsonUtility.ToJson(region);
                return (path, json);
            }
        }

        /// <summary>在 region 里 upsert（同 (cx,cy) 则替换，空差量则移除）。使用者需持有 <c>_writeLock</c>。</summary>
        private static void UpsertChunkInRegion(RegionSaveData region, ChunkSaveData data)
        {
            if (region == null) return;
            region.Chunks ??= new List<ChunkSaveData>();
            for (var i = region.Chunks.Count - 1; i >= 0; i--)
            {
                var c = region.Chunks[i];
                if (c == null) { region.Chunks.RemoveAt(i); continue; }
                if (c.ChunkX == data.ChunkX && c.ChunkY == data.ChunkY)
                    region.Chunks.RemoveAt(i);
            }
            if (!data.IsEmpty)
            {
                data.SavedAtUnixMs = NowUnixMs();
                region.Chunks.Add(data);
            }
        }

        /// <summary>原子写入：先写 .tmp 再 Move 覆盖目标，断电不会留下半截 JSON。</summary>
        private void WriteFileAtomic(string path, string json)
        {
            lock (_writeLock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    var tmp = path + TMP_SUFFIX;
                    File.WriteAllText(tmp, json);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
                catch (Exception ex)
                {
                    // 不能 LogWarning（可能在后台线程）。Unity Debug.* 是线程安全的。
                    Debug.LogWarning($"[MapPersistence] write failed: {path} : {ex.Message}");
                }
            }
        }

        private void DeleteFileSafe(string path)
        {
            lock (_writeLock)
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MapPersistence] delete failed: {path} : {ex.Message}");
                }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 删除整张地图

        /// <summary>删除指定 MapId 的全部存档（Meta + 所有 region 文件 + 目录）。返回是否成功。</summary>
        public bool DeleteMapData(string mapId)
        {
            var dir = MapDir(mapId);
            lock (_writeLock)
            {
                try
                {
                    // 同步干掉该 mapId 的 region 缓存，下次访问会从磁盘（已不存在）重读
                    var keysToDrop = new List<RegionKey>();
                    foreach (var k in _regionCache.Keys) if (k.MapId == mapId) keysToDrop.Add(k);
                    for (var i = 0; i < keysToDrop.Count; i++) _regionCache.Remove(keysToDrop[i]);

                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                        Log($"已删除地图存档目录: {dir}", Color.yellow);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    LogWarning($"DeleteMapData 失败: {dir} : {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>枚举该地图所有存档 chunk 坐标（跨 region 文件展平）。</summary>
        public IEnumerable<(int cx, int cy)> EnumerateChunkFiles(string mapId)
        {
            var dir = ChunksDir(mapId);
            if (!Directory.Exists(dir)) yield break;
            foreach (var path in Directory.EnumerateFiles(dir, "r_*.json"))
            {
                var region = ReadJsonOrNull<RegionSaveData>(path);
                if (region?.Chunks == null) continue;
                for (var i = 0; i < region.Chunks.Count; i++)
                {
                    var c = region.Chunks[i];
                    if (c != null && !c.IsEmpty) yield return (c.ChunkX, c.ChunkY);
                }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>region 缓存键：(mapId, regionX, regionY)。</summary>
        private readonly struct RegionKey : IEquatable<RegionKey>
        {
            public readonly string MapId;
            public readonly int X, Y;
            public RegionKey(string mapId, int x, int y) { MapId = mapId; X = x; Y = y; }
            public bool Equals(RegionKey other) => X == other.X && Y == other.Y && MapId == other.MapId;
            public override bool Equals(object obj) => obj is RegionKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    var h = MapId == null ? 0 : MapId.GetHashCode();
                    h = (h * 397) ^ X;
                    h = (h * 397) ^ Y;
                    return h;
                }
            }
        }
    }
}
