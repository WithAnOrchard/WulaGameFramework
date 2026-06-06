using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class ShieldEffect : ISkillEffect
    {
        public const string BUFF_ID = "shield";
        public float Reduction = 0.5f;
        public float Duration = 5f;
        public bool ApplyToSelf = true;

        public ShieldEffect() { }
        public ShieldEffect(float reduction, float duration, bool applyToSelf = true)
        {
            Reduction = reduction;
            Duration = duration;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            if (!SkillEntityProxy.TryGetDamageReduction(targetId, out var original)) return;
            SkillEntityProxy.SetDamageReduction(targetId, Mathf.Max(original, Mathf.Clamp01(Reduction)));
            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BUFF_ID,
                SourceId = ctx.CasterId,
                Duration = Duration,
                OnExpire = _ => SkillEntityProxy.SetDamageReduction(targetId, original),
            });
        }
    }
}
