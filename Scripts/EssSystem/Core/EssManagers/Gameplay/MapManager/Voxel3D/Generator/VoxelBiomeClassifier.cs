using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 3D 体素群系分类器（与 2D <c>BiomeClassifier</c> 平行）。
    /// 决策树：
    /// <list type="number">
    ///   <item>height ≤ sea → <see cref="VoxelBiomeIds.Ocean"/></item>
    ///   <item>height ≤ sea + beachBand 且 elevation01 &lt; 0.55 → <see cref="VoxelBiomeIds.Beach"/></item>
    ///   <item>elevation01 ≥ 0.90 且 temperature01 &lt; 0.45 → <see cref="VoxelBiomeIds.SnowPeak"/></item>
    ///   <item>elevation01 ≥ 0.78 → <see cref="VoxelBiomeIds.Mountain"/></item>
    ///   <item>elevation01 ≥ 0.62 → <see cref="VoxelBiomeIds.Hills"/></item>
    ///   <item>否则按 (Temperature, Moisture) Whittaker 分到 Tundra/Taiga/Plains/Forest/Desert/Savanna</item>
    /// </list>
    /// </summary>
    public static class VoxelBiomeClassifier
    {
        // 高度阈值（elevation01 ∈ [0,1]）
        private const float SnowPeakElevation        = 0.90f;
        private const float SnowPeakTemperatureMax   = 0.45f;
        private const float MountainElevation        = 0.78f;
        private const float HillElevation            = 0.62f;

        // Beach 限制：海岸丘陵不算沙滩
        private const float BeachElevationCap        = 0.55f;

        // 温湿度断点
        private const float ColdMax                  = 0.30f;
        private const float TemperateMax             = 0.60f;

        /// <summary>
        /// 群系分类。
        /// </summary>
        /// <param name="height">最终高度（block）。</param>
        /// <param name="seaLevel">海平面（block）。</param>
        /// <param name="beachBand">沙滩判定带（高度差，block）。</param>
        /// <param name="elevation01">独立海拔噪声 [0,1]。</param>
        /// <param name="temperature01">温度 [0,1]，0=极地、1=赤道。</param>
        /// <param name="moisture01">湿度 [0,1]，0=干、1=湿。</param>
        public static byte Classify(int height, int seaLevel, int beachBand,
            float elevation01, float temperature01, float moisture01)
        {
            if (height <= seaLevel) return VoxelBiomeIds.Ocean;

            if (height - seaLevel <= beachBand && elevation01 < BeachElevationCap)
                return VoxelBiomeIds.Beach;

            // 山系优先
            if (elevation01 >= SnowPeakElevation && temperature01 < SnowPeakTemperatureMax)
                return VoxelBiomeIds.SnowPeak;
            if (elevation01 >= MountainElevation) return VoxelBiomeIds.Mountain;
            if (elevation01 >= HillElevation)     return VoxelBiomeIds.Hills;

            // Whittaker 温湿度
            if (temperature01 < ColdMax)
                return moisture01 < 0.5f ? VoxelBiomeIds.Tundra : VoxelBiomeIds.Taiga;

            if (temperature01 < TemperateMax)
                return moisture01 < 0.4f ? VoxelBiomeIds.Plains : VoxelBiomeIds.Forest;

            // 热带
            return moisture01 < 0.40f ? VoxelBiomeIds.Desert : VoxelBiomeIds.Savanna;
        }
    }
}
