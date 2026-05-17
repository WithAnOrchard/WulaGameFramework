using System;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 属性修饰器叠加方式。
    /// <para>
    /// 公式： <c>final = (base + Σ Flat) * (1 + Σ PercentAdd) * Π(1 + PercentMul)</c>
    /// </para>
    /// </summary>
    public enum StatModifierOp
    {
        /// <summary>整数加成（+5 STR）。先于百分比生效。</summary>
        Flat = 0,
        /// <summary>百分比加（+15% STR）。所有 PercentAdd 先求和再一次性乘到基础值。</summary>
        PercentAdd = 1,
        /// <summary>百分比乘（×1.1 ×1.05 …）。多个 PercentMul 顺次相乘。</summary>
        PercentMul = 2,
    }

    /// <summary>
    /// 单条属性修饰器 —— 装备 / Buff / 状态改变属性的最小记录。
    /// <para>
    /// <see cref="SourceId"/> 标识出处（"weapon:iron_sword" / "buff:strength_potion"），
    /// 卸下装备 / Buff 失效时按同一 SourceId 一次性移除。
    /// </para>
    /// </summary>
    [Serializable]
    public class StatModifier
    {
        /// <summary>来源标识（同一来源加同名修饰会被先移除再添加，避免重复叠加）。</summary>
        public string SourceId;

        /// <summary>是否作用在 Primary（true）还是 Derived（false）。</summary>
        public bool TargetIsPrimary;

        /// <summary>目标 Primary 索引（仅 <see cref="TargetIsPrimary"/>=true 有效）。</summary>
        public PrimaryStat TargetPrimary;

        /// <summary>目标 Derived 索引（仅 <see cref="TargetIsPrimary"/>=false 有效）。</summary>
        public DerivedStat TargetDerived;

        /// <summary>叠加方式。</summary>
        public StatModifierOp Op;

        /// <summary>修饰值（Flat=整数；PercentAdd / PercentMul=小数，0.15 表示 +15%）。</summary>
        public float Value;

        /// <summary>持续时间（秒）。<c>≤0</c> 表示永久（装备 / 永久 Buff）。</summary>
        public float Duration;

        public StatModifier() { }

        public static StatModifier ForPrimary(string sourceId, PrimaryStat target, StatModifierOp op, float value, float duration = 0f) =>
            new StatModifier
            {
                SourceId = sourceId,
                TargetIsPrimary = true,
                TargetPrimary = target,
                Op = op,
                Value = value,
                Duration = duration,
            };

        public static StatModifier ForDerived(string sourceId, DerivedStat target, StatModifierOp op, float value, float duration = 0f) =>
            new StatModifier
            {
                SourceId = sourceId,
                TargetIsPrimary = false,
                TargetDerived = target,
                Op = op,
                Value = value,
                Duration = duration,
            };
    }
}
