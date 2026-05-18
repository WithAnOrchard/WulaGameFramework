using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 持续治疗效果（HoT，Heal over Time）—— 每 <see cref="TickInterval"/> 秒回复 <see cref="HealPerTick"/> 生命。
    /// 典型用法：再生术 / 治疗结界。<see cref="ApplyToSelf"/>=false 时回血给 ctx.Target。
    /// </summary>
    public class HotEffect : ISkillEffect
    {
        public string BuffId = "hot";

        public float Duration = 5f;
        public float TickInterval = 1f;

        public float HealPerTick = 5f;
        public float HealPerLevelPerTick;

        public bool ApplyToSelf;

        public HotEffect() { }

        public HotEffect(string buffId, float duration, float tickInterval, float healPerTick,
            float healPerLevelPerTick = 0f, bool applyToSelf = false)
        {
            BuffId = buffId;
            Duration = duration;
            TickInterval = tickInterval;
            HealPerTick = healPerTick;
            HealPerLevelPerTick = healPerLevelPerTick;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var target = ApplyToSelf ? ctx.Caster : ctx.Target;
            if (target == null || !SkillService.HasInstance) return;
            var dmg = target.Get<IDamageable>();
            if (dmg == null || dmg.IsDead) return;

            var perTick = HealPerTick + HealPerLevelPerTick * (ctx.Level - 1);
            var caster = ctx.Caster;

            SkillService.Instance.ApplyBuff(target, new BuffInstance
            {
                BuffId = BuffId,
                Source = caster,
                Target = target,
                Duration = Duration,
                TickInterval = UnityEngine.Mathf.Max(0.05f, TickInterval),
                OnTick = (b, _) =>
                {
                    if (b?.Target == null) return;
                    var d = b.Target.Get<IDamageable>();
                    d?.Heal(perTick, b.Source);
                },
            });
        }
    }
}
