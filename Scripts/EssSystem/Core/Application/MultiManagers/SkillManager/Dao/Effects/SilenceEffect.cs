using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 沉默效果 —— 在目标 <see cref="IControllable"/> 上 Push 一层 Silence，目标无法主动施法。
    /// 移动不受影响（区别于 <see cref="StunEffect"/>）。
    /// </summary>
    public class SilenceEffect : ISkillEffect
    {
        public string BuffId = "silence";
        public float Duration = 3f;

        public SilenceEffect() { }
        public SilenceEffect(string buffId, float duration) { BuffId = buffId; Duration = duration; }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Target == null || !SkillService.HasInstance) return;
            var ctrl = ctx.Target.Get<IControllable>();
            if (ctrl == null) return;

            ctrl.PushSilence();
            SkillService.Instance.ApplyBuff(ctx.Target, new BuffInstance
            {
                BuffId = BuffId,
                Source = ctx.Caster,
                Target = ctx.Target,
                Duration = Duration,
                OnExpire = _ => ctrl.PopSilence(),
            });
        }
    }
}
