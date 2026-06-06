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
                    var count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, ~0, QueryTriggerInteraction.Collide);
                    for (var i = 0; i < count; i++)
                    {
                        var targetId = SkillEntityProxy.IdFrom(_buffer[i]);
                        if (string.IsNullOrEmpty(targetId)) continue;
                        if (!includeSelf && targetId == b.SourceId) continue;
                        if (SkillEntityProxy.IsDead(targetId)) continue;
                        SkillEntityProxy.Damage(targetId, damage, b.SourceId, type, center);
                    }
                },
            });
        }
    }
}
