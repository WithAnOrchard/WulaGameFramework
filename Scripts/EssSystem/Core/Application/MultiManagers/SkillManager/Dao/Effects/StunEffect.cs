using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 眩晕效果 —— 在目标 <see cref="IControllable"/> 上 Push 一层 Stun（计数 +1），
    /// 持续 <see cref="Duration"/> 秒，Buff 到期自动 Pop。
    /// <list type="bullet">
    /// <item>消费方：<see cref="MovableComponent"/>/<see cref="Rigidbody2DMoverComponent"/>.Move() 短路、
    /// <c>SkillService.CastSkill</c> 短路。</item>
    /// <item>叠加：多个 Stun Buff 共存时计数累加，单个 Cleanse 不会让其他 Stun 失效（LIFO 安全）。</item>
    /// <item>目标无 <see cref="IControllable"/> 时静默跳过（保留对老 Entity 的兼容）。</item>
    /// </list>
    /// </summary>
    public class StunEffect : ISkillEffect
    {
        public string BuffId = "stun";
        public float Duration = 1.5f;

        public StunEffect() { }
        public StunEffect(string buffId, float duration) { BuffId = buffId; Duration = duration; }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Target == null || !SkillService.HasInstance) return;
            var ctrl = ctx.Target.Get<IControllable>();
            if (ctrl == null) return;

            ctrl.PushStun();
            SkillService.Instance.ApplyBuff(ctx.Target, new BuffInstance
            {
                BuffId = BuffId,
                Source = ctx.Caster,
                Target = ctx.Target,
                Duration = Duration,
                OnExpire = _ => ctrl.PopStun(),
            });
        }
    }
}
