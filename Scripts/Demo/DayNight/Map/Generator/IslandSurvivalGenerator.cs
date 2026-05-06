using UnityEngine;
using EssSystem.EssManager.MapManager.Dao;
using EssSystem.EssManager.MapManager.Dao.Generator;
using EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Dao;
using Demo.DayNight.Map.Config;

namespace Demo.DayNight.Map.Generator
{
    /// <summary>
    /// 海岛生成器 —— 距离场 + 海岸 Perlin 扰动构形，内陆按温度/湿度 2D 决策表派生群系。
    /// <para>**确定性**：相同 (chunkX, chunkY, Seed) 多次调用结果一致 —— Tile 完全由噪声派生，无内部状态。</para>
    /// </summary>
    public class IslandSurvivalGenerator : IMapGenerator
    {
        private readonly IslandSurvivalMapConfig _cfg;

        // 派生子种子：把单一 Seed 拆成几条 Perlin 偏移，避免温度/湿度/海岸完全同相
        private readonly float _seedShore;
        private readonly float _seedTemp;
        private readonly float _seedMoist;

        // Warp 用两条独立 Perlin 通道，避免对角线相关
        private readonly float _seedWarpX;
        private readonly float _seedWarpY;

        public IslandSurvivalGenerator(IslandSurvivalMapConfig cfg)
        {
            _cfg = cfg ?? new IslandSurvivalMapConfig();
            // 使用大质数偏移把 int Seed 映射到 (0, 100000) 区间的 float，作为 Perlin 入参偏移
            _seedShore = ComputeSeedOffset(_cfg.Seed, 0.31415f, 0f);
            _seedTemp  = ComputeSeedOffset(_cfg.Seed, 1.61803f, 7000f);
            _seedMoist = ComputeSeedOffset(_cfg.Seed, 2.71828f, 13000f);
            _seedWarpX = ComputeSeedOffset(_cfg.Seed, 4.66920f, 23000f);
            _seedWarpY = ComputeSeedOffset(_cfg.Seed, 5.55555f, 31000f);
        }

        private static float ComputeSeedOffset(int seed, float prime, float bias) =>
            Mathf.Repeat(seed * prime + bias, 100000f);

        // ─── 海岸 fBm（多倍频 Perlin）—— 与碰撞器共享相同 noise 函数，避免视觉/物理错位 ───────
        private const int  ShoreOctaves      = 3;
        private const float ShoreLacunarity  = 2.0f;   // 频率倍率
        private const float ShorePersistence = 0.5f;   // 振幅衰减

