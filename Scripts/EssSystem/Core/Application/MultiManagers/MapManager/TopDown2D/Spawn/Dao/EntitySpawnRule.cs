using System;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn.Dao
{
    /// <summary>
    /// 单条实体生成规则。归 <see cref="EntitySpawnRuleSet"/> 管理，由 <c>EntitySpawnDecorator</c> 在区块生成期评估。
    /// <para>**确定性**：所有随机数走 <c>ChunkSeed.Rng(mapId, cx, cy, <see cref="TileRngTag"/>)</c>，
    /// 同 (Seed, MapId, MapConfig, RuleSet) 必产生同位置同类型实体。</para>
    /// <para>**复现**：spawn 实体的 instanceId 由 (mapId, cx, cy, ruleId, lx, ly[, clusterIdx]) 决定性派生，
    /// 因此玩家砍掉的实体重新进入区块时 instanceId 一致，从而 IsSpawnDestroyed 命中跳过。</para>
    /// </summary>
    [Serializable]
    public class EntitySpawnRule
    {
        // ─── 标识 / 关联 ─────────────────────────────────────────────────
        /// <summary>规则唯一 ID（用于 instanceId 派生）。**禁止包含 <c>:</c> / <c>#</c> / <c>/</c>**。</summary>
        public string RuleId;

        /// <summary>命中时创建的 EntityConfig（必须已注册到 <c>EntityService</c>）。</summary>
        public string EntityConfigId;

        /// <summary>RNG 派生标签。同一区块多个规则用不同 tag 互不相关。默认 <c>"spawn"</c>。</summary>
        public string TileRngTag = "spawn";

        // ─── 过滤器（全部 AND；空字段视为不限） ───────────────────────────
        /// <summary>命中其一即算通过；为空则不限群系（仅靠其它过滤器筛）。</summary>
        public string[] BiomeIds;

        /// <summary>命中其一即算通过；通常至少限定 <c>"land"</c> 或具体 TypeId 避免水里出树。</summary>
        public string[] TileTypeIds;

        public FloatRange ElevationRange;
        public FloatRange TemperatureRange;
        public FloatRange MoistureRange;

        // ─── 密度 / 形态 ─────────────────────────────────────────────────
        /// <summary>每格独立掷骰命中概率 [0,1]。</summary>
        public float DensityPerTile = 0.05f;

        /// <summary>命中后额外生成的邻居最小数（含主体）。1 = 单点；2 起即 cluster。</summary>
        public int ClusterMin = 1;

        /// <summary>命中后额外生成的邻居最大数（含主体）。</summary>
        public int ClusterMax = 1;

        /// <summary>cluster 邻居采样的曼哈顿半径（&gt;0 才生效）。</summary>
        public int ClusterRadius = 2;

        /// <summary>同 RuleId 已 spawn 实体的最小曼哈顿间距（区块内）。0 = 不限。</summary>
        public int MinSpacing = 0;

        // ─── 调度 ────────────────────────────────────────────────────────
        /// <summary>装饰器内部多规则评估顺序（越小越先）。同 Priority 按 RuleId 字母序保稳定。</summary>
        public int Priority = 200;

        /// <summary>本规则单区块上限。超出后停止评估，防性能尖刺。</summary>
        public int MaxPerChunk = 16;

        public EntitySpawnRule() { }
        public EntitySpawnRule(string ruleId, string entityConfigId)
        {
            RuleId = ruleId;
            EntityConfigId = entityConfigId;
        }

        // ─── 链式 setter（业务侧手写规则用） ─────────────────────────────
        public EntitySpawnRule WithRngTag(string tag) { TileRngTag = tag; return this; }
        public EntitySpawnRule WithBiomes(params string[] ids) { BiomeIds = ids; return this; }
        public EntitySpawnRule WithTileTypes(params string[] ids) { TileTypeIds = ids; return this; }
        public EntitySpawnRule WithElevationRange(float min, float max) { ElevationRange = FloatRange.Of(min, max); return this; }
        public EntitySpawnRule WithTemperatureRange(float min, float max) { TemperatureRange = FloatRange.Of(min, max); return this; }
        public EntitySpawnRule WithMoistureRange(float min, float max) { MoistureRange = FloatRange.Of(min, max); return this; }
        public EntitySpawnRule WithDensity(float density) { DensityPerTile = density; return this; }
        public EntitySpawnRule WithCluster(int min, int max, int radius)
        { ClusterMin = min; ClusterMax = max; ClusterRadius = radius; return this; }
        public EntitySpawnRule WithMinSpacing(int spacing) { MinSpacing = spacing; return this; }
        public EntitySpawnRule WithPriority(int priority) { Priority = priority; return this; }
        public EntitySpawnRule WithMaxPerChunk(int max) { MaxPerChunk = max; return this; }
    }
}
