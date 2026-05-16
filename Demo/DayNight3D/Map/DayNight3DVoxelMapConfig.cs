using System;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator;

namespace Demo.DayNight3D.Map
{
    /// <summary>
    /// DayNight3D Demo 专属体素地图配置 —— 在默认 <see cref="VoxelMapConfig"/> 基础上：
    /// <list type="bullet">
    ///   <item><b>持久化隔离</b>：<see cref="VoxelMapConfig.ConfigId"/> = <c>"daynight3d_voxel"</c>。</item>
    ///   <item><b>群系阈值</b>：SnowLine=32 / BeachBand=3，让雪线与沙滩在当前振幅下都可见。</item>
    ///   <item><b>世界 AABB</b>：默认 30×30 chunk 安全网（钳玩家不出地图）。</item>
    ///   <item><b>单岛生成</b>：<see cref="CreateGenerator"/> 走 <see cref="DayNight3DIslandGenerator"/>，
    ///         径向 mask 驱动 MC Continentalness，岛屿天然居中且自带海岸过渡，永远不会被矩形切开。</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class DayNight3DVoxelMapConfig : VoxelMapConfig
    {
        // ── World Bounds（玩家钳位用 safety net）────────────────────────
        [Header("World Bounds (safety net for player clamp)")]
        [Tooltip("是否启用世界 AABB 钳位（防玩家走出地图）。岛屿形状由 Island 字段决定，与此无关。")]
        public bool BoundedWorld = true;

        [Tooltip("世界中心 chunk 坐标（玩家从此附近出生）。")]
        public Vector2Int WorldCenterChunk = Vector2Int.zero;

        [Tooltip("世界半边长（chunk 数）—— 世界正方形 AABB 边长 = 2 × HalfChunksXZ。\n" +
                 "默认 15 → 30×30 chunks → 480×480 block。")]
        [Range(2, 64)] public int WorldHalfChunksXZ = 15;

        // ── Island Shape（岛屿形状，决定可视陆地）─────────────────────────
        [Header("Island Shape")]
        [Tooltip("岛屿名义半径（block）—— 海岸线大致在此半径处。\n" +
                 "默认 160 = AABB 半边 240 - 留 80 block 边缘海域。")]
        [Min(8f)] public float IslandRadiusBlocks = 160f;

        [Tooltip("海岸 → 深海过渡带宽度（block）。在 [Radius, Radius+Falloff] 区间陆地高度衰减入海。\n" +
                 "32~80 推荐：值越大海岸越柔缓 / 沙滩越广。")]
        [Min(1f)] public float CoastFalloffBlocks = 48f;

        [Tooltip("海岸不规则度 [0..1]。0 = 标准圆岛；0.3~0.5 = 自然海湾；> 0.7 海岸高度扭曲，可能裂成岛群。")]
        [Range(0f, 1f)] public float ShapeWarpStrength = 0.35f;

        [Tooltip("海岸扭曲噪声频率（越小波长越大）。0.005~0.02 推荐。")]
        [Range(0.001f, 0.05f)] public float ShapeWarpScale = 0.012f;

        [Tooltip("岛形锋锐度 [0.5..4]。1 = 锥形圆缓；1.5 = 圆润（默认）；2.5 = 顶部更尖；3+ = 高原平台。")]
        [Range(0.5f, 4f)] public float IslandShapePower = 1.5f;

        [Tooltip("径向 mask 在 Continentalness 上的权重 [0..1]。\n" +
                 "1 = 完全径向（最干净的同心圆岛）；0.85 = 岛形主导 + MC 扰动（默认，海岸自然不规整）；\n" +
                 "0 = 退化为 AABB 内全 MC（看不到岛形）。")]
        [Range(0f, 1f)] public float ContinentalnessFromMaskWeight = 0.85f;

        public DayNight3DVoxelMapConfig() : base("daynight3d_voxel", "DayNight3D Voxel World")
        {
            // ── DayNight3D 专属 base 默认 ────────────────────────────────
            Seed       = 20240509;
            SnowLine   = 32;
            BeachBand  = 3;
        }

        // ── 工厂：单岛生成器（径向 mask + MC noise router）───────────────
        public override IVoxelMapGenerator CreateGenerator()
            => new DayNight3DIslandGenerator(this);

        // ── 工具：世界中心 / AABB ─────────────────────────────────────────

        /// <summary>世界中心世界 (x, z)（block 单位，含半 chunk 偏移让中心落在 chunk 几何中心）。</summary>
        public Vector2 WorldCenterWorld
        {
            get
            {
                var size = ChunkSize;
                return new Vector2(
                    WorldCenterChunk.x * size + size * 0.5f,
                    WorldCenterChunk.y * size + size * 0.5f);
            }
        }

        /// <summary>世界正方形 AABB（chunk 矩形 → block AABB）—— 仅作 Player 钳位 safety net。</summary>
        public Rect WorldRect
        {
            get
            {
                var size = ChunkSize;
                var minX = (WorldCenterChunk.x - WorldHalfChunksXZ) * size;
                var minZ = (WorldCenterChunk.y - WorldHalfChunksXZ) * size;
                var w    = WorldHalfChunksXZ * 2 * size;
                return new Rect(minX, minZ, w, w);
            }
        }
    }
}
