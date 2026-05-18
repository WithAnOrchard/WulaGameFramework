using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 护盾效果 —— 给目标（或自身）添加一段时间的入伤减免。
    /// <list type="bullet">
    /// <item><see cref="Reduction"/>=1 → 完全无敌；0.5 → 半伤；0.3 → 减伤 30% 等。</item>
    /// <item>过期回调精准还原 <see cref="DamageableComponent.DamageReduction"/> 至原值。</item>
    /// <item>支持叠加：若同实体已有相同 <see cref="BUFF_ID"/> 仍可再加（取较高减伤值），但 OnExpire 会按 LIFO 还原。</item>
    /// </list>
    /// </summary>
    public class ShieldEffect : ISkillEffect
    {
        public const string BUFF_ID = "shield";

        public float Reduction = 0.5f;
        public float Duration = 5f;
        public bool ApplyToSelf = true;

        public ShieldEffect() { }

        public ShieldEffect(float reduction, float duration, bool applyToSelf = true)
        {
            Reduction = reduction;
            Duration = duration;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var target = ApplyToSelf ? ctx.Caster : ctx.Target;
            if (target == null || !SkillService.HasInstance) return;
            var dmg = target.Get<IDamageable>() as DamageableComponent;
            if (dmg == null) return;

            var origReduction = dmg.DamageReduction;
            // 取较高值：护盾期间不能因为旧弱减伤覆盖
            dmg.DamageReduction = UnityEngine.Mathf.Max(origReduction, UnityEngine.Mathf.Clamp01(Reduction));

            SkillService.Instance.ApplyBuff(target, new BuffInstance
            {
                BuffId = BUFF_ID,
                Source = ctx.Caster,
                Target = target,
                Duration = Duration,
                OnExpire = _ => { if (dmg != null) dmg.DamageReduction = origReduction; },
            });
        }
    }
}
