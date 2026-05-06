using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.EssManager.MapManager.Persistence.Dao;

namespace EssSystem.EssManager.MapManager.Persistence
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

        /// <summary>串行化所有文件 IO（同 chunk 多次 SaveChunkAsync 不会并发竞争）。</summary>
        private readonly object _writeLock = new();

        protected override void Initialize()
        {
            base.Initialize();
            Log($"MapPersistenceService 初始化完成 (root={RootDir})", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region 路径

        /// <summary>所有地图存档的根目录。</summary>
        public string RootDir => Path.Combine(Application.persistentDataPath, ROOT_DIR_NAME);

        /// <summary>单张地图的目录（mapId 必须非空）。</summary>
        public string MapDir(string mapId) => Path.Combine(RootDir, SanitizeMapId(mapId));

        /// <summary>该地图的 chunk 子目录。</summary>
        public string ChunksDir(string mapId) => Path.Combine(MapDir(mapId), CHUNKS_DIR_NAME);

        /// <summary>该地图的 Meta.json 路径。</summary>
        public string MetaPath(string mapId) => Path.Combine(MapDir(mapId), META_FILE_NAME);

        /// <summary>单个 chunk 的存档路径。</summary>
        public string ChunkPath(string mapId, int cx, int cy) =>
            Path.Combine(ChunksDir(mapId), $"{cx}_{cy}.json");

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

        /// <summary>同步读取 ChunkSaveData；不存在返回 null；损坏返回 null 并告警。</summary>
        public ChunkSaveData LoadChunk(string mapId, int cx, int cy)
        {
            var path = ChunkPath(mapId, cx, cy);
            return ReadJsonOrNull<ChunkSaveData>(path);
        }

        /// <summary>判断 chunk 是否已有存档文件（不读内容）。</summary>
        public bool HasChunkFile(string mapId, int cx, int cy) =>
            File.Exists(ChunkPath(mapId, cx, cy));

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
        /// 异步写入 ChunkSaveData。空差量（<see cref="ChunkSaveData.IsEmpty"/>）时跳过文件写入，
        /// 并删除已存在的旧文件（避免读到陈旧数据）。
        /// </summary>
        public Task SaveChunkAsync(ChunkSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.MapId))
            {
                LogWarning("SaveChunkAsync: data 为空或缺 MapId");
                return Task.CompletedTask;
            }
            var path = ChunkPath(data.MapId, data.ChunkX, data.ChunkY);

            if (data.IsEmpty)
            {
                // 没有差量 → 删除旧文件，避免下次错误地"加载"过时数据
                return Task.Run(() => DeleteFileSafe(path));
            }

            data.SavedAtUnixMs = NowUnixMs();
            var json = JsonUtility.ToJson(data);
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
            var path = ChunkPath(data.MapId, data.ChunkX, data.ChunkY);
            if (data.IsEmpty)
            {
                DeleteFileSafe(path);
                return;
            }
            data.SavedAtUnixMs = NowUnixMs();
            var json = JsonUtility.ToJson(data);
            WriteFileAtomic(path, json);
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

        /// <summary>删除指定 MapId 的全部存档（Meta + 所有 chunk 文件 + 目录）。返回是否成功。</summary>
        public bool DeleteMapData(string mapId)
        {
            var dir = MapDir(mapId);
            lock (_writeLock)
            {
                try
                {
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

        /// <summary>枚举该地图已存在的 chunk 文件坐标（用于 SaveAllDirty 等批量场景）。</summary>
        public IEnumerable<(int cx, int cy)> EnumerateChunkFiles(string mapId)
        {
            var dir = ChunksDir(mapId);
            if (!Directory.Exists(dir)) yield break;
            foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var idx = name.IndexOf('_');
                if (idx <= 0 || idx >= name.Length - 1) continue;
                if (!int.TryParse(name.Substring(0, idx), out var cx)) continue;
                if (!int.TryParse(name.Substring(idx + 1), out var cy)) continue;
                yield return (cx, cy);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        private static long NowUnixMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
