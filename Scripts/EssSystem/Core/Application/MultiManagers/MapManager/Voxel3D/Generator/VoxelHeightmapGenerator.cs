using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Generator;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 把 (chunkX, chunkZ) → <see cref="VoxelChunk"/>。
    /// <para>
    /// **统一管线（高度永远 MC noise router）**：
    /// <list type="bullet">
    ///   <item><b>高度</b>：MC 1.18+ 三通道 noise router (Continentalness + Erosion + Peaks-Valleys)
    ///   → <see cref="MCNoiseRouter"/> 共享 spline 输出 block-int 高度，可选 (2R+1)² 盒模糊。</item>
    ///   <item><b>群系</b> 二选一：
    ///     <list type="bullet">
    ///       <item><see cref="VoxelMapConfig.SharedPerlin"/> 非空 → 走 2D <c>BiomeClassifier</c>，用
    ///         elev / temp / moist 气候三参数出 Forest / Desert / Savanna / Taiga / Tundra 等丰富 biome。</item>
    ///       <item>SharedPerlin == null → 按高度 5 档（Ocean / Beach / Plains / Hills / Mountain / SnowPeak）。</item>
    ///     </list>
    ///   </item>
    /// </list>
    /// </para>
    /// 纯无状态 + 纯函数；同 (Seed, ChunkCoord) 永远同结果。
    /// </summary>
    public class VoxelHeightmapGenerator : IVoxelMapGenerator
    {
        private readonly VoxelMapConfig _cfg;

        /// <summary>可选：仅供 biome 分类的 2D 委托（含 elev/temp/moist 采样）；高度从不走它。</summary>
        private readonly PerlinMapGenerator _climateDelegate;

        // MC noise router 三通道独立偏移（Seed 派生）
        private readonly float _contOffX, _contOffZ;
        private readonly float _eroOffX,  _eroOffZ;
        private readonly float _wOffX,    _wOffZ;

        public VoxelHeightmapGenerator(VoxelMapConfig cfg)
        {
            _cfg = cfg;

            if (cfg.SharedPerlin != null)
                _climateDelegate = new PerlinMapGenerator(cfg.SharedPerlin);

            var rng = new System.Random(cfg.Seed);
            _contOffX = NextOff(rng); _contOffZ = NextOff(rng);
            _eroOffX  = NextOff(rng); _eroOffZ  = NextOff(rng);
            _wOffX    = NextOff(rng); _wOffZ    = NextOff(rng);
        }

        private static float NextOff(System.Random rng)
            => (float)(rng.NextDouble() * 200000.0 - 100000.0);

        // ───────────────────────────────────────────────────────────
        public VoxelChunk Generate(int chunkX, int chunkZ)
        {
            var size      = _cfg.ChunkSize;
            var chunk     = new VoxelChunk(chunkX, chunkZ, size);
            var maxHeight = _cfg.MaxHeight;
            var sea01     = (float)_cfg.SeaLevel / Mathf.Max(1, maxHeight);

            for (var lz = 0; lz < size; lz++)
            for (var lx = 0; lx < size; lx++)
            {
                var idx = lz * size + lx;
                var wx  = chunkX * size + lx;
                var wz  = chunkZ * size + lz;

                // 1) 高度 ── MC noise router
                var h = SampleHeight(wx, wz);
                chunk.Heights[idx] = (byte)Mathf.Clamp(h, 0, maxHeight - 1);

                // 2) 群系
                byte biome;
                if (_climateDelegate != null)
                {
                    // 路径混合：MC 高度 + 2D 气候 → 气候驱动 biome
                    var h01    = (float)h / Mathf.Max(1, maxHeight);
                    var elev   = _climateDelegate.SampleElevation(wx, wz);
                    var temp   = _climateDelegate.SampleTemperature(wx, wz, elev);
                    var moist  = _climateDelegate.SampleMoisture(wx, wz, h01, elev);
                    var typeId = BiomeClassifier.Classify(h01, sea01, elev, temp, moist);
                    biome      = TileTypeIdToVoxelBiome.Map(typeId);
                }
                else
                {
                    biome = ClassifyByHeight(h);
                }

                var profile = VoxelBiomes.Profiles[biome];
                chunk.Biomes[idx]     = biome;
                chunk.TopBlocks[idx]  = profile.TopBlock;
                chunk.SideBlocks[idx] = profile.SideBlock;
            }
            return chunk;
        }

        /// <summary>对外暴露：世界 (wx, wz) → 地表高度（block，MC noise router 输出）。
        /// 流式跟随采样 / 玩家落地点查询都走这里。</summary>
        public int SampleHeight(int wx, int wz)
        {
            var radius = Mathf.Clamp(_cfg.MCHeightSmoothRadius, 0, 5);
            if (radius <= 0)
                return Mathf.Clamp(Mathf.RoundToInt(SampleHeightRaw(wx, wz)), 0, _cfg.MaxHeight - 1);

            // 盒模糊抹平蛋糕断崖
            float sum = 0f;
            int   count = 0;
            for (var oz = -radius; oz <= radius; oz++)
            for (var ox = -radius; ox <= radius; ox++)
            {
                sum += SampleHeightRaw(wx + ox, wz + oz);
                count++;
            }
            return Mathf.Clamp(Mathf.RoundToInt(sum / count), 0, _cfg.MaxHeight - 1);
        }

        // ───────────────────────────────────────────────────────────
        /// <summary>单点 raw 高度（连续 float，未 round / 未模糊）—— MC noise router 公式。</summary>
        private float SampleHeightRaw(int wx, int wz)
        {
            MCNoiseRouter.SampleChannels(
                wx, wz,
                _contOffX, _contOffZ, _cfg.ContinentalnessScale,
                _eroOffX,  _eroOffZ,  _cfg.ErosionScale,
                _wOffX,    _wOffZ,    _cfg.WeirdnessScale,
                out var c, out var e, out _, out var pv);

            // TerrainAmplitude 作总参考（MC 默认 24 → spline 原值）
            var ampRef     = _cfg.TerrainAmplitude / 24f;
            var baseOffset = MCNoiseRouter.ContinentalnessSpline(c) * ampRef;
            var amplitude  = MCNoiseRouter.ErosionSpline(e)         * ampRef * Mathf.Max(0.05f, _cfg.MCAmplitudeScale);

            return _cfg.SeaLevel + baseOffset + amplitude * pv;
        }

        // ───────────────────────────────────────────────────────────
        /// <summary>无气候时按高度 5 档分群系：Ocean / Beach / Plains / Hills / Mountain / SnowPeak。</summary>
        private byte ClassifyByHeight(int h)
        {
            var sea = _cfg.SeaLevel;
            if (h <= sea)                       return VoxelBiomeIds.Ocean;
            if (h <= sea + _cfg.BeachBand)      return VoxelBiomeIds.Beach;
            if (h >= _cfg.SnowLine)             return VoxelBiomeIds.SnowPeak;
            if (h >= _cfg.SnowLine - 8)         return VoxelBiomeIds.Mountain;
            if (h >= sea + 16)                  return VoxelBiomeIds.Hills;
            return VoxelBiomeIds.Plains;
        }
    }
}
