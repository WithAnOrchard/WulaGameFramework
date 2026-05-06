using EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Dao;

namespace EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Generator
{
    /// <summary>
    /// Biome 分类器：按 (海陆 / 海拔 / 温度 / 湿度) 派生 Tile 的生物群系 TypeId。
    /// <para>
    /// 决策树（陆地，按现实地理比例调参）：
    /// <list type="number">
    /// <item>elevation ≥ 0.90 <b>且</b> temperature &lt; 0.45  → SnowPeak（仅高海拔且冷的山顶才积雪）</item>
    /// <item>elevation ≥ 0.78                                → Mountain（陆地 ~10–15% 山地）</item>
    /// <item>elevation ≥ 0.62                                → Hill（陆地 ~15–20% 丘陵）</item>
    /// <item><b>height &lt; SeaLevel + BeachHeightBand 且 elevation &lt; BeachElevationCap → Beach</b>（MC 风格的 surface-by-height-proximity）</item>
    /// <item>否则按 (T, M) Whittaker 分类（Tundra/Taiga/Grassland/Forest/Swamp/Desert/Savanna/Rainforest）</item>
    /// </list>
    /// </para>
    /// <para>
    /// **极地低地**（temperature 很低 + elevation 中低）→ Tundra/Taiga，不再被 SnowPeak 强占；
    /// 只有同时满足"高海拔 + 寒冷"才是雪峰，吻合阿尔卑斯/喜马拉雅雪线模型。
    /// </para>
    /// <para>
    /// 海洋：按 h 与 SeaLevel 距离分 DeepOcean / Ocean / ShallowOcean 三层。
    /// 海滩用「最终高度 h 刚好在 SeaLevel 之上一薄层」(MC 1.18+ surface-by-height-proximity)。
    /// </para>
    /// </summary>
    public static class BiomeClassifier
    {
        // —— 陆地高度阈值（集中在此便于调参） ————————————————————————
        // 假设 elevation 噪声近似 [0,1] 均匀分布：
        //   Hill   [0.62, 0.78) ≈ 16%
        //   Mountain [0.78, 0.90) ≈ 12%
        //   SnowPeak [0.90, 1.0] ≈ 10% 候选，再 AND 温度条件，实际占比更低
        //   其余 ~62% 由 Whittaker (T,M) 派生群系覆盖
        private const float SnowPeakElevation = 0.90f;
        /// <summary>SnowPeak 还需要 temperature &lt; 此值（雪线温度上限）。
        /// 原值 0.15 仅在极地，结合高海拔几乎不可能 → 几乎没有 SnowPeak。
        /// 0.45 大致对应高山雪线（亚热带高山也可有雪盖）。</summary>
        private const float SnowPeakTemperatureMax = 0.45f;
        private const float MountainElevation = 0.78f;
        private const float HillElevation = 0.62f;

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
                if (height < seaLevel - DepthDeep) return TopDownTileTypes.DeepOcean;
                if (height < seaLevel - DepthShallow) return Dao.TileTypes.Ocean;
                return TopDownTileTypes.ShallowOcean;
            }

            // ─── 海岸薄层：height 紧贴 SeaLevel 之上 + 非海岸丘陵 → Beach ───
            // 放在 SnowPeak/Mountain/Hill 之前，海岸无论 elevation 噪声多高都会被沙覆盖（除非超 cap）。
            if (height - seaLevel < BeachHeightBand && elevation < BeachElevationCap)
            {
                return TopDownTileTypes.Beach;
            }

            // ─── 陆地：高度优先级 ───
            // SnowPeak 必须同时满足"高海拔 + 寒冷"（AND），符合现实雪线模型。
            // 极地低地走下方 (T,M) 路径 → Tundra；高山顶不冷也是 Mountain（如赤道高山多数无雪）。
            if (elevation >= SnowPeakElevation && temperature < SnowPeakTemperatureMax)
                return TopDownTileTypes.SnowPeak;
            if (elevation >= MountainElevation) return TopDownTileTypes.Mountain;
            if (elevation >= HillElevation) return TopDownTileTypes.Hill;

            // ─── 陆地：按 (T, M) Whittaker 派生 ───
            if (temperature < ColdMax)
            {
                return moisture < 0.5f ? TopDownTileTypes.Tundra : TopDownTileTypes.Taiga;
            }
            if (temperature < TemperateMax)
            {
                if (moisture < 0.30f) return TopDownTileTypes.Grassland;
                if (moisture < 0.70f) return TopDownTileTypes.Forest;
                return TopDownTileTypes.Swamp;
            }
            // 热带
            if (moisture < 0.30f) return TopDownTileTypes.Desert;
            if (moisture < 0.60f) return TopDownTileTypes.Savanna;
            return TopDownTileTypes.Rainforest;
        }
    }
}
