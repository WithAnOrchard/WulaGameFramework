using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Persistence.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Persistence
{
    /// <summary>
    /// 体素区块级存档底层 IO（与 2D <c>MapPersistenceService</c> 平行，路径独立避免冲突）。
    /// <para>**线程模型**：</para>
    /// <list type="bullet">
    /// <item>读盘：调用方主线程同步执行（小文件 + ChunksPerFrame 已限速）</item>
    /// <item>写盘：JSON 序列化在主线程，文件 IO 在 ThreadPool（<see cref="Task.Run"/>）</item>
    /// <item>所有写入持有 <c>_writeLock</c> 串行化</item>
    /// <item>原子写：先写 .tmp 再 Move，断电不会留下半截 JSON</item>
    /// </list>
    /// <para>**调用入口**：通常由 <c>Voxel3DMapService</c> 间接调度；业务侧不直接使用本服务。</para>
    /// </summary>
    public class VoxelMapPersistenceService : Service<VoxelMapPersistenceService>
    {
        private const string ROOT_DIR_NAME   = "VoxelMapData";
        private const string CHUNKS_DIR_NAME = "Chunks";
        private const string META_FILE_NAME  = "Meta.json";
        private const string TMP_SUFFIX      = ".tmp";

        public const int REGION_SIZE = 10;

        private readonly object _writeLock = new();
        private readonly Dictionary<RegionKey, VoxelRegionSaveData> _regionCache = new();

        public static int ToRegion(int chunkCoord) =>
            chunkCoord >= 0 ? chunkCoord / REGION_SIZE : (chunkCoord - REGION_SIZE + 1) / REGION_SIZE;

        protected override void Initialize()
        {
            base.Initialize();
            Log($"VoxelMapPersistenceService 初始化完成 (root={RootDir})", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region 路径

        public string RootDir => Path.Combine(Application.persistentDataPath, ROOT_DIR_NAME);
        public string MapDir(string mapId) => Path.Combine(RootDir, SanitizeMapId(mapId));
        public string ChunksDir(string mapId) => Path.Combine(MapDir(mapId), CHUNKS_DIR_NAME);
        public string MetaPath(string mapId) => Path.Combine(MapDir(mapId), META_FILE_NAME);
        public string RegionPath(string mapId, int rx, int rz) =>
            Path.Combine(ChunksDir(mapId), $"r_{rx}_{rz}.json");

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

        public VoxelMapMetaSaveData LoadMeta(string mapId) =>
            ReadJsonOrNull<VoxelMapMetaSaveData>(MetaPath(mapId));

        public VoxelChunkSaveData LoadChunk(string mapId, int cx, int cz)
        {
            var region = GetOrLoadRegion(mapId, ToRegion(cx), ToRegion(cz));
            if (region == null || region.Chunks == null) return null;
            for (var i = 0; i < region.Chunks.Count; i++)
            {
                var c = region.Chunks[i];
                if (c != null && c.ChunkX == cx && c.ChunkZ == cz) return c;
            }
            return null;
        }

        public bool HasChunkFile(string mapId, int cx, int cz) => LoadChunk(mapId, cx, cz) != null;
        public bool HasMeta(string mapId) => File.Exists(MetaPath(mapId));

        private VoxelRegionSaveData GetOrLoadRegion(string mapId, int rx, int rz)
        {
            var key = new RegionKey(mapId, rx, rz);
            lock (_writeLock)
            {
                if (_regionCache.TryGetValue(key, out var cached)) return cached;
                var path = RegionPath(mapId, rx, rz);
                var loaded = ReadJsonOrNull<VoxelRegionSaveData>(path) ?? new VoxelRegionSaveData(mapId, rx, rz);
                loaded.MapId ??= mapId;
                loaded.Chunks ??= new List<VoxelChunkSaveData>();
                _regionCache[key] = loaded;
                return loaded;
            }
        }

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
        #region 写

        public Task SaveMetaAsync(VoxelMapMetaSaveData meta)
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

        public Task SaveChunkAsync(VoxelChunkSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.MapId))
            {
                LogWarning("SaveChunkAsync: data 为空或缺 MapId");
                return Task.CompletedTask;
            }
            var (path, json) = PrepareRegionWrite(data, out var deleteOnly);
            if (deleteOnly) return Task.Run(() => DeleteFileSafe(path));
            if (json == null) return Task.CompletedTask;
            return Task.Run(() => WriteFileAtomic(path, json));
        }

        public void SaveMetaSync(VoxelMapMetaSaveData meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.MapId)) return;
            meta.LastSavedAtUnixMs = NowUnixMs();
            WriteFileAtomic(MetaPath(meta.MapId), JsonUtility.ToJson(meta));
        }

        public void SaveChunkSync(VoxelChunkSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.MapId)) return;
            var (path, json) = PrepareRegionWrite(data, out var deleteOnly);
            if (deleteOnly) { DeleteFileSafe(path); return; }
            if (json == null) return;
            WriteFileAtomic(path, json);
        }

        private (string path, string json) PrepareRegionWrite(VoxelChunkSaveData data, out bool deleteOnly)
        {
            deleteOnly = false;
            var rx = ToRegion(data.ChunkX);
            var rz = ToRegion(data.ChunkZ);
            var path = RegionPath(data.MapId, rx, rz);

            lock (_writeLock)
            {
                var region = GetOrLoadRegion(data.MapId, rx, rz);
                UpsertChunkInRegion(region, data);
                if (region.IsEmpty)
                {
                    _regionCache.Remove(new RegionKey(data.MapId, rx, rz));
                    deleteOnly = true;
                    return (path, null);
                }
                region.SavedAtUnixMs = NowUnixMs();
                return (path, JsonUtility.ToJson(region));
            }
        }

        private static void UpsertChunkInRegion(VoxelRegionSaveData region, VoxelChunkSaveData data)
        {
            if (region == null) return;
            region.Chunks ??= new List<VoxelChunkSaveData>();
            for (var i = region.Chunks.Count - 1; i >= 0; i--)
            {
                var c = region.Chunks[i];
                if (c == null) { region.Chunks.RemoveAt(i); continue; }
                if (c.ChunkX == data.ChunkX && c.ChunkZ == data.ChunkZ)
                    region.Chunks.RemoveAt(i);
            }
            if (!data.IsEmpty)
            {
                data.SavedAtUnixMs = NowUnixMs();
                region.Chunks.Add(data);
            }
        }

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
                    Debug.LogWarning($"[VoxelMapPersistence] write failed: {path} : {ex.Message}");
                }
            }
        }

        private void DeleteFileSafe(string path)
        {
            lock (_writeLock)
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception ex) { Debug.LogWarning($"[VoxelMapPersistence] delete failed: {path} : {ex.Message}"); }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 删除整张地图

        public bool DeleteMapData(string mapId)
        {
            var dir = MapDir(mapId);
            lock (_writeLock)
            {
                try
                {
                    var keysToDrop = new List<RegionKey>();
                    foreach (var k in _regionCache.Keys) if (k.MapId == mapId) keysToDrop.Add(k);
                    for (var i = 0; i < keysToDrop.Count; i++) _regionCache.Remove(keysToDrop[i]);

                    if (Directory.Exists(dir))
                    {
                        Directory.Delete(dir, recursive: true);
                        Log($"已删除体素地图存档目录: {dir}", Color.yellow);
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

        public IEnumerable<(int cx, int cz)> EnumerateChunkFiles(string mapId)
        {
            var dir = ChunksDir(mapId);
            if (!Directory.Exists(dir)) yield break;
            foreach (var path in Directory.EnumerateFiles(dir, "r_*.json"))
            {
                var region = ReadJsonOrNull<VoxelRegionSaveData>(path);
                if (region?.Chunks == null) continue;
                for (var i = 0; i < region.Chunks.Count; i++)
                {
                    var c = region.Chunks[i];
                    if (c != null && !c.IsEmpty) yield return (c.ChunkX, c.ChunkZ);
                }
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        private readonly struct RegionKey : IEquatable<RegionKey>
        {
            public readonly string MapId;
            public readonly int X, Z;
            public RegionKey(string mapId, int x, int z) { MapId = mapId; X = x; Z = z; }
            public bool Equals(RegionKey other) => X == other.X && Z == other.Z && MapId == other.MapId;
            public override bool Equals(object obj) => obj is RegionKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    var h = MapId == null ? 0 : MapId.GetHashCode();
                    h = (h * 397) ^ X;
                    h = (h * 397) ^ Z;
                    return h;
                }
            }
        }
    }
}
