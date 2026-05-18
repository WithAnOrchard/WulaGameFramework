using System;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 反伤护盾 —— Buff 期间，每当目标受到伤害（<see cref="IDamageable.Damaged"/> 触发）即把
    /// "实际伤害 × <see cref="ReflectRatio"/>" 返还给攻击者。
    /// <list type="bullet">
    /// <item>原伤害不抵消（不是减伤）；只在事件链 **之后** 追加一次反向 TryDamage。</item>
    /// <item>过滤：source==null / source==self 时跳过，防止 DoT 自伤或环境伤害触发死循环。</item>
    /// <item>OnExpire 精准解绑事件，不会泄漏。</item>
    /// </list>
    /// 典型用法：荆棘护甲、镜面披风、复仇祝福。
    /// </summary>
    public class DamageReflectEffect : ISkillEffect
    {
        public string BuffId = "damage_reflect";
        public float Duration = 5f;
        public float ReflectRatio = 0.5f;
        public bool ApplyToSelf = true;
        public string ReflectDamageType = "reflect";

        public DamageReflectEffect() { }

        public DamageReflectEffect(float reflectRatio, float duration, bool applyToSelf = true,
            string buffId = "damage_reflect", string reflectDamageType = "reflect")
        {
            ReflectRatio = reflectRatio;
            Duration = duration;
            ApplyToSelf = applyToSelf;
            BuffId = buffId;
            ReflectDamageType = reflectDamageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var target = ApplyToSelf ? ctx.Caster : ctx.Target;
            if (target == null || !SkillService.HasInstance) return;
            var dmg = target.Get<IDamageable>();
            if (dmg == null) return;

            var ratio = ReflectRatio;
            var type = ReflectDamageType;

            Action<Entity, Entity, float, string> handler = (self, src, dealt, _) =>
            {
                if (src == null || src == self || dealt <= 0f) return;
                if (!EntityService.HasInstance) return;
                EntityService.Instance.TryDamage(src, dealt * ratio,
                    source: self, damageType: type);
            };
            dmg.Damaged += handler;

            SkillService.Instance.ApplyBuff(target, new BuffInstance
            {
                BuffId = BuffId,
                Source = ctx.Caster,
                Target = target,
                Duration = Duration,
                OnExpire = _ => dmg.Damaged -= handler,
            });
        }
    }
}
