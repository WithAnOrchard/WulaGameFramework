using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class DamageEffect : ISkillEffect
    {
        public float BaseDamage;
        public float DamagePerLevel;
        public string DamageType;

        public DamageEffect(float baseDamage, float damagePerLevel = 0f, string damageType = "skill")
        {
            BaseDamage = baseDamage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.TargetId)) return;
            if (SkillEntityProxy.IsDead(ctx.TargetId)) return;
            var totalDamage = BaseDamage + DamagePerLevel * (ctx.Level - 1);
            SkillEntityProxy.Damage(ctx.TargetId, totalDamage, ctx.CasterId, DamageType);
        }
    }
}
