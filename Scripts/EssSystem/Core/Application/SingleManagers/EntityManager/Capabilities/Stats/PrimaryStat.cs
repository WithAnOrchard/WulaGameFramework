namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 实体的 6 个基础属性（Primary Attributes）。
    /// <para>
    /// 玩家初值通常全 10；NPC / 怪物按角色定位差异化。修饰器（<see cref="StatModifier"/>）
    /// 可以叠加到任一基础属性上，最终值由 <see cref="AttributeSet"/> 在读取时按公式合成。
    /// </para>
    /// </summary>
    public enum PrimaryStat
    {
        /// <summary>力量 — 决定 CarryCapacity / 近战伤害 / 击退抗性。</summary>
        STR = 0,
        /// <summary>敏捷 — 决定 AttackSpeed / DodgeChance / CritChance。</summary>
        DEX = 1,
        /// <summary>体质 — 决定 MaxHp / HpRegen / 抗毒。</summary>
        CON = 2,
        /// <summary>智力 — 决定 MaxMp / 法术伤害 / 学习速度。</summary>
        INT = 3,
        /// <summary>感知 — 决定 ViewRange / MpRegen / 命中。</summary>
        WIS = 4,
        /// <summary>魅力 — 决定 NPC 价格折扣 / 对话选项。</summary>
        CHA = 5,
    }
}
