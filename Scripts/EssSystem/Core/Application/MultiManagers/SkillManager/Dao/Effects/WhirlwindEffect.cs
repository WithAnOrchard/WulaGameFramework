using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
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

        private static readonly Collider[] _buffer = new Collider[64];
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;

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
            if (ctx == null || string.IsNullOrEmpty(ctx.CasterId) || !SkillService.HasInstance) return;
            var casterId = ctx.CasterId;
            var damage = DamagePerTick + DamagePerLevelPerTick * (ctx.Level - 1);
            var radius = Radius;
            var type = DamageType;
            var includeSelf = IncludeSelf;

            SkillService.Instance.ApplyBuff(casterId, new BuffInstance
            {
                BuffId = BUFF_ID,
                SourceId = casterId,
                Duration = Duration,
                TickInterval = Mathf.Max(0.05f, TickInterval),
                OnTick = (b, _) =>
                {
                    var center = SkillEntityProxy.Position(b.SourceId);
                    var seen = new System.Collections.Generic.HashSet<string>();

                    var count2D = Physics2D.OverlapCircle(center, radius, _contactFilter, _buffer2D);
                    for (var i = 0; i < count2D; i++)
                        DamageTarget(SkillEntityProxy.IdFrom(_buffer2D[i]), seen, includeSelf, b.SourceId, damage, type, center);

                    var count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, ~0, QueryTriggerInteraction.Collide);
                    for (var i = 0; i < count; i++)
                        DamageTarget(SkillEntityProxy.IdFrom(_buffer[i]), seen, includeSelf, b.SourceId, damage, type, center);
                },
            });
        }

        private static void DamageTarget(string targetId, System.Collections.Generic.HashSet<string> seen,
            bool includeSelf, string sourceId, float damage, string type, Vector3 center)
        {
            if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId)) return;
            if (!includeSelf && targetId == sourceId) return;
            if (SkillEntityProxy.IsDead(targetId)) return;
            SkillEntityProxy.Damage(targetId, damage, sourceId, type, center);
        }
    }
}
