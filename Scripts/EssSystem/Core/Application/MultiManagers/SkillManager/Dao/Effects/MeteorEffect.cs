using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 陨石术 —— 在 <see cref="SkillEffectContext.Position"/>（PointTarget 模式）经过 <see cref="ImpactDelay"/> 秒后
    /// 落下一颗陨石，造成 <see cref="Radius"/> 半径范围伤害。
    /// <list type="bullet">
    /// <item>调度走 <see cref="BuffInstance"/>.OnExpire，无需 Coroutine / 新 MonoBehaviour。</item>
    /// <item>Buff 挂在 caster 身上仅做计时器用，Target 也写 caster（避免 BuffSet 空）。</item>
    /// <item>命中过滤：默认排除施法者；可设置 <see cref="IncludeSelf"/>=true 让法师"自爆"。</item>
    /// </list>
    /// </summary>
    public class MeteorEffect : ISkillEffect
    {
        public const string BUFF_ID = "meteor_pending";

        public float ImpactDelay = 1.2f;
        public float Radius = 3f;
        public float Damage = 30f;
        public float DamagePerLevel;
        public bool IncludeSelf;
        public string DamageType = "fire";

        private static readonly List<Collider2D> _buffer = new List<Collider2D>();
        private static readonly ContactFilter2D _noFilter = new ContactFilter2D { useTriggers = true };

        public MeteorEffect() { }

        public MeteorEffect(float impactDelay, float radius, float damage, float damagePerLevel = 0f,
            string damageType = "fire", bool includeSelf = false)
        {
            ImpactDelay = impactDelay;
            Radius = radius;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
            IncludeSelf = includeSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (!SkillService.HasInstance || ctx?.Caster == null) return;

            // 落点：PointTarget 用 ctx.Position；否则取施法者朝向前方 Range 距离
            var center = ctx.Position != Vector3.zero
                ? (Vector2)ctx.Position
                : (Vector2)ctx.Caster.WorldPosition;

            var caster = ctx.Caster;
            var radius = Radius;
            var damage = Damage + DamagePerLevel * (ctx.Level - 1);
            var includeSelf = IncludeSelf;
            var dmgType = DamageType;

            SkillService.Instance.ApplyBuff(caster, new BuffInstance
            {
                BuffId = BUFF_ID,
                Source = caster,
                Target = caster,
                Duration = Mathf.Max(0.05f, ImpactDelay),
                OnExpire = _ => Impact(caster, center, radius, damage, includeSelf, dmgType),
            });
        }

        private static void Impact(Entity caster, Vector2 center, float radius, float damage,
            bool includeSelf, string damageType)
        {
            if (!EntityService.HasInstance || damage <= 0f) return;
            var count = Physics2D.OverlapCircle(center, radius, _noFilter, _buffer);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle?.Entity == null) continue;
                if (!includeSelf && handle.Entity == caster) continue;
                var d = handle.Entity.Get<IDamageable>();
                if (d == null || d.IsDead) continue;
                EntityService.Instance.TryDamage(handle.Entity, damage,
                    source: caster, damageType: damageType, damageSourcePosition: center);
            }
        }
    }
}