        /// <summary>多倍频 Perlin（fBm），输出 [-0.5, 0.5] 区间附近的小幅噪声。
        /// <paramref name="seedShore"/> 由 <see cref="ComputeSeedOffset"/> 派生。</summary>
        private static float ShoreFbm(float worldX, float worldY, float baseFrequency, float seedShore)
        {
            var amp = 1f;
            var freq = baseFrequency;
            var sum = 0f;
            var norm = 0f;
            for (var o = 0; o < ShoreOctaves; o++)
            {
                var n = Mathf.PerlinNoise((worldX + seedShore) * freq, (worldY + seedShore) * freq) - 0.5f;
                sum += n * amp;
                norm += amp;
                amp *= ShorePersistence;
                freq *= ShoreLacunarity;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        public void FillChunk(Chunk chunk)
        {
            if (chunk == null || chunk.Tiles == null) return;

            var cs = chunk.Size;
            var worldOriginTileX = _cfg.OriginChunkX * cs;
            var worldOriginTileY = _cfg.OriginChunkY * cs;
            var worldSizeTiles   = _cfg.WorldSizeChunks * cs;
            var centerX = worldOriginTileX + worldSizeTiles * 0.5f;
            var centerY = worldOriginTileY + worldSizeTiles * 0.5f;
            var maxRadius = worldSizeTiles * 0.5f;

            for (var ly = 0; ly < cs; ly++)
            {
                var worldY = chunk.ChunkY * cs + ly;
                for (var lx = 0; lx < cs; lx++)
                {
                    var worldX = chunk.ChunkX * cs + lx;

                    // 1) 距离场 = warp 扭曲 + 圆形距离 + fBm 海岸抖动 → 归一化距离
                    var dist = ComputeIslandDist(worldX, worldY, _cfg,
                        _seedShore, _seedWarpX, _seedWarpY,
                        centerX, centerY, maxRadius);

                    chunk.SetTile(lx, ly, ResolveTile(worldX, worldY, dist));
                }
            }
        }

        /// <summary>对单个 tile 推导最终 (typeId, elevation, temperature, moisture)。</summary>
        private Tile ResolveTile(int worldX, int worldY, float dist)
        {
            // 海洋
            if (dist >= _cfg.DeepOceanThreshold)
                return new Tile(TopDownTileTypes.DeepOcean, 0, 128, 255);
            if (dist >= _cfg.ShallowOceanThreshold)
            {
                var elev = (byte)Mathf.Clamp(20f + (1f - dist) * 60f, 0f, 80f);
                return new Tile(TopDownTileTypes.ShallowOcean, elev, 140, 240);
            }

            // 内陆 / 海滩共用的湿度采样
            var moisture = Mathf.PerlinNoise(
                (worldX + _seedMoist) * _cfg.BiomeFrequency,
                (worldY + _seedMoist) * _cfg.BiomeFrequency);
            var moistureByte = (byte)Mathf.Clamp(moisture * 255f, 0f, 255f);

            // 海滩"候选"区：用一条独立的低频 noise 决定**这一段海岸**到底是沙还是草，
            // 让海滩不是闭合环而是"一段段断续沙岸 + 草坡入海"。
            if (dist >= _cfg.BeachThreshold)
            {
                var beachPick = Mathf.PerlinNoise(
                    (worldX + _seedTemp) * 0.06f,
                    (worldY + _seedTemp) * 0.06f);
                // beachPick 在 [0,1]：高于 BeachBreakup 才出沙，否则用 Grassland 接水
                if (beachPick > _cfg.BeachBreakup)
                    return new Tile(TopDownTileTypes.Beach, 80, 200, 110);
                return new Tile(TopDownTileTypes.Grassland, 95, 200, moistureByte);
            }

            // ── 内陆：海岛专用群系（去掉 Tundra/Taiga/Desert 等不合适的极地+沙漠）──
            var typeId = PickIslandBiome(dist, moisture);

            // 中央山脊：靠近圆心 + 在山带内 → 山地
            if (_cfg.MountainCenterRatio > 0f && dist < _cfg.MountainCenterRatio
                && Mathf.PerlinNoise((worldX + _seedTemp) * _cfg.BiomeFrequency,
                                     (worldY + _seedTemp) * _cfg.BiomeFrequency) < 0.55f)
                typeId = TopDownTileTypes.Mountain;

            byte elevation;
            if (typeId == TopDownTileTypes.Mountain) elevation = 230;
            else elevation = (byte)Mathf.Clamp(120f + (1f - dist) * 80f, 80f, 220f);

            // 海岛默认温暖湿润，温度只用 moisture 影响（避免冷温带群系）
            var temperatureByte = (byte)Mathf.Clamp(160f + moisture * 60f, 0f, 255f);
            return new Tile(typeId, elevation, temperatureByte, moistureByte);
        }

        /// <summary>
        /// 海岛专用群系决策：以"距中心半径 dist"为主导（同心环），辅以 moisture 微调。
        /// 调色板限制为 Grassland / Forest / Rainforest / Savanna / Swamp，外加中央 Mountain。
        /// </summary>
        private static string PickIslandBiome(float dist, float moisture)
        {
            // 沿海带（dist 0.65~BeachThreshold）：草原 / 稀树草原 / 湿润沼泽
            if (dist > 0.65f)
            {
                if (moisture < 0.35f) return TopDownTileTypes.Savanna;
                if (moisture > 0.75f) return TopDownTileTypes.Swamp;
                return TopDownTileTypes.Grassland;
            }
            // 中圈（0.35~0.65）：温带森林为主，干处草原
            if (dist > 0.35f)
            {
                if (moisture < 0.35f) return TopDownTileTypes.Grassland;
                return TopDownTileTypes.Forest;
            }
            // 内陆（0~0.35）：森林 → 雨林（湿）
            if (moisture < 0.55f) return TopDownTileTypes.Forest;
            return TopDownTileTypes.Rainforest;
        }

        /// <summary>本生成器的所有计算都是 per-tile 纯函数，没有需要预热的区域级缓存。</summary>
        public void PrewarmAround(int chunkX, int chunkY, int chunkSize) { }

        // ─────────────────────────────────────────────────────────────
        #region 共享给碰撞器 / 出生点查询的静态 helper

        /// <summary>世界中心（tile 坐标）。</summary>
        public static Vector2 GetWorldCenter(IslandSurvivalMapConfig cfg)
        {
            var worldOriginTileX = cfg.OriginChunkX * cfg.ChunkSize;
            var worldOriginTileY = cfg.OriginChunkY * cfg.ChunkSize;
            var worldSizeTiles   = cfg.WorldSizeChunks * cfg.ChunkSize;
            return new Vector2(
                worldOriginTileX + worldSizeTiles * 0.5f,
                worldOriginTileY + worldSizeTiles * 0.5f);
        }

        /// <summary>归一化最大半径（tile 单位 → 边长一半）。</summary>
        public static float GetMaxRadiusTiles(IslandSurvivalMapConfig cfg) =>
            cfg.WorldSizeChunks * cfg.ChunkSize * 0.5f;

        /// <summary>
        /// **核心距离公式**（generator + boundary 共享）。返回归一化距离 dist：
        /// dist &lt; BeachThreshold = 陆地, [BeachThreshold, ShallowOceanThreshold) = 海滩,
        /// [ShallowOceanThreshold, DeepOceanThreshold) = 浅海, ≥ DeepOceanThreshold = 深海。
        /// <para>组合：① 域扭曲（Perlin 把世界坐标拉扯）→ ② 圆心距离 ÷ maxRadius → ③ 加 fBm 海岸抖动。</para>
        /// </summary>
        public static float ComputeIslandDist(
            float worldX, float worldY, IslandSurvivalMapConfig cfg,
            float seedShore, float seedLobeBig, float seedLobeMid,
            float centerX, float centerY, float maxRadius)
        {
            // ① 圆形距离 cd 与单位方向 (ux, uy)
            var rawDx = worldX - centerX;
            var rawDy = worldY - centerY;
            var rawDist = Mathf.Sqrt(rawDx * rawDx + rawDy * rawDy);
            var cd = rawDist / Mathf.Max(1f, maxRadius);
            // 当玩家正好在圆心，方向无意义；取一个稳定向量避免 NaN
            var ux = rawDist > 0.0001f ? rawDx / rawDist : 1f;
            var uy = rawDist > 0.0001f ? rawDy / rawDist : 0f;

            // ② **taper**：在 cd→1.0 时快速归零，硬约束海岛不外扩超出世界框
            var cd2 = cd * cd;
            var taper = Mathf.Max(0f, 1f - cd2 * cd2);

            // ③ **沿单位圆采样的 angular Perlin** —— 直接生成径向偏移
            //    Perlin 用 (cos θ, sin θ) 作为入参，沿 θ 完美连续无接缝；
            //    多频叠加 → 大尺度湾 + 中尺度半岛，本质上比 xy-warp 更适合"形状"塑造。
            var lobeBigScale = 1.8f;   // 频率 1.8 → 角向约 3 个大湾
            var lobeMidScale = 4.5f;   // 频率 4.5 → 角向约 7-9 个小湾/半岛
            var lobeBig = Mathf.PerlinNoise(ux * lobeBigScale + seedLobeBig,
                                            uy * lobeBigScale + seedLobeBig) - 0.5f;
            var lobeMid = Mathf.PerlinNoise(ux * lobeMidScale + seedLobeMid,
                                            uy * lobeMidScale + seedLobeMid) - 0.5f;
            // shoreOffset ∈ [-WarpAmplitude, +WarpAmplitude]（权重和=2 × max each=0.5 → ±1）
            var shoreOffset = (lobeBig * 1.3f + lobeMid * 0.7f) * cfg.WarpAmplitude;
            // 正 = 半岛凸出（dist 减小=land 更靠外），负 = 海湾内凹
            var dist = cd - shoreOffset * taper;

            // ④ 高频 fBm 细节（用 worldX/Y 而非 angular，让海岸有锯齿/犬牙细节）
            dist += ShoreFbm(worldX, worldY, cfg.ShorelineFrequency, seedShore)
                  * cfg.ShorelineNoise * taper;
            return dist;
        }

        /// <summary>从 cfg.Seed 派生与 generator 一致的 shore + warp 子种子。</summary>
        public static (float seedShore, float seedWarpX, float seedWarpY) DeriveSeeds(IslandSurvivalMapConfig cfg) => (
            ComputeSeedOffset(cfg.Seed, 0.31415f, 0f),
            ComputeSeedOffset(cfg.Seed, 4.66920f, 23000f),
            ComputeSeedOffset(cfg.Seed, 5.55555f, 31000f));

        /// <summary>
        /// 给定世界 tile 坐标，复算 dist 并返回这个 tile 在岛上是哪一类：
        /// 0=陆地, 1=海滩, 2=浅海, 3=深海。
        /// </summary>
        public static int ClassifyTile(int worldX, int worldY, IslandSurvivalMapConfig cfg)
        {
            var center = GetWorldCenter(cfg);
            var maxR = GetMaxRadiusTiles(cfg);
            var (s, wx, wy) = DeriveSeeds(cfg);
            var dist = ComputeIslandDist(worldX, worldY, cfg, s, wx, wy, center.x, center.y, maxR);

            if (dist >= cfg.DeepOceanThreshold) return 3;
            if (dist >= cfg.ShallowOceanThreshold) return 2;
            if (dist >= cfg.BeachThreshold) return 1;
            return 0;
        }

        /// <summary>
        /// 沿给定角度 <paramref name="angleRad"/> 从世界中心二分查找 dist == <paramref name="threshold"/> 处的半径，
        /// 单位 tile。给 <see cref="IslandBoundary"/> 取多边形顶点用。
        /// <para>
        /// 常用阈值：
        /// <list type="bullet">
        /// <item><c>cfg.BeachThreshold</c>（默认 0.93）= 陆地↔海滩边界（land/grass 与 beach 交界）</item>
        /// <item><c>cfg.ShallowOceanThreshold</c>（默认 0.97）= **海滩↔浅海边界**（land 与 water tile 真正交界，碰撞墙首选）</item>
        /// <item><c>cfg.DeepOceanThreshold</c>（默认 1.00）= 浅海↔深海边界</item>
        /// </list>
        /// </para>
        /// 因为 warp 后 dist 不是 r 的解析函数，只能数值搜索；二分 14 步 ≈ 0.5 tile 精度。
        /// </summary>
        public static float SampleShoreRadiusTiles(float angleRad, IslandSurvivalMapConfig cfg, float threshold)
        {
            var center = GetWorldCenter(cfg);
            var maxR = GetMaxRadiusTiles(cfg);
            var (s, wx, wy) = DeriveSeeds(cfg);

            // 二分搜索：在 [0, maxR * 1.2] 范围找 dist == threshold
            float lo = 0f, hi = maxR * 1.2f;
            for (var step = 0; step < 14; step++)
            {
                var mid = (lo + hi) * 0.5f;
                var px = center.x + Mathf.Cos(angleRad) * mid;
                var py = center.y + Mathf.Sin(angleRad) * mid;
                var d = ComputeIslandDist(px, py, cfg, s, wx, wy, center.x, center.y, maxR);
                if (d < threshold) lo = mid;
                else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        /// <summary>便捷重载：默认用 BeachThreshold（保留兼容性）。</summary>
        public static float SampleShoreRadiusTiles(float angleRad, IslandSurvivalMapConfig cfg) =>
            SampleShoreRadiusTiles(angleRad, cfg, cfg.BeachThreshold);

        #endregion
    }
}
