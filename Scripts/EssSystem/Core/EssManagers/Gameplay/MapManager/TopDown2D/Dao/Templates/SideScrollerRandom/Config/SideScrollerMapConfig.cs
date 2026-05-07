using System;
using EssSystem.Core.Util;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Generator;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Generator;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Config
{
    /// <summary>
    /// 横版 2D 随机大世界配置（骨架）。
    /// <para>
    /// 当前生成器仅做"地表线 + 土/石分层 + 基岩底封"的最小可玩样例，业务侧后续可在
    /// <see cref="SideScrollerMapGenerator"/> 中扩展矿脉、洞穴、群系（雪/沙/草地）、敌人区域等。
    /// </para>
    /// </summary>
    [Serializable]
    public class SideScrollerMapConfig : MapConfig
    {
        // ─── 世界尺寸（纵向） ──────────────────────────────────
        [InspectorHelp("世界海平面高度（Tile 数）。Y 大于此值是地表 / 天空，小于此值才有地形。")]
        public int SeaLevelY = 64;

        [InspectorHelp("基岩深度上限（Y 小于此值全部填 Bedrock）。")]
        public int BedrockY = 0;

        [InspectorHelp("土层厚度（地表草皮以下的 Dirt 格数）。")]
        public int DirtThickness = 5;

        // ─── 地表噪声 ──────────────────────────────────────────
        [InspectorHelp("随机种子。同一 Seed 必出同一地形。")]
        public int Seed = 12345;

        [InspectorHelp("地表起伏振幅（Tile 数）。海平面 ± Amplitude 之间。")]
        [Range(0, 64)] public int SurfaceAmplitude = 12;

        [InspectorHelp("地表 Perlin 频率：值越大山丘越密。0.01~0.1 常见。")]
        [Range(0.001f, 0.5f)] public float SurfaceFrequency = 0.03f;

        [InspectorHelp("Perlin 倍频层数。1=单频；3=较自然。")]
        [Range(1, 6)] public int SurfaceOctaves = 3;

        [InspectorHelp("倍频持续度（每多一层振幅衰减比）。0.5 经典值。")]
        [Range(0.1f, 0.9f)] public float SurfacePersistence = 0.5f;

        public SideScrollerMapConfig() { }

        public SideScrollerMapConfig(string configId, string displayName) : base(configId, displayName)
        {
            ChunkSize = 16;
        }

        public override IMapGenerator CreateGenerator() => new SideScrollerMapGenerator(this);
    }
}
