using System;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 3D 体素地图配置（heightmap-only，无地下、无破坏）。
    /// <para>派生类可 override <see cref="CreateGenerator"/> 切到自家生成器（如 IslandVoxelMapConfig）。</para>
    /// <para>
    /// **统一 MC 风格生成**：高度永远走 MC 1.18+ noise router（Continentalness + Erosion + Peaks-Valleys）。
    /// 当 <see cref="SharedPerlin"/> 非空时，biome 分类会走 2D <c>BiomeClassifier</c>（气候驱动 → Forest / Desert /
    /// Savanna / Taiga / Tundra 等），否则按高度 5 档（Ocean / Beach / Plains / Hills / Mountain / SnowPeak）。
    /// </para>
    /// </summary>
    [Serializable]
    public class VoxelMapConfig
    {
        [Header("Shared 2D Climate (optional, for biome classification only)")]
        [Tooltip("可选：内嵌一份 2D PerlinMapConfig，仅供 biome 分类使用（高度永远 MC noise router）。\n" +
                 "• 非空 → 3D biome 走 2D BiomeClassifier 用 elev/temp/moist 出气候驱动 biome（Forest/Desert/Savanna/Taiga/Tundra…）；\n" +
                 "• null → 3D biome 简单按高度 5 档（Ocean/Beach/Plains/Hills/Mountain/SnowPeak）。\n" +
                 "建议把 SharedPerlin.Seed / ChunkSize 与本类对齐，让 2D / 3D 看到的气候图同源。")]
        public PerlinMapConfig SharedPerlin;
        [Header("Identity")]
        [Tooltip("配置唯一 ID（与模板 DefaultConfigId 对齐；持久化按此 key 去重）。")]
        public string ConfigId = "default_voxel_3d";

        [Tooltip("Inspector / 日志显示用的友好名字。")]
        public string DisplayName = "Default Voxel 3D";

        [Tooltip("世界种子；改这一个值就生新世界。")]
        public int Seed = 20240501;

        [Header("Chunk")]
        [Tooltip("Chunk 在 X / Z 方向的尺寸（格）。建议 16。")]
        [Range(8, 64)] public int ChunkSize = 16;

        [Tooltip("世界最高方块层数（0..MaxHeight-1）。仅用于 clamp + 雪线计算。")]
        [Range(16, 255)] public int MaxHeight = 96;

        [Header("Terrain (sea level + global amplitude)")]
        [Tooltip("地表平均海平面高度（格）。低于该值的高度强制改 Water。")]
        [Range(0, 64)] public int SeaLevel = 12;

        [Tooltip("地形高度总幅值（block）—— 作 MC noise router 的 spline 总缩放参考。" +
                 "MC 默认 24（≈ ContinentalnessSpline 输出原幅）；调高让山势整体放大。")]
        [Range(1f, 80f)] public float TerrainAmplitude = 24f;

        [Header("MC-style Noise Router (1.18+ height pipeline)")]
        [Tooltip("Continentalness（大陆度）噪声频率 —— 低频，控制大尺度高度偏移（深海/海/海岸/低地/中地/高地）。\n" +
                 "越小大陆越大。建议 0.0015~0.003；> 0.005 会出大量蛜蛒状长条陆地。")]
        [Range(0.0005f, 0.01f)] public float ContinentalnessScale = 0.0018f;

        [Tooltip("Erosion（侵蚀度）噪声频率 —— 中频；-1 = 崎岖崇山、+1 = 侵蚀平原。控制山势振幅。0.008~0.015 推荐。")]
        [Range(0.001f, 0.04f)] public float ErosionScale = 0.012f;

        [Tooltip("Weirdness 噪声频率 —— pv = 1 − |3|w| − 2| 出三段峰/谷/平过渡。\n" +
                 "越小峰谷越平缓大尺度。建议 0.006~0.012；> 0.015 在地表上出小尺度条带感。")]
        [Range(0.001f, 0.04f)] public float WeirdnessScale = 0.008f;

        [Tooltip("MC noise router 振幅总缩放 [0.1..2]。叠在 TerrainAmplitude 之上：" +
                 "0.5 = 矮丘陵；1.0 = MC 标准；1.5 = 高耸火山。")]
        [Range(0.1f, 2f)] public float MCAmplitudeScale = 1f;

        [Tooltip("MC 模式下额外的高度盒模糊半径（block）。0 = 关闭；1~2 = 推荐（默认 1 平缓自然）；" +
                 "更大 = 山势更圆润，开销 (2R+1)² 倍噪声采样。")]
        [Range(0, 5)] public int MCHeightSmoothRadius = 1;

        [Header("Biome (height-based)")]
        [Tooltip("沙滩高度阈值（海平面以上 N 格内为沙滩）。")]
        [Range(0, 5)] public int BeachBand = 2;

        [Tooltip("雪线高度（高于此值用 Snow 顶面）。")]
        [Range(20, 200)] public int SnowLine = 60;

        // ── 群系流水线（VoxelClimateSampler + VoxelBiomeClassifier 用）────
        [Header("Climate / Elevation noise (biome 分类用)")]
        [Tooltip("独立海拔噪声尺度 —— 控制丘陵/山地划分的尺度。值越小山系越大。")]
        [Min(0.0001f)] public float ElevationScale = 0.01f;
        [Range(1, 6)]  public int   ElevationOctaves     = 4;
        [Range(0.1f, 1f)] public float ElevationPersistence = 0.5f;
        [Range(1f, 4f)]   public float ElevationLacunarity  = 2f;

        [Header("Climate / Temperature")]
        [Tooltip("赤道基础温度。0.85 赤道酷热；0.6 整体偏凉。")]
        [Range(0f, 1f)] public float BaseTemperature = 0.7f;
        [Tooltip("纬度周期（block）：多少格等于一整圈赤道→极地→赤道。")]
        [Min(1f)] public float LatitudePeriod = 4000f;
        [Tooltip("纬度对温度的影响权重 ∈ [0,1]。0 = 全球同温；0.7 = 标准。")]
        [Range(0f, 1f)] public float LatitudeStrength = 0.5f;
        [Tooltip("海拔降温率 ∈ [0,1]：每单位归一化海拔扣多少温度。0.4 雪线明显。")]
        [Range(0f, 1f)] public float ElevationLapseRate = 0.4f;
        [Tooltip("温度局部扰动频率：让同纬度也有冷岛/暖岛。0.005 大块（推荐）。")]
        [Min(0.0001f)] public float TemperatureNoiseScale    = 0.005f;
        [Tooltip("温度局部扰动幅度 ∈ [0,0.5]。")]
        [Range(0f, 0.5f)] public float TemperatureNoiseStrength = 0.1f;

        [Header("Climate / Moisture")]
        [Tooltip("基础湿度（归一化 [0,1]）。越高整体越湿。")]
        [Range(0f, 1f)] public float BaseMoisture = 0.5f;
        [Tooltip("湿度 fBm 频率。")]
        [Min(0.0001f)] public float MoistureScale       = 0.005f;
        [Range(1, 6)]  public int   MoistureOctaves     = 3;
        [Range(0.1f, 1f)] public float MoisturePersistence = 0.5f;
        [Range(1f, 4f)]   public float MoistureLacunarity  = 2f;
        [Tooltip("海岸湿润加成 ∈ [0,1]：让海岸带天然偏湿。")]
        [Range(0f, 1f)] public float OceanMoistureBoost = 0.4f;
        [Tooltip("高海拔变干强度 ∈ [0,1]：山顶天然偏干。")]
        [Range(0f, 1f)] public float ElevationDryness   = 0.3f;

        // ──────────────────────────────────────────────────────────────
        // 构造与工厂
        // ──────────────────────────────────────────────────────────────

        /// <summary>无参构造 —— 保留 Inspector / Unity 反序列化默认值。</summary>
        public VoxelMapConfig() { }

        /// <summary>便利构造 —— 业务侧 / 模板按 (ConfigId, DisplayName) 直建。</summary>
        public VoxelMapConfig(string configId, string displayName)
        {
            ConfigId    = configId;
            DisplayName = displayName;
        }

        /// <summary>
        /// **生成器工厂** —— 派生 Config 时 override 切自家生成器（如 BiomeVoxelGenerator）。
        /// 默认返回 fBm Perlin heightmap 生成器。
        /// </summary>
        public virtual IVoxelMapGenerator CreateGenerator() => new VoxelHeightmapGenerator(this);
    }
}
