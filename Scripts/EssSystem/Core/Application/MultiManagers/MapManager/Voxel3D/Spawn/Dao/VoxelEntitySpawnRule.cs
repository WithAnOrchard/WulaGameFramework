using System;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Spawn.Dao
{
    /// <summary>
    /// 单条体素实体生成规则（与 2D <c>EntitySpawnRule</c> 平行）。
    /// <para>过滤器读 <see cref="VoxelChunk"/> 的 column 三值（TopBlock + Height）；
    /// 命中位置 spawn 在 <c>(wx + 0.5, height + 1, wz + 0.5)</c>（站在 column 顶面之上）。</para>
    /// <para>**确定性**：所有 RNG 走 <c>ChunkSeed.Rng(mapId, cx, cz, <see cref="TileRngTag"/>)</c>，
    /// 同 (Seed, MapId, Config, RuleSet) 必产生同位置同类型实体。</para>
    /// </summary>
    [Serializable]
    public class VoxelEntitySpawnRule
    {
        // ─── 标识 / 关联 ─────────────────────────────────────────────────
        public string RuleId;
        public string EntityConfigId;
        public string TileRngTag = "spawn";

        // ─── 过滤器（全部 AND；空字段视为不限） ───────────────────────────
        /// <summary>命中其一即通过；为空则不限。常用：<c>{ VoxelBlockTypes.Grass }</c> 限定草地之上。</summary>
        public byte[] TopBlockIds;

        /// <summary>命中其一即通过；为空则不限（一般 SideBlock 用于山体露土，spawn 多按 TopBlock 过滤）。</summary>
        public byte[] SideBlockIds;

        /// <summary>column 高度过滤（地表 y），如海平面以上、雪线以下。</summary>
        public IntRange HeightRange;

        // ─── 密度 / 形态 ─────────────────────────────────────────────────
        public float DensityPerTile = 0.05f;
        public int ClusterMin = 1;
        public int ClusterMax = 1;
        public int ClusterRadius = 2;
        public int MinSpacing = 0;

        // ─── 调度 ────────────────────────────────────────────────────────
        public int Priority = 200;
        public int MaxPerChunk = 16;

        public VoxelEntitySpawnRule() { }
        public VoxelEntitySpawnRule(string ruleId, string entityConfigId)
        {
            RuleId = ruleId; EntityConfigId = entityConfigId;
        }

        // ─── 链式 setter ─────────────────────────────────────────────────
        public VoxelEntitySpawnRule WithRngTag(string tag) { TileRngTag = tag; return this; }
        public VoxelEntitySpawnRule WithTopBlocks(params byte[] ids) { TopBlockIds = ids; return this; }
        public VoxelEntitySpawnRule WithSideBlocks(params byte[] ids) { SideBlockIds = ids; return this; }
        public VoxelEntitySpawnRule WithHeightRange(int min, int max) { HeightRange = IntRange.Of(min, max); return this; }
        public VoxelEntitySpawnRule WithDensity(float density) { DensityPerTile = density; return this; }
        public VoxelEntitySpawnRule WithCluster(int min, int max, int radius)
        { ClusterMin = min; ClusterMax = max; ClusterRadius = radius; return this; }
        public VoxelEntitySpawnRule WithMinSpacing(int spacing) { MinSpacing = spacing; return this; }
        public VoxelEntitySpawnRule WithPriority(int priority) { Priority = priority; return this; }
        public VoxelEntitySpawnRule WithMaxPerChunk(int max) { MaxPerChunk = max; return this; }
    }
}
