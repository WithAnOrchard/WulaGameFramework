using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 治疗效果 —— 恢复施法者或目标的 HP。
    /// </summary>
    public class HealEffect : ISkillEffect
    {
        public float BaseHeal;
        public float HealPerLevel;

        /// <summary>true = 治疗施法者自身，false = 治疗目标。</summary>
        public bool HealSelf;

        public HealEffect(float baseHeal, float healPerLevel = 0f, bool healSelf = false)
        {
            BaseHeal = baseHeal;
            HealPerLevel = healPerLevel;
            HealSelf = healSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var target = HealSelf ? ctx.Caster : ctx.Target;
            if (target == null) return;
            var dmg = target.Get<IDamageable>();
            if (dmg == null || dmg.IsDead) return;

            var amount = BaseHeal + HealPerLevel * (ctx.Level - 1);
            dmg.Heal(amount, ctx.Caster);
        }
    }
}
