using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 把 (chunkX, chunkZ) → <see cref="VoxelChunk"/>：fBm Perlin 出高度场，再按高度→方块映射。
    /// 纯无状态 + 纯函数；同 (Seed, ChunkCoord) 永远同结果。
    /// </summary>
    public class VoxelHeightmapGenerator : IVoxelMapGenerator
    {
        private readonly VoxelMapConfig _cfg;
        // Octave 偏移，避免不同 octave 用同坐标采样 Perlin 出现镜像伪影
        private readonly Vector2[] _octaveOffsets;

        public VoxelHeightmapGenerator(VoxelMapConfig cfg)
        {
            _cfg = cfg;
            var rng = new System.Random(cfg.Seed);
            _octaveOffsets = new Vector2[Mathf.Max(1, cfg.Octaves)];
            for (var i = 0; i < _octaveOffsets.Length; i++)
            {
                // 随机偏移到 +/-100000，足够让 octave 之间不相关
                var ox = (float)(rng.NextDouble() * 200000.0 - 100000.0);
                var oz = (float)(rng.NextDouble() * 200000.0 - 100000.0);
                _octaveOffsets[i] = new Vector2(ox, oz);
            }
        }

        public VoxelChunk Generate(int chunkX, int chunkZ)
        {
            var size = _cfg.ChunkSize;
            var chunk = new VoxelChunk(chunkX, chunkZ, size);

            for (var lz = 0; lz < size; lz++)
            for (var lx = 0; lx < size; lx++)
            {
                var wx = chunkX * size + lx;
                var wz = chunkZ * size + lz;

                var h = SampleHeight(wx, wz);
                byte top, side;
                ResolveBlocks(h, out top, out side);

                var idx = lz * size + lx;
                chunk.Heights[idx]    = (byte)Mathf.Clamp(h, 0, _cfg.MaxHeight - 1);
                chunk.TopBlocks[idx]  = top;
                chunk.SideBlocks[idx] = side;
            }
            return chunk;
        }

        /// <summary>
        /// 对外暴露：给定世界 (wx, wz) 直接出高度（用于流式跟随高度采样、玩家落地点等）。
        /// </summary>
        public int SampleHeight(int wx, int wz)
        {
            float amplitude = 1f, frequency = 1f, sum = 0f, norm = 0f;
            for (var o = 0; o < _octaveOffsets.Length; o++)
            {
                var off = _octaveOffsets[o];
                var sx = (wx + off.x) * _cfg.TerrainScale * frequency;
                var sz = (wz + off.y) * _cfg.TerrainScale * frequency;
                // PerlinNoise 返回 0..1；映射到 -1..1
                var n = Mathf.PerlinNoise(sx, sz) * 2f - 1f;
                sum  += n * amplitude;
                norm += amplitude;
                amplitude *= _cfg.Persistence;
                frequency *= _cfg.Lacunarity;
            }
            // 归一化到 -1..1
            var noise01 = (norm > 0f ? sum / norm : 0f) * 0.5f + 0.5f;
            var h = Mathf.RoundToInt(_cfg.TerrainBase + noise01 * _cfg.TerrainAmplitude);
            return Mathf.Clamp(h, 0, _cfg.MaxHeight - 1);
        }

        private void ResolveBlocks(int h, out byte top, out byte side)
        {
            // 海平面以下：水盖一层（视觉高度 clamp 由 Mesher 处理）
            if (h <= _cfg.SeaLevel)
            {
                top  = VoxelBlockTypes.Water;
                side = VoxelBlockTypes.Sand;
                return;
            }

            // 海岸沙滩
            if (h <= _cfg.SeaLevel + _cfg.BeachBand)
            {
                top  = VoxelBlockTypes.Sand;
                side = VoxelBlockTypes.Sand;
                return;
            }

            // 雪线之上：雪
            if (h >= _cfg.SnowLine)
            {
                top  = VoxelBlockTypes.Snow;
                side = VoxelBlockTypes.Stone;
                return;
            }

            // 默认：草地，断层露泥土
            top  = VoxelBlockTypes.Grass;
            side = VoxelBlockTypes.Dirt;
        }
    }
}
