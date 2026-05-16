using System;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Generator;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config
{
    /// <summary>
    /// Perlin 噪声 2D 平面地图配置 —— 海洋 / 陆地二分。
    /// <para>
    /// 通过多倍频 fBm（fractal Brownian motion）累加 Unity <see cref="Mathf.PerlinNoise"/>，
    /// 对每个世界 Tile 采样一次，与 <see cref="SeaLevel"/> 比较得到 Ocean / Land。
    /// </para>
    /// </summary>
    [Serializable]
    public class PerlinMapConfig : MapConfig
    {
        [InspectorHelp("随机种子。\n" +
                       "• 相同 Seed + 相同其他参数 = 完全一致的地形（可复现 bug / 截图）\n" +
                       "• 想换一张新地图但保持风格不变：只改这个值。")]
        public int Seed = 20240501;

        [InspectorHelp("噪声采样频率（每 Tile 推进多少噪声空间）。控制「特征尺寸」：\n" +
                       "• 0.01 ≈ 大陆级，波长约 100 Tile\n" +
                       "• 0.02 ≈ 岛屿级（推荐起点，波长约 50 Tile）\n" +
                       "• 0.05 ≈ 池塘 / 小岛级（波长约 20 Tile）\n" +
                       "• 0.1+ 噪点过多，像随机散点")]
        [Min(0.0001f)] public float NoiseScale = 0.02f;

        [InspectorHelp("fBm 倍频层数。1 = 纯单层 Perlin；2~6 越多细节越丰富。\n" +
                       "性能：每加 1 层 ≈ 多一次 PerlinNoise 采样。")]
        [Min(1)] public int Octaves = 3;

        [InspectorHelp("每层振幅衰减系数 ∈ (0,1)。\n" +
                       "• 0.2 ~ 0.3：海岸线非常平滑，细节几乎消失\n" +
                       "• 0.4 ~ 0.5：自然地貌（推荐）\n" +
                       "• 0.6+：海岸线出现锯齿、孤岛碎片增多")]
        [Range(0f, 1f)] public float Persistence = 0.4f;

        [InspectorHelp("每层频率倍增系数 &gt; 1。通常保持 2 即可，调风格优先调 Persistence。")]
        [Min(1f)] public float Lacunarity = 2f;

        [InspectorHelp("海平面阈值（最终高度图已归一化到 [0,1]）。\n" +
                       "• 噪声 &lt; 此值 → 海洋；≥ 此值 → 陆地\n" +
                       "• 0.3 = 大陆为主，零星海洋\n" +
                       "• 0.5 = 海陆约各半\n" +
                       "• 0.58 = 大片海洋 + 成片大陆（推荐）\n" +
                       "• 0.7 = 海洋为主，零星岛屿")]
        [Range(0f, 1f)] public float SeaLevel = 0.58f;

        [Header("MC-like Terrain Parameters")]
        [InspectorHelp("大陆性采样频率：控制大陆/海洋的大尺度分布。值越小，大陆块越大。")]
        [Min(0.0001f)] public float ContinentalnessScale = 0.0008f;

        [InspectorHelp("大陆性权重：越高越由大陆性决定海陆骨架，越低越接近细节噪声。")]
        [Range(0f, 1f)] public float ContinentalnessWeight = 0.88f;

        [InspectorHelp("侵蚀性采样频率：控制被削平/压低的低地与河谷区域尺度。")]
        [Min(0.0001f)] public float ErosionScale = 0.004f;

        [InspectorHelp("侵蚀性权重：越高越削弱高地与山脊，使地形更平缓。")]
        [Range(0f, 1f)] public float ErosionWeight = 0.55f;

        [InspectorHelp("脊柱性采样频率：控制山脉/山脊带的尺度。")]
        [Min(0.0001f)] public float RidgesScale = 0.006f;

        [InspectorHelp("脊柱性权重：越高山脉越明显、越连续。")]
        [Range(0f, 1f)] public float RidgesWeight = 0.65f;

        [InspectorHelp("垂直缩放：控制整体高度起伏幅度。0.5 更平，1 标准，1.5+ 更极端。")]
        [Range(0f, 2f)] public float VerticalScale = 1f;

        [InspectorHelp("气候是否与大陆性/水循环/侵蚀/山脊强关联。\n" +
                       "关闭：温湿度相对独立，适合魔幻场景。\n" +
                       "开启：大陆性与海陆水循环会影响侵蚀，侵蚀/山脊随后影响温湿度，更贴近现实。")]
        public bool ClimateCoupledToTerrain = false;

        [Header("Continent Mask (大陆分层)")]
        [InspectorHelp("大陆掩膜频率：决定「单块大陆」的尺度（一层独立的超低频 Perlin）。\n" +
                       "• 0.0008 ≈ 行星级，单大陆跨 ~1300 Tile（推荐：大块大陆）\n" +
                       "• 0.0015 ≈ 次大陆级，单大陆跨 ~700 Tile\n" +
                       "• 0.003  ≈ 群岛级，多个中型大陆\n" +
                       "• 0      = 关闭分层，仅用下面 fBm 细节")]
        [Min(0f)] public float ContinentScale = 0.0008f;

        [InspectorHelp("大陆掩膜权重：最终高度 = Lerp(fBm 细节, 大陆掩膜, 权重)。\n" +
                       "• 0   = 完全由 fBm 决定（容易碎裂成小岛）\n" +
                       "• 0.5 = 大陆形状与细节各半\n" +
                       "• 0.75= 大陆主导，海岸线由 fBm 加扰动（推荐）\n" +
                       "• 1   = 完全由大陆掩膜决定，海岸线最平滑")]
        [Range(0f, 1f)] public float ContinentWeight = 0.75f;

        // 海滩判定改为「height 刚好在 SeaLevel 上方 ~3.5%」的固定薄层策略，
        // 逻辑内置于 BiomeClassifier.BeachHeightBand / BeachElevationCap，不再暴露参数。

        // ─── 地幔线 / 海拔（独立于海陆判定） ──────────────────────────
        [Header("Elevation / 地幔线")]
        [InspectorHelp("地幔线 fBm 频率：决定山脉/丘陵的尺度。\n" +
                       "• 0.005 ≈ 巨型山脉，单脉横跨数百 Tile\n" +
                       "• 0.01  ≈ 山脉级（推荐起点）\n" +
                       "• 0.02  ≈ 丘陵级\n" +
                       "• 0.05+ ≈ 碎丘陵 / 噪点")]
        [Min(0.0001f)] public float ElevationScale = 0.01f;

        [InspectorHelp("地幔线 fBm 倍频层数。2 = 平缓山脊，4 = 主脉 + 支脉 + 山谷（推荐），6+ 细节多但可能碎片化。")]
        [Min(1)] public int ElevationOctaves = 4;

        [InspectorHelp("地幔线持续度：0.3 平滑；0.5 自然山脉（推荐）；0.7+ 崎岖多细碎峰丛。")]
        [Range(0f, 1f)] public float ElevationPersistence = 0.5f;

        [InspectorHelp("地幔线间隙度：每层频率倍增系数。通常保持 2。")]
        [Min(1f)] public float ElevationLacunarity = 2f;

        [InspectorHelp("大陆掩膜对海拔的抬升强度 ∈ [0,1]：决定「山脉是否倾向于长在大陆中心」。\n" +
                       "• 0   = 海拔与大陆完全无关\n" +
                       "• 0.5 = 平衡（推荐）\n" +
                       "• 1   = 陆地必高、海洋必低")]
        [Range(0f, 1f)] public float ElevationContinentLift = 0.5f;

        // ─── 温度（由纬度 + 海拔 + 噪声派生） ──────────────────────────
        [Header("Temperature / 温度")]
        [InspectorHelp("赤道基础温度（归一化 [0,1]）。0.85 = 赤道酷热（推荐）；0.6 = 整体偏凉；1.0 = 极端炎热。")]
        [Range(0f, 1f)] public float BaseTemperature = 0.85f;

        [InspectorHelp("纬度周期：多少 Tile 等于「赤道→极地→赤道」一整圈 sin。\n" +
                       "• 2000 = 紧凑气候带\n• 4000 = 标准（推荐）\n• 8000+ = 巨型气候带")]
        [Min(1f)] public float LatitudePeriod = 4000f;

        [InspectorHelp("纬度对温度的影响权重 ∈ [0,1]。0 = 全球同温；0.7 = 标准（推荐）；1 = 极端。")]
        [Range(0f, 1f)] public float LatitudeStrength = 0.7f;

        [InspectorHelp("海拔降温率 ∈ [0,1]：每单位归一化海拔扣除多少温度。\n0.2 微凉；0.4 雪线明显（推荐）；0.7 山顶必结冰。")]
        [Range(0f, 1f)] public float ElevationLapseRate = 0.4f;

        [InspectorHelp("温度局部扰动频率：让同一气候带也有冷岛/暖岛。0.005 大型扰动（推荐），0.02 高频破碎。")]
        [Min(0.0001f)] public float TemperatureNoiseScale = 0.005f;

        [InspectorHelp("温度局部扰动幅度 ∈ [0,0.5]。0 无扰动；0.1 自然小波动（推荐）；0.3+ 同纬度差异巨大。")]
        [Range(0f, 0.5f)] public float TemperatureNoiseStrength = 0.1f;

        // ─── 湿度（fBm + 海陆 + 海拔） ───────────────────────────────
        [Header("Moisture / 湿度")]
        [InspectorHelp("基础湿度（归一化 [0,1]）。越高整体越湿，群系更容易偏森林/雨林/沼泽。")]
        [Range(0f, 1f)] public float BaseMoisture = 0.5f;

        [InspectorHelp("湿度 fBm 频率：决定干湿区域大小。0.003 大块干湿带（推荐），0.01 中等斑块，0.03+ 细碎变化。")]
        [Min(0.0001f)] public float MoistureScale = 0.005f;

        [InspectorHelp("湿度 fBm 倍频层数。2 平滑过渡，3 标准（推荐），5+ 复杂局部细节。")]
        [Min(1)] public int MoistureOctaves = 3;

        [InspectorHelp("湿度 fBm 振幅衰减。0.5 是经典默认。")]
        [Range(0f, 1f)] public float MoisturePersistence = 0.5f;

        [InspectorHelp("湿度 fBm 频率倍增。2.0 是经典默认。")]
        [Min(1f)] public float MoistureLacunarity = 2f;

        [InspectorHelp("海洋 / 海岸湿度加成 ∈ [0,1]：让海洋天然湿润、海岸偏湿。\n" +
                       "• 0   = 关闭海陆耦合\n• 0.4 = 海岸明显偏湿（推荐）\n• 0.8 = 主要由离海远近决定")]
        [Range(0f, 1f)] public float OceanMoistureBoost = 0.4f;

        [InspectorHelp("高海拔变干强度 ∈ [0,1]：山顶天然偏干（雪山而非雨林）。\n• 0 不影响\n• 0.3 山顶偏干（推荐）\n• 0.7 高山极干")]
        [Range(0f, 1f)] public float ElevationDryness = 0.3f;

        // ─── 河流 / 湖泊（MC-like：噪声河道 Biome + 陆地/湿度/低地约束） ────────
        [Header("Rivers / 河流")]
        [InspectorHelp("是否生成河流与湖泊。关闭则跳过 RiverTracer 阶段。")]
        public bool RiverEnabled = true;

        [InspectorHelp("河流稀疏度：值越高河流越少，值越低河流越密。推荐 40~100。")]
        [Min(1)] public int RiverFlowThreshold = 45;

        [InspectorHelp("河流基础宽度（格）：主河道周围额外染色半径。\n• 0 细线\n• 1 自然主河（推荐）\n• 3+ 宽河/大江")]
        [Range(0f, 8f)] public float RiverWidthPerFlowDecade = 1f;

        [InspectorHelp("河流蜿蜒强度 ∈ [0,1]：越大越弯曲，但过大会显得绕。推荐 0.35~0.5。")]
        [Range(0f, 1f)] public float RiverMeanderStrength = 0.45f;

        [InspectorHelp("湖泊生成概率 ∈ [0,1]：只在内陆盆地候选中抽样。调低可减少湖泊。")]
        [Range(0f, 1f)] public float RiverLakeChance = 0.015f;

        [InspectorHelp("湖泊半径（格）：实际边缘会用噪声扰动。")]
        [Range(1, 12)] public int RiverLakeRadius = 4;

        [InspectorHelp("湖盆/出口搜索半径：寻路卡住或生成湖泊时在此半径内寻找出水口。")]
        [Range(2, 32)] public int RiverPondEscapeRadius = 10;

        public PerlinMapConfig() { }

        public PerlinMapConfig(string configId, string displayName) : base(configId, displayName) { }

        // ─── 链式 setter（与项目其它 Config 风格一致） ────────────────
        public PerlinMapConfig WithSeed(int seed) { Seed = seed; return this; }
        public PerlinMapConfig WithChunkSize(int size) { ChunkSize = size; return this; }
        public PerlinMapConfig WithNoiseScale(float scale) { NoiseScale = scale; return this; }
        public PerlinMapConfig WithOctaves(int octaves) { Octaves = octaves; return this; }
        public PerlinMapConfig WithPersistence(float p) { Persistence = p; return this; }
        public PerlinMapConfig WithLacunarity(float l) { Lacunarity = l; return this; }
        public PerlinMapConfig WithSeaLevel(float v) { SeaLevel = v; return this; }
        public PerlinMapConfig WithContinentalnessScale(float v) { ContinentalnessScale = v; return this; }
        public PerlinMapConfig WithContinentalnessWeight(float v) { ContinentalnessWeight = v; return this; }
        public PerlinMapConfig WithErosionScale(float v) { ErosionScale = v; return this; }
        public PerlinMapConfig WithErosionWeight(float v) { ErosionWeight = v; return this; }
        public PerlinMapConfig WithRidgesScale(float v) { RidgesScale = v; return this; }
        public PerlinMapConfig WithRidgesWeight(float v) { RidgesWeight = v; return this; }
        public PerlinMapConfig WithVerticalScale(float v) { VerticalScale = v; return this; }
        public PerlinMapConfig WithClimateCoupledToTerrain(bool v) { ClimateCoupledToTerrain = v; return this; }
        public PerlinMapConfig WithContinentScale(float s) { ContinentScale = s; return this; }
        public PerlinMapConfig WithContinentWeight(float w) { ContinentWeight = w; return this; }
        public PerlinMapConfig WithElevationScale(float s) { ElevationScale = s; return this; }
        public PerlinMapConfig WithElevationOctaves(int o) { ElevationOctaves = o; return this; }
        public PerlinMapConfig WithElevationPersistence(float p) { ElevationPersistence = p; return this; }
        public PerlinMapConfig WithElevationLacunarity(float l) { ElevationLacunarity = l; return this; }
        public PerlinMapConfig WithElevationContinentLift(float v) { ElevationContinentLift = v; return this; }
        public PerlinMapConfig WithBaseTemperature(float v) { BaseTemperature = v; return this; }
        public PerlinMapConfig WithLatitudePeriod(float v) { LatitudePeriod = v; return this; }
        public PerlinMapConfig WithLatitudeStrength(float v) { LatitudeStrength = v; return this; }
        public PerlinMapConfig WithElevationLapseRate(float v) { ElevationLapseRate = v; return this; }
        public PerlinMapConfig WithTemperatureNoiseScale(float v) { TemperatureNoiseScale = v; return this; }
        public PerlinMapConfig WithTemperatureNoiseStrength(float v) { TemperatureNoiseStrength = v; return this; }
        public PerlinMapConfig WithMoistureScale(float v) { MoistureScale = v; return this; }
        public PerlinMapConfig WithBaseMoisture(float v) { BaseMoisture = v; return this; }
        public PerlinMapConfig WithMoistureOctaves(int v) { MoistureOctaves = v; return this; }
        public PerlinMapConfig WithMoisturePersistence(float v) { MoisturePersistence = v; return this; }
        public PerlinMapConfig WithMoistureLacunarity(float v) { MoistureLacunarity = v; return this; }
        public PerlinMapConfig WithOceanMoistureBoost(float v) { OceanMoistureBoost = v; return this; }
        public PerlinMapConfig WithElevationDryness(float v) { ElevationDryness = v; return this; }
        public PerlinMapConfig WithRiverEnabled(bool v) { RiverEnabled = v; return this; }
        public PerlinMapConfig WithRiverFlowThreshold(int v) { RiverFlowThreshold = v; return this; }
        public PerlinMapConfig WithRiverWidthPerFlowDecade(float v) { RiverWidthPerFlowDecade = v; return this; }
        public PerlinMapConfig WithRiverMeanderStrength(float v) { RiverMeanderStrength = v; return this; }
        public PerlinMapConfig WithRiverLakeChance(float v) { RiverLakeChance = v; return this; }
        public PerlinMapConfig WithRiverLakeRadius(int v) { RiverLakeRadius = v; return this; }
        public PerlinMapConfig WithRiverPondEscapeRadius(int v) { RiverPondEscapeRadius = v; return this; }

        public override IMapGenerator CreateGenerator() => new PerlinMapGenerator(this);
    }
}
