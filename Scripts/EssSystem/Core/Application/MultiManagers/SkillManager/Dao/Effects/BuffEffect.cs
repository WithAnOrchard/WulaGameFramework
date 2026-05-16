using System;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// Buff 效果 —— 给目标或自身施加 Buff/Debuff。
    /// <para>通过 <see cref="BuffFactory"/> 创建 <see cref="BuffInstance"/>，
    /// 然后由 <see cref="SkillService"/> 挂载到目标 Entity。</para>
    /// </summary>
    public class BuffEffect : ISkillEffect
    {
        /// <summary>Buff 定义 ID。</summary>
        public string BuffId;

        /// <summary>Buff 持续时间（秒）。</summary>
        public float Duration;

        /// <summary>Buff 工厂 —— 创建 BuffInstance 的委托。</summary>
        public Func<SkillEffectContext, float, BuffInstance> BuffFactory;

        /// <summary>true = 施加到自身，false = 施加到目标。</summary>
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
            var target = ApplyToSelf ? ctx.Caster : ctx.Target;
            if (target == null || BuffFactory == null) return;

            var buff = BuffFactory(ctx, Duration);
            if (buff == null) return;

            // 挂载到 SkillService 的 Buff 管理
            if (SkillService.HasInstance)
                SkillService.Instance.ApplyBuff(target, buff);
        }
    }
}
