using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator;

namespace Demo.DayNight3D.Map
{
    /// <summary>
    /// DayNight3D 单岛生成器 —— 径向 mask 驱动 MC noise router 的 Continentalness。
    /// <para>**保证岛屿形状自包含**：mask 是距离派生的衰减函数，岛屿天然在世界中心、自带海岸过渡。
    /// MC 的 Erosion + Peaks-Valleys 仅提供地表纹理（山势 / 起伏）—— 不会让陆地"漏"到岛外。</para>
    ///
    /// <para>**算法（每点 (wx, wz)）**：</para>
    /// <list type="number">
    ///   <item>计算到 <c>WorldCenterWorld</c> 的径向距离 <c>dist</c>；</item>
    ///   <item>用低频 Perlin 扭曲 dist → <c>warpedDist</c>（让海岸不规整）；</item>
    ///   <item><c>u = clamp01(1 − (warpedDist − Radius) / Falloff)</c> → smoothstep → mask01；</item>
    ///   <item><c>landShape = pow(mask01, IslandShapePower)</c>；</item>
    ///   <item>采样 MC 三通道 (cNoise, e, w, pv)；</item>
    ///   <item><c>cIsland = lerp(-1, +0.85, landShape)</c>；
    ///         <c>c = lerp(cNoise, cIsland, ContinentalnessFromMaskWeight)</c>；</item>
    ///   <item><c>h = SeaLevel + ContinentalnessSpline(c) × ampRef + ErosionSpline(e) × ampRef × MCAmp × pv × landShape</c>；</item>
    ///   <item>按高度 5 档分群系（Ocean/Beach/Plains/Hills/Mountain/SnowPeak）。</item>
    /// </list>
    ///
    /// 同 (Seed, ChunkCoord) 永远生成同一份 chunk —— 持久化覆盖前提。
    /// </summary>
    public sealed class DayNight3DIslandGenerator : IVoxelMapGenerator
    {
        private readonly DayNight3DVoxelMapConfig _cfg;

        // Seed 派生的噪声相位偏移
        private readonly float _shapeOffX, _shapeOffZ;
        private readonly float _contOffX,  _contOffZ;
        private readonly float _eroOffX,   _eroOffZ;
        private readonly float _wOffX,     _wOffZ;

        public DayNight3DIslandGenerator(DayNight3DVoxelMapConfig cfg)
        {
            _cfg = cfg;
            var rng = new System.Random(cfg.Seed ^ 0x4F09BAD1);
            _shapeOffX = NextOff(rng); _shapeOffZ = NextOff(rng);
            _contOffX  = NextOff(rng); _contOffZ  = NextOff(rng);
            _eroOffX   = NextOff(rng); _eroOffZ   = NextOff(rng);
            _wOffX     = NextOff(rng); _wOffZ     = NextOff(rng);
        }

        // ───────────────────────────────────────────────────────────
        public VoxelChunk Generate(int chunkX, int chunkZ)
        {
            var size  = _cfg.ChunkSize;
            var chunk = new VoxelChunk(chunkX, chunkZ, size);

            for (var lz = 0; lz < size; lz++)
            for (var lx = 0; lx < size; lx++)
            {
                var idx = lz * size + lx;
                var wx  = chunkX * size + lx;
                var wz  = chunkZ * size + lz;

                var h = SampleHeight(wx, wz);
                chunk.Heights[idx] = (byte)Mathf.Clamp(h, 0, _cfg.MaxHeight - 1);

                ResolveBlocks(h, out var biome, out var top, out var side);
                chunk.Biomes[idx]     = biome;
                chunk.TopBlocks[idx]  = top;
                chunk.SideBlocks[idx] = side;
            }
            return chunk;
        }

