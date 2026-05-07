using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 3D 体素地图配置（heightmap-only，无地下、无破坏）。
    /// </summary>
    [Serializable]
    public class VoxelMapConfig
    {
        [Header("Identity")]
        [Tooltip("世界种子；改这一个值就生新世界。")]
        public int Seed = 20240501;

        [Header("Chunk")]
        [Tooltip("Chunk 在 X / Z 方向的尺寸（格）。建议 16。")]
        [Range(8, 64)] public int ChunkSize = 16;

        [Tooltip("世界最高方块层数（0..MaxHeight-1）。仅用于 clamp + 雪线计算。")]
        [Range(16, 255)] public int MaxHeight = 96;

        [Header("Terrain (Perlin)")]
        [Tooltip("地表平均海平面高度（格）。低于该值的高度强制改 Water。")]
        [Range(0, 64)] public int SeaLevel = 12;

        [Tooltip("基础高度噪声尺度。值越小山体越平缓。")]
        public float TerrainScale = 0.025f;

        [Tooltip("地形高度幅值（峰谷差）。最终 height = Base + Perlin * Amplitude。")]
        [Range(1f, 80f)] public float TerrainAmplitude = 24f;

        [Tooltip("地形高度基线（最低山脚高度）。")]
        [Range(0, 64)] public int TerrainBase = 6;

        [Tooltip("Octaves 数量；越大细节越多，性能更慢。")]
        [Range(1, 6)] public int Octaves = 4;

        [Range(0.1f, 1f)] public float Persistence = 0.5f;
        [Range(1f, 4f)]   public float Lacunarity  = 2.0f;

        [Header("Biome (height-based)")]
        [Tooltip("沙滩高度阈值（海平面以上 N 格内为沙滩）。")]
        [Range(0, 5)] public int BeachBand = 2;

        [Tooltip("雪线高度（高于此值用 Snow 顶面）。")]
        [Range(20, 200)] public int SnowLine = 60;
    }
}
