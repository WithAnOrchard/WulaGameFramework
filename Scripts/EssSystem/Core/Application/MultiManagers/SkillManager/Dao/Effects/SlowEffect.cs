using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 减速 / 加速效果 —— 通过 Buff 在 <see cref="Duration"/> 秒内将目标 <c>SpeedMultiplier</c>
    /// 乘以 <see cref="Multiplier"/>。<see cref="Multiplier"/> &lt; 1 = 减速（霜冻），&gt; 1 = 加速（疾跑）。
    /// <list type="bullet">
    /// <item>支持 <see cref="MovableComponent"/> 和 <see cref="Rigidbody2DMoverComponent"/> 两种 IMovable 实现。</item>
    /// <item>叠加：OnExpire 还原"进入 buff 前"的 SpeedMultiplier，多重 Slow 串联 LIFO 还原。</item>
    /// <item><see cref="ApplyToSelf"/>=true 时作用于施法者；否则作用于 ctx.Target。</item>
    /// </list>
    /// </summary>
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
            var target = ApplyToSelf ? ctx.Caster : ctx.Target;
            if (target == null || !SkillService.HasInstance) return;
            var mover = target.Get<IMovable>();
            if (mover == null) return;

            float origMul;
            System.Action<float> writeMul;
            switch (mover)
            {
                case MovableComponent m1:
                    origMul = m1.SpeedMultiplier;
                    writeMul = v => m1.SpeedMultiplier = v;
                    break;
                case Rigidbody2DMoverComponent m2:
                    origMul = m2.SpeedMultiplier;
                    writeMul = v => m2.SpeedMultiplier = v;
                    break;
                default:
                    return; // 第三方 IMovable 不支持 SpeedMultiplier，静默跳过
            }

            writeMul(origMul * Mathf.Max(0f, Multiplier));
            SkillService.Instance.ApplyBuff(target, new BuffInstance
            {
                BuffId = BuffId,
                Source = ctx.Caster,
                Target = target,
                Duration = Duration,
                OnExpire = _ => writeMul(origMul),
            });
        }
    }
}
