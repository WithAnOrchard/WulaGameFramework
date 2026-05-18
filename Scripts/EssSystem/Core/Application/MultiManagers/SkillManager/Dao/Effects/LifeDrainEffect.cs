using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 吸血效果 —— 对目标造成 <see cref="Damage"/> 伤害，并将"实际造成"伤害 × <see cref="HealRatio"/>
    /// 回复给施法者。注意是 EntityService.TryDamage 的实际返回值，自动遵守无敌 / 减伤。
    /// 适合"暗影抽取""生命窃取打击"等吸血法术 / 武器。
    /// </summary>
    public class LifeDrainEffect : ISkillEffect
    {
        public float Damage = 12f;
        public float DamagePerLevel;
        public float HealRatio = 0.5f;
        public string DamageType = "drain";

        public LifeDrainEffect() { }

        public LifeDrainEffect(float damage, float healRatio = 0.5f, float damagePerLevel = 0f,
            string damageType = "drain")
        {
            Damage = damage;
            HealRatio = Mathf.Max(0f, healRatio);
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Target == null || ctx.Caster == null || !EntityService.HasInstance) return;

            var damage = Damage + DamagePerLevel * (ctx.Level - 1);
            var dealt = EntityService.Instance.TryDamage(ctx.Target, damage,
                source: ctx.Caster, damageType: DamageType);
            if (dealt <= 0f) return;

            var heal = dealt * HealRatio;
            ctx.Caster.Get<IDamageable>()?.Heal(heal, ctx.Caster);
        }
    }
}
