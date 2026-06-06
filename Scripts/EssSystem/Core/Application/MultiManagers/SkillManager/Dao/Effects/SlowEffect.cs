using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class SlowEffect : ISkillEffect
    {
        public string BuffId = "slow";
        public float Multiplier = 0.5f;
        public float Duration = 3f;
        public bool ApplyToSelf;

        public SlowEffect() { }

        public SlowEffect(string buffId, float multiplier, float duration, bool applyToSelf = false)
        {
            BuffId = buffId;
            Multiplier = multiplier;
            Duration = duration;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            if (!SkillEntityProxy.TryGetSpeedMultiplier(targetId, out var original)) return;
            SkillEntityProxy.SetSpeedMultiplier(targetId, original * Mathf.Max(0f, Multiplier));
            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Duration,
                OnExpire = _ => SkillEntityProxy.SetSpeedMultiplier(targetId, original),
            });
        }
    }
}
