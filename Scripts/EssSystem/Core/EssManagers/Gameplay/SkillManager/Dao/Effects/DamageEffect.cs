using EssSystem.Core.EssManagers.Gameplay.EntityManager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;

namespace EssSystem.Core.EssManagers.Gameplay.SkillManager.Dao.Effects
{
    /// <summary>
    /// 伤害效果 —— 对目标造成固定或按等级缩放的伤害。
    /// 走 <see cref="EntityService.TryDamage"/> 统一伤害流水线。
    /// </summary>
    public class DamageEffect : ISkillEffect
    {
        /// <summary>基础伤害。</summary>
        public float BaseDamage;

        /// <summary>每级额外伤害。</summary>
        public float DamagePerLevel;

        /// <summary>伤害类型标签（如 "fire", "physical"）。</summary>
        public string DamageType;

        public DamageEffect(float baseDamage, float damagePerLevel = 0f, string damageType = "skill")
        {
            BaseDamage = baseDamage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx.Target == null || !EntityService.HasInstance) return;
            var dmg = ctx.Target.Get<IDamageable>();
            if (dmg == null || dmg.IsDead) return;

            var totalDamage = BaseDamage + DamagePerLevel * (ctx.Level - 1);
            EntityService.Instance.TryDamage(ctx.Target, totalDamage,
                source: ctx.Caster, damageType: DamageType);
        }
    }
}
