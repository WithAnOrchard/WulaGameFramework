using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class HotEffect : ISkillEffect
    {
        public string BuffId = "hot";
        public float Duration = 3f;
        public float TickInterval = 1f;
        public float HealPerTick = 5f;
        public float HealPerLevelPerTick;
        public bool ApplyToSelf = true;

        public HotEffect() { }

        public HotEffect(string buffId, float duration, float tickInterval, float healPerTick,
            float healPerLevelPerTick = 0f, bool applyToSelf = true)
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
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            if (SkillEntityProxy.IsDead(targetId)) return;
            var perTick = HealPerTick + HealPerLevelPerTick * (ctx.Level - 1);
            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Duration,
                TickInterval = UnityEngine.Mathf.Max(0.05f, TickInterval),
                OnTick = (b, _) => SkillEntityProxy.Heal(b.TargetId, perTick, b.SourceId),
            });
        }
    }
}
