using EssSystem.EssManager.MapManager.Dao;

namespace EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Generator
{
    /// <summary>
    /// Biome 分类器：按 (海陆 / 海拔 / 温度 / 湿度) 派生 Tile 的生物群系 TypeId。
    /// <para>
    /// 决策树（陆地）：
    /// <list type="number">
    /// <item>elevation ≥ 0.85 OR temperature &lt; 0.15  → SnowPeak</item>
    /// <item>elevation ≥ 0.70                           → Mountain</item>
    /// <item>elevation ≥ 0.50                           → Hill</item>
    /// <item><b>height &lt; SeaLevel + BeachHeightBand 且 elevation &lt; BeachElevationCap → Beach</b>（MC 风格的 surface-by-height-proximity）</item>
    /// <item>否则按 (T, M) Whittaker 分类（Tundra/Taiga/Grassland/Forest/Swamp/Desert/Savanna/Rainforest）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 海洋：按 h 与 SeaLevel 距离分 DeepOcean / Ocean / ShallowOcean 三层。
    /// </para>
    /// <para>
    /// 注意：海滩判定用「最终高度 h 刚好在 SeaLevel 之上一薄层」—— 和 MC 1.18+ 的地表生成
    /// 策略一致，可靠产生一圈海岸沙带。不再使用 continentalness 窗口，避免被独立噪声的统计盲区拖垮。
    /// </para>
    /// </summary>
    public static class BiomeClassifier
    {
        // —— 陆地高度阈值（集中在此便于调参） ————————————————————————
        private const float SnowPeakElevation = 0.85f;
        private const float SnowPeakTemperatureMax = 0.15f;
        private const float MountainElevation = 0.70f;
        private const float HillElevation = 0.50f;

        private const float ColdMax = 0.30f;     // T < ColdMax 极地
        private const float TemperateMax = 0.60f; // ColdMax ≤ T < TemperateMax 温带

        private const float DepthDeep = 0.15f;   // h < SeaLevel - DepthDeep 深海
        private const float DepthShallow = 0.05f; // h ≥ SeaLevel - DepthShallow 浅海

        // —— 海滩条带：沿海地一圈薄层判定（不依赖 Inspector 序列化值）————————
        /// <summary>海滩高度窗口：height ∈ [seaLevel, seaLevel + band] 的陆地才是海滩。</summary>
        private const float BeachHeightBand = 0.035f;
        /// <summary>海滩独立海拔硬封顶：高于此值的海岸丘陵让位给 Hill/Mountain。</summary>
        private const float BeachElevationCap = 0.55f;

        /// <summary>
        /// 分类核心。所有输入参数都已归一化到 [0,1]。
        /// </summary>
        /// <param name="height">海陆判定用的最终高度（来自 SampleHeight）。</param>
        /// <param name="seaLevel">海陆阈值（来自 PerlinMapConfig.SeaLevel）。</param>
        /// <param name="elevation">独立海拔（来自 SampleElevation）。</param>
        /// <param name="temperature">温度（来自 SampleTemperature）。</param>
        /// <param name="moisture">湿度（来自 SampleMoisture）。</param>
        public static string Classify(float height, float seaLevel,
            float elevation, float temperature, float moisture)
        {
            // ─── 海洋分层 ───
            if (height < seaLevel)
            {
                if (height < seaLevel - DepthDeep) return TileTypes.DeepOcean;
                if (height < seaLevel - DepthShallow) return TileTypes.Ocean;
                return TileTypes.ShallowOcean;
            }

            // ─── 海岸薄层：height 紧贴 SeaLevel 之上 + 非海岸丘陵 → Beach ───
            // 放在 SnowPeak/Mountain/Hill 之前，海岸无论 elevation 噪声多高都会被沙覆盖（除非超 cap）。
            if (height - seaLevel < BeachHeightBand && elevation < BeachElevationCap)
            {
                return TileTypes.Beach;
            }

            // ─── 陆地：高度优先级 ───
            if (elevation >= SnowPeakElevation || temperature < SnowPeakTemperatureMax)
                return TileTypes.SnowPeak;
            if (elevation >= MountainElevation) return TileTypes.Mountain;
            if (elevation >= HillElevation) return TileTypes.Hill;

            // ─── 陆地：按 (T, M) Whittaker 派生 ───
            if (temperature < ColdMax)
            {
                return moisture < 0.5f ? TileTypes.Tundra : TileTypes.Taiga;
            }
            if (temperature < TemperateMax)
            {
                if (moisture < 0.30f) return TileTypes.Grassland;
                if (moisture < 0.70f) return TileTypes.Forest;
                return TileTypes.Swamp;
            }
            // 热带
            if (moisture < 0.30f) return TileTypes.Desert;
            if (moisture < 0.60f) return TileTypes.Savanna;
            return TileTypes.Rainforest;
        }
    }
}
