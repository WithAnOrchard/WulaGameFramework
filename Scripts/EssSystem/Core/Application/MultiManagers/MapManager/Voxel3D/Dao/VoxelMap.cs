using System;
using System.Collections.Generic;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 运行时 3D 体素地图实例 —— 与 2D <c>Map</c> 平行。
    /// <para>
    /// 持有 (mapId, configId, chunkSize, generator) + 已生成的 <see cref="VoxelChunk"/> 字典。
    /// 懒生成：通过 <see cref="GetOrGenerateChunk"/> 首次访问时填充对应区块。
    /// </para>
    /// <para>
    /// 与 2D Map 区别：3D Chunk 是 heightmap 形态（每列 TopBlock + SideBlock + Height），
    /// 数据布局完全不同，因此 Map / Chunk 不复用 2D 类型。
    /// </para>
    /// </summary>
    public class VoxelMap
    {
        public string MapId { get; }
        public string ConfigId { get; }
        public int ChunkSize { get; }

        private readonly IVoxelMapGenerator _generator;
        private readonly Dictionary<long, VoxelChunk> _chunks = new();

        /// <summary>底层生成器（外部可调用 <see cref="IVoxelMapGenerator.SampleHeight"/> 做查询）。</summary>
        public IVoxelMapGenerator Generator => _generator;

        /// <summary>新区块刚生成完成（已跑完 PostFillHook，<b>但装饰器还未执行</b>）触发。
        /// Voxel3DMapService 订阅此事件跑装饰器、再向业务层广播最终态。</summary>
        public event Action<VoxelMap, VoxelChunk> ChunkGenerated;

        /// <summary>区块即将卸载（仍可访问 chunk 数据，订阅者可写盘）。</summary>
        public event Action<VoxelMap, VoxelChunk> ChunkUnloading;

        /// <summary>区块已被移出字典后触发。业务层据此 despawn 实体。</summary>
        public event Action<VoxelMap, int, int> ChunkUnloaded;

        /// <summary>**内部钩子**：FillChunk 之后、ChunkGenerated 之前调用。
        /// 由 <c>Voxel3DMapService</c> 安装，用于读盘 / 应用差量等。业务层不要直接使用。</summary>
        public Action<VoxelMap, VoxelChunk> PostFillHook { get; set; }

        public VoxelMap(string mapId, string configId, int chunkSize, IVoxelMapGenerator generator)
        {
            if (string.IsNullOrEmpty(mapId)) throw new ArgumentException("mapId is null/empty");
            if (chunkSize <= 0) throw new ArgumentException("chunkSize must be > 0");
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            MapId = mapId;
            ConfigId = configId;
            ChunkSize = chunkSize;
            _generator = generator;
        }

        /// <summary>已生成的区块只读视图。Key 编码为 (cx,cz) → long。</summary>
        public IReadOnlyDictionary<long, VoxelChunk> LoadedChunks => _chunks;

        /// <summary>取/生成区块。生成完成依次触发 <see cref="PostFillHook"/> + <see cref="ChunkGenerated"/>。</summary>
        public VoxelChunk GetOrGenerateChunk(int chunkX, int chunkZ)
        {
            var key = ChunkKey(chunkX, chunkZ);
            if (_chunks.TryGetValue(key, out var existing)) return existing;

            var chunk = _generator.Generate(chunkX, chunkZ);
            // 与 2D Map 一致：先 PostFillHook 应用差量，再放进字典 + 广播 ChunkGenerated（让装饰器读到完整数据）
            PostFillHook?.Invoke(this, chunk);
            _chunks[key] = chunk;
            ChunkGenerated?.Invoke(this, chunk);
            return chunk;
        }

        /// <summary>仅查询已加载区块（不会触发生成）。</summary>
        public VoxelChunk PeekChunk(int chunkX, int chunkZ) =>
            _chunks.TryGetValue(ChunkKey(chunkX, chunkZ), out var c) ? c : null;

        /// <summary>采样世界 (wx, wz) 处的地表高度（直接走 generator，不构建区块）。</summary>
        public int SampleHeight(int wx, int wz) => _generator.SampleHeight(wx, wz);

        /// <summary>卸载指定区块。下次访问会按种子重新生成（确定性保证）。</summary>
        public bool UnloadChunk(int chunkX, int chunkZ)
        {
            var key = ChunkKey(chunkX, chunkZ);
            if (!_chunks.TryGetValue(key, out var chunk)) return false;
            ChunkUnloading?.Invoke(this, chunk);
            _chunks.Remove(key);
            ChunkUnloaded?.Invoke(this, chunkX, chunkZ);
            return true;
        }

        /// <summary>清空全部已生成区块（每个都依次发 unloading + unloaded）。</summary>
        public void UnloadAll()
        {
            if (_chunks.Count == 0) return;
            if (ChunkUnloading != null)
            {
                foreach (var kv in _chunks) ChunkUnloading.Invoke(this, kv.Value);
            }
            var keys = new List<long>(_chunks.Keys);
            _chunks.Clear();
            if (ChunkUnloaded == null) return;
            foreach (var k in keys)
            {
                var cx = (int)(k >> 32);
                var cz = (int)k;
                ChunkUnloaded.Invoke(this, cx, cz);
            }
        }

        // ─── 工具 ──────────────────────────────────────────────────
        private static long ChunkKey(int cx, int cz) => ((long)cx << 32) | (uint)cz;
    }
}
