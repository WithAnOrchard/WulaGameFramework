using System;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Generator;
using Demo.DayNight.Map.Generator;

namespace Demo.DayNight.Map.Config
{
    /// <summary>
    /// 昼夜求生 - 海岛地图配置：**有界**世界 + Perlin 噪声群系。
    /// <para>
    /// 默认 20×20 chunks（≈ 320×320 tiles，按 ChunkSize=16），围绕原点 (0,0) 居中。
    /// 越界区块全部填 <c>DeepOcean</c>，不会无限延伸；玩家能从地图边界看到无限大海。
    /// </para>
    /// </summary>
    [Serializable]
    public class IslandSurvivalMapConfig : MapConfig
    {
        /// <summary>主随机种子（影响群系噪声 + 海岸线扰动）。</summary>
        public int Seed = 1337;

        /// <summary>世界宽 / 高（chunks）。20 ⇒ 20×20 区块的有界世界。</summary>
        public int WorldSizeChunks = 20;

        /// <summary>世界左下角的 chunk 坐标。默认让世界以原点 (0,0) 为中心：-10。</summary>
        public int OriginChunkX = -10;
        public int OriginChunkY = -10;

        /// <summary>海岸线 fBm 抖动幅度（0=完美圆形，0.2=略不规则，0.4=破碎海岸）。</summary>
        public float ShorelineNoise = 0.30f;

        /// <summary>海岸线 fBm 基频（值越大形状越细碎；过大会变成噪点）。</summary>
        public float ShorelineFrequency = 0.018f;

        /// <summary>**Angular Perlin 振幅**（径向偏移幅度，相对 maxRadius）。
        /// 0=完美圆形；0.40=明显湾/半岛；0.55=显著破碎；0.7+=狭长不规则岛。
        /// 经 taper(cd) 缩放后近边缘归零，海岛仍硬约束在世界框内。</summary>
        public float WarpAmplitude = 0.55f;

        /// <summary>**[已废弃]** 旧版 xy-warp Perlin 频率，新角向 Perlin 公式不再使用。
        /// 留字段仅为兼容已持久化的旧 config（避免 JsonUtility 反序列化异常）。</summary>
        [System.Obsolete("Not used since 2026-05; retained for serialization compatibility.")]
        public float WarpFrequency = 0.012f;

        /// <summary>群系 Perlin 频率（温度/湿度噪声采样步长，越大群系越细碎）。</summary>
        public float BiomeFrequency = 0.025f;

        /// <summary>归一化距离 &lt; 此值 + 温度偏低时升级为山地（中央山脊）。0=禁用。</summary>
        public float MountainCenterRatio = 0.18f;

        /// <summary>归一化距离 ≥ 此值开始过渡到深海。</summary>
        public float DeepOceanThreshold = 1.0f;

        /// <summary>归一化距离 ≥ 此值为浅海。注意：(DeepOcean - ShallowOcean) 决定浅海带宽。</summary>
        public float ShallowOceanThreshold = 0.97f;

        /// <summary>归一化距离 ≥ 此值进入海滩"候选"区。
        /// 注意：beach 不是连续环 —— ResolveTile 内还会用 fBm 抽取一部分 beach 还原为 Grassland，
        /// 让海岸看起来一段沙、一段草，而不是死板的沙环。</summary>
        public float BeachThreshold = 0.93f;

        /// <summary>海滩破碎度 [0,1]：0=连续沙环，0.5=半数海岸是沙、半数是草，1=几乎无沙岸。
        /// 默认 0.55 给"零散沙岸"的视觉。</summary>
        public float BeachBreakup = 0.55f;

        public IslandSurvivalMapConfig() { ChunkSize = 16; }

        public IslandSurvivalMapConfig(string id, string name) : base(id, name) { ChunkSize = 16; }

        public override IMapGenerator CreateGenerator() => new IslandSurvivalGenerator(this);
    }
}
