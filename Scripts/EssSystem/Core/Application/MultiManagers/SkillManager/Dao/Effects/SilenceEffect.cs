using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class SilenceEffect : ISkillEffect
    {
        public string BuffId = "silence";
        public float Duration = 3f;

        public SilenceEffect() { }
        public SilenceEffect(string buffId, float duration) { BuffId = buffId; Duration = duration; }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.TargetId) || !SkillService.HasInstance) return;
            if (!SkillEntityProxy.PushControl(ctx.TargetId, "Silence")) return;
            SkillService.Instance.ApplyBuff(ctx.TargetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Duration,
                OnExpire = _ => SkillEntityProxy.PopControl(ctx.TargetId, "Silence"),
            });
        }
    }
}
