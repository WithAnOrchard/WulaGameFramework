using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 持续伤害效果（DoT，Damage over Time）—— 在目标身上挂 Buff，每 <see cref="TickInterval"/> 秒造成 <see cref="DamagePerTick"/> 伤害。
    /// 典型用法：燃烧 / 中毒 / 流血 / 腐蚀。
    /// </summary>
    public class DotEffect : ISkillEffect
    {
        public string BuffId = "dot";

        public float Duration = 5f;
        public float TickInterval = 1f;

        public float DamagePerTick = 3f;
        public float DamagePerLevelPerTick;

        public string DamageType = "dot";

        public DotEffect() { }

        public DotEffect(string buffId, float duration, float tickInterval, float damagePerTick,
            float damagePerLevelPerTick = 0f, string damageType = "dot")
        {
            BuffId = buffId;
            Duration = duration;
            TickInterval = tickInterval;
            DamagePerTick = damagePerTick;
            DamagePerLevelPerTick = damagePerLevelPerTick;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Target == null || !SkillService.HasInstance) return;
            var dmg = ctx.Target.Get<IDamageable>();
            if (dmg == null || dmg.IsDead) return;

            var perTick = DamagePerTick + DamagePerLevelPerTick * (ctx.Level - 1);
            var caster = ctx.Caster;
            var target = ctx.Target;
            var dmgType = DamageType;

            SkillService.Instance.ApplyBuff(target, new BuffInstance
            {
                BuffId = BuffId,
                Source = caster,
                Target = target,
                Duration = Duration,
                TickInterval = UnityEngine.Mathf.Max(0.05f, TickInterval),
                OnTick = (b, _) =>
                {
                    if (b?.Target == null || !EntityService.HasInstance) return;
                    EntityService.Instance.TryDamage(b.Target, perTick,
                        source: b.Source, damageType: dmgType);
                },
            });
        }
    }
}
