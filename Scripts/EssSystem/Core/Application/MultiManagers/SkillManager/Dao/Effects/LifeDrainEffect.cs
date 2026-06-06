using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class LifeDrainEffect : ISkillEffect
    {
        public float Damage = 10f;
        public float DamagePerLevel;
        public float HealRatio = 0.5f;
        public string DamageType = "life_drain";

        public LifeDrainEffect() { }

        public LifeDrainEffect(float damage, float healRatio = 0.5f, float damagePerLevel = 0f,
            string damageType = "life_drain")
        {
            Damage = damage;
            HealRatio = healRatio;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.TargetId) || string.IsNullOrEmpty(ctx.CasterId)) return;
            var dealt = SkillEntityProxy.Damage(ctx.TargetId, Damage + DamagePerLevel * (ctx.Level - 1),
                ctx.CasterId, DamageType);
            if (dealt > 0f) SkillEntityProxy.Heal(ctx.CasterId, dealt * UnityEngine.Mathf.Max(0f, HealRatio), ctx.CasterId);
        }
    }
}