        public int SampleHeight(int wx, int wz)
        {
            var radius = Mathf.Clamp(_cfg.MCHeightSmoothRadius, 0, 5);
            if (radius <= 0)
                return Mathf.Clamp(Mathf.RoundToInt(SampleHeightRaw(wx, wz)), 0, _cfg.MaxHeight - 1);

            // 盒模糊抹平断崖
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
        /// <summary>单点 raw 高度（连续 float）。径向 mask 注入 MC continentalness。</summary>
        private float SampleHeightRaw(int wx, int wz)
        {
            // ── A. 径向 mask（圆岛 + 海岸扭曲）──
            var center = _cfg.WorldCenterWorld;
            var dx = wx + 0.5f - center.x;
            var dz = wz + 0.5f - center.y;
            var dist = Mathf.Sqrt(dx * dx + dz * dz);

            var warp = Mathf.PerlinNoise(
                          (wx + _shapeOffX) * _cfg.ShapeWarpScale,
                          (wz + _shapeOffZ) * _cfg.ShapeWarpScale) * 2f - 1f;
            var warpedDist = dist + warp * _cfg.ShapeWarpStrength * _cfg.IslandRadiusBlocks;

            var falloff = Mathf.Max(0.001f, _cfg.CoastFalloffBlocks);
            var u       = Mathf.Clamp01(1f - (warpedDist - _cfg.IslandRadiusBlocks) / falloff);
            var mask    = u * u * (3f - 2f * u);                              // smoothstep
            var landShape = Mathf.Pow(Mathf.Max(0.0001f, mask), Mathf.Max(0.1f, _cfg.IslandShapePower));

            // ── B. MC 三通道噪声 ──
            MCNoiseRouter.SampleChannels(
                wx, wz,
                _contOffX, _contOffZ, _cfg.ContinentalnessScale,
                _eroOffX,  _eroOffZ,  _cfg.ErosionScale,
                _wOffX,    _wOffZ,    _cfg.WeirdnessScale,
                out var cNoise, out var e, out _, out var pv);

            // ── C. 把 mask 喂进 Continentalness（核心一步）──
            // mask=1 → cIsland = +0.85（高地）；mask=0 → cIsland = -1（深海）
            var cIsland = Mathf.Lerp(-1f, 0.85f, landShape);
            var c       = Mathf.Lerp(cNoise, cIsland, _cfg.ContinentalnessFromMaskWeight);
            c = Mathf.Clamp(c, -1f, 1f);

            // ── D. Spline → 高度 ──
            var ampRef     = _cfg.TerrainAmplitude / 24f;
            var baseOffset = MCNoiseRouter.ContinentalnessSpline(c) * ampRef;
            var amplitude  = MCNoiseRouter.ErosionSpline(e)         * ampRef * Mathf.Max(0.05f, _cfg.MCAmplitudeScale);

            // 振幅乘 landShape：海里彻底平坦，避免远海冒小礁石
            return _cfg.SeaLevel + baseOffset + amplitude * pv * landShape;
        }

        // ───────────────────────────────────────────────────────────
        /// <summary>按高度 5 档分群系 + 取 (Top, Side)。
        /// 与 <c>VoxelHeightmapGenerator.ClassifyByHeight</c> 口径一致。</summary>
        private void ResolveBlocks(int h, out byte biome, out byte top, out byte side)
        {
            var sea = _cfg.SeaLevel;
            if (h <= sea)
            {
                biome = VoxelBiomeIds.Ocean;
                top  = VoxelBlockTypes.Water; side = VoxelBlockTypes.Sand;
                return;
            }
            if (h <= sea + _cfg.BeachBand)
            {
                biome = VoxelBiomeIds.Beach;
                top  = VoxelBlockTypes.Sand;  side = VoxelBlockTypes.Sand;
                return;
            }
            if (h >= _cfg.SnowLine)
            {
                biome = VoxelBiomeIds.SnowPeak;
                top  = VoxelBlockTypes.Snow;  side = VoxelBlockTypes.Stone;
                return;
            }
            if (h >= _cfg.SnowLine - 8)
            {
                biome = VoxelBiomeIds.Mountain;
                top  = VoxelBlockTypes.Stone; side = VoxelBlockTypes.Stone;
                return;
            }
            if (h >= sea + 16)
            {
                biome = VoxelBiomeIds.Hills;
                top  = VoxelBlockTypes.Grass; side = VoxelBlockTypes.Dirt;
                return;
            }
            biome = VoxelBiomeIds.Plains;
            top  = VoxelBlockTypes.Grass; side = VoxelBlockTypes.Dirt;
        }

        // ───────────────────────────────────────────────────────────
        private static float NextOff(System.Random rng)
            => (float)(rng.NextDouble() * 200000.0 - 100000.0);
    }
}
