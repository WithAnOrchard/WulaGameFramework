using System;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class BuffEffect : ISkillEffect
    {
        public string BuffId;
        public float Duration;
        public Func<SkillEffectContext, float, BuffInstance> BuffFactory;
        public bool ApplyToSelf;

        public BuffEffect(string buffId, float duration, Func<SkillEffectContext, float, BuffInstance> factory,
            bool applyToSelf = false)
        {
            BuffId = buffId;
            Duration = duration;
            BuffFactory = factory;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || BuffFactory == null) return;
            var buff = BuffFactory(ctx, Duration);
            if (buff == null) return;
            buff.SourceId = ctx.CasterId;
            if (SkillService.HasInstance)
                SkillService.Instance.ApplyBuff(targetId, buff);
        }
    }
}
