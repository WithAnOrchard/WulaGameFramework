using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 旋风斩 —— 在施法者周围 <see cref="Radius"/> 半径内每 <see cref="TickInterval"/> 秒造成一次范围伤害，
    /// 持续 <see cref="Duration"/> 秒。底层用 <see cref="BuffInstance"/> 调度，OnTick 内做 Physics2D.OverlapCircleNonAlloc。
    /// </summary>
    public class WhirlwindEffect : ISkillEffect
    {
        public const string BUFF_ID = "whirlwind";

        public float Duration = 3f;
        public float TickInterval = 0.4f;

        public float Radius = 2.5f;
        public float DamagePerTick = 5f;
        public float DamagePerLevelPerTick;

        public string DamageType = "whirlwind";
        public bool IncludeSelf;

        private static readonly List<Collider2D> _buffer = new List<Collider2D>();
        private static readonly ContactFilter2D _noFilter = new ContactFilter2D { useTriggers = true };

        public WhirlwindEffect() { }

        public WhirlwindEffect(float duration, float tickInterval, float radius, float damagePerTick,
            float damagePerLevelPerTick = 0f, string damageType = "whirlwind")
        {
            Duration = duration;
            TickInterval = tickInterval;
            Radius = radius;
            DamagePerTick = damagePerTick;
            DamagePerLevelPerTick = damagePerLevelPerTick;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Caster?.CharacterRoot == null || !SkillService.HasInstance) return;
            var caster = ctx.Caster;
            var damage = DamagePerTick + DamagePerLevelPerTick * (ctx.Level - 1);
            var radius = Radius;
            var dmgType = DamageType;
            var includeSelf = IncludeSelf;

            SkillService.Instance.ApplyBuff(caster, new BuffInstance
            {
                BuffId = BUFF_ID,
                Source = caster,
                Target = caster,
                Duration = Duration,
                TickInterval = Mathf.Max(0.05f, TickInterval),
                OnTick = (b, _) =>
                {
                    var root = b.Source?.CharacterRoot;
                    if (root == null || !EntityService.HasInstance) return;
                    var center = (Vector2)root.position;
                    var count = Physics2D.OverlapCircle(center, radius, _noFilter, _buffer);
                    for (var i = 0; i < count; i++)
                    {
                        var col = _buffer[i];
                        if (col == null) continue;
                        var handle = col.GetComponentInParent<EntityHandle>();
                        if (handle == null || handle.Entity == null) continue;
                        if (!includeSelf && handle.Entity == b.Source) continue;
                        var d = handle.Entity.Get<IDamageable>();
                        if (d == null || d.IsDead) continue;
                        EntityService.Instance.TryDamage(handle.Entity, damage,
                            source: b.Source, damageType: dmgType, damageSourcePosition: center);
                    }
                },
            });
        }
    }
}
