using UnityEngine;
using Demo.Tribe.Enemy;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 生物 Feature —— 用 <see cref="TribeCreature"/> + 预设配置 spawn 一只敌人 / 动物 / NPC 候选。
    /// </summary>
    public class CreatureFeature : TribeFeatureSpec
    {
        /// <summary>显示名（GameObject 名）。</summary>
        public string DisplayName;

        /// <summary>生物配置（来自 <see cref="TribeCreaturePresets"/> 或自定义实例）。</summary>
        public TribeCreatureConfig CreatureConfig;

        /// <summary>SortingOrder 偏移（默认 +2，敌人通常压在采集物前）。</summary>
        public int SortingOffset = 2;

        public CreatureFeature(float worldX, string displayName, TribeCreatureConfig config,
            float yOffset = 0.75f, int sortingOffset = 2)
        {
            WorldX = worldX; YOffset = yOffset;
            DisplayName = displayName;
            CreatureConfig = config;
            SortingOffset = sortingOffset;
        }

        public override void Build(TribeBiomeContext ctx)
        {
            if (CreatureConfig == null)
            {
                Debug.LogWarning($"[CreatureFeature] {DisplayName} 缺 TribeCreatureConfig，跳过");
                return;
            }
            var go = new GameObject(DisplayName);
            go.transform.position = ComputeWorldPosition(ctx);
            if (ctx.EnemiesRoot != null) go.transform.SetParent(ctx.EnemiesRoot, true);

            var creature = go.AddComponent<TribeCreature>();
            creature.Configure(CreatureConfig);
            creature.SortingOrder = ctx.BaseSortingOrder + SortingOffset;
        }
    }
}
