using System;
using System.Collections.Generic;
using EssSystem.EssManager.MapManager.Dao.Generator;

namespace EssSystem.EssManager.MapManager.Dao
{
    /// <summary>
    /// 运行时地图实例 —— 不参与 Service 持久化，仅内存。
    /// <para>
    /// 持有：地图元数据（<see cref="MapId"/> / <see cref="ConfigId"/> / <see cref="ChunkSize"/>）+
    /// 已生成的区块字典 + 生成器引用（<see cref="IMapGenerator"/>）。
    /// </para>
    /// <para>
    /// **懒生成**：通过 <see cref="GetOrGenerateChunk"/> / <see cref="GetTile"/> 首次访问时才填充对应区块。
    /// </para>
    /// </summary>
    public class Map
    {
        public string MapId { get; }
        public string ConfigId { get; }
        public int ChunkSize { get; }

        private readonly IMapGenerator _generator;
        private readonly Dictionary<long, Chunk> _chunks = new();

        /// <summary>底层生成器（用于 MapView 调用 <see cref="IMapGenerator.PrewarmAround"/> 预热重计算）。</summary>
        public IMapGenerator Generator => _generator;

        /// <summary>
        /// 新区块刚由 <see cref="IMapGenerator.FillChunk"/> 填好地形后触发。
        /// <para>
        /// <c>MapService</c> 会订阅此事件：先跑所有 <c>IChunkDecorator</c>（植物 / 生物 / 结构等），
        /// 再向业务层广播 <c>MapService.ChunkGenerated</c>。想直接挂到 Map 上也行。
        /// </para>
        /// </summary>
        public event Action<Map, Chunk> ChunkGenerated;

        /// <summary>区块被 <see cref="UnloadChunk"/> 或 <see cref="UnloadAll"/> 清除后触发。业务层据此 despawn 实体。</summary>
        public event Action<Map, int, int> ChunkUnloaded;

        public Map(string mapId, string configId, int chunkSize, IMapGenerator generator)
        {
            if (chunkSize <= 0) throw new ArgumentException("chunkSize must be > 0");
            MapId = mapId;
            ConfigId = configId;
            ChunkSize = chunkSize;
            _generator = generator;
        }

        /// <summary>已生成的区块只读视图。</summary>
        public IReadOnlyDictionary<long, Chunk> LoadedChunks => _chunks;

        /// <summary>取/生成指定区块（坐标按整数 Chunk 单位）。生成完成会触发 <see cref="ChunkGenerated"/>。</summary>
        public Chunk GetOrGenerateChunk(int chunkX, int chunkY)
        {
            var key = ChunkKey(chunkX, chunkY);
            if (_chunks.TryGetValue(key, out var existing)) return existing;

            var chunk = new Chunk(chunkX, chunkY, ChunkSize);
            _generator?.FillChunk(chunk);
            _chunks[key] = chunk;
            ChunkGenerated?.Invoke(this, chunk);
            return chunk;
        }

        /// <summary>仅查询已加载区块（不会触发生成），未加载返回 null。</summary>
        public Chunk PeekChunk(int chunkX, int chunkY) =>
            _chunks.TryGetValue(ChunkKey(chunkX, chunkY), out var c) ? c : null;

        /// <summary>世界 Tile 坐标 → Tile（必要时生成区块）。</summary>
        public Tile GetTile(int tileX, int tileY)
        {
            var cx = FloorDiv(tileX, ChunkSize);
            var cy = FloorDiv(tileY, ChunkSize);
            var lx = tileX - cx * ChunkSize;
            var ly = tileY - cy * ChunkSize;
            return GetOrGenerateChunk(cx, cy).GetTile(lx, ly);
        }

        /// <summary>
        /// 卸载指定区块（仅内存）。后续访问会按种子重新生成 → 形状一致；
        /// 成功卸载会触发 <see cref="ChunkUnloaded"/>，业务层据此清理 spawn 的实体。
        /// </summary>
        public bool UnloadChunk(int chunkX, int chunkY)
        {
            var key = ChunkKey(chunkX, chunkY);
            if (!_chunks.Remove(key)) return false;
            ChunkUnloaded?.Invoke(this, chunkX, chunkY);
            return true;
        }

        /// <summary>
        /// 清空全部已生成区块。每个被清除的区块都会依次触发 <see cref="ChunkUnloaded"/>，
        /// 保证业务层可以逐块 despawn 而不必特判 DestroyMap 路径。
        /// </summary>
        public void UnloadAll()
        {
            if (_chunks.Count == 0) return;
            // 先拷贝 key 列表，避免迭代时修改字典。
            var keys = new List<long>(_chunks.Keys);
            _chunks.Clear();
            if (ChunkUnloaded == null) return;
            foreach (var k in keys)
            {
                var cx = (int)(k >> 32);
                var cy = (int)k;
                ChunkUnloaded.Invoke(this, cx, cy);
            }
        }

        // ─── 工具 ──────────────────────────────────────────────────
        private static long ChunkKey(int cx, int cy) => ((long)cx << 32) | (uint)cy;

        /// <summary>向下取整除（处理负坐标）。</summary>
        private static int FloorDiv(int a, int b)
        {
            var q = a / b;
            if ((a % b) != 0 && ((a < 0) ^ (b < 0))) q--;
            return q;
        }
    }
}
