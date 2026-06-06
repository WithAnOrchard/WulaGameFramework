using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class DotEffect : ISkillEffect
    {
        public string BuffId = "dot";
        public float Duration = 3f;
        public float TickInterval = 1f;
        public float DamagePerTick = 5f;
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
            if (ctx == null || string.IsNullOrEmpty(ctx.TargetId) || !SkillService.HasInstance) return;
            if (SkillEntityProxy.IsDead(ctx.TargetId)) return;
            var perTick = DamagePerTick + DamagePerLevelPerTick * (ctx.Level - 1);
            var sourceId = ctx.CasterId;
            var targetId = ctx.TargetId;
            var type = DamageType;
            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = sourceId,
                Duration = Duration,
                TickInterval = UnityEngine.Mathf.Max(0.05f, TickInterval),
                OnTick = (b, _) => SkillEntityProxy.Damage(b.TargetId, perTick, b.SourceId, type),
            });
        }
    }
}
