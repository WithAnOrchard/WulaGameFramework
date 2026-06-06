using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class StunEffect : ISkillEffect
    {
        public string BuffId = "stun";
        public float Duration = 1.5f;

        public StunEffect() { }
        public StunEffect(string buffId, float duration) { BuffId = buffId; Duration = duration; }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.TargetId) || !SkillService.HasInstance) return;
            if (!SkillEntityProxy.PushControl(ctx.TargetId, "Stun")) return;
            SkillService.Instance.ApplyBuff(ctx.TargetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Duration,
                OnExpire = _ => SkillEntityProxy.PopControl(ctx.TargetId, "Stun"),
            });
        }
    }
}
