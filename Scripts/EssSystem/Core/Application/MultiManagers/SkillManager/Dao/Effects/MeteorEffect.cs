using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class MeteorEffect : ISkillEffect
    {
        public const string BUFF_ID = "meteor_pending";
        public float ImpactDelay = 1.2f;
        public float Radius = 3f;
        public float Damage = 30f;
        public float DamagePerLevel;
        public bool IncludeSelf;
        public string DamageType = "fire";

        private static readonly Collider[] _buffer = new Collider[64];
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;

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
            if (!SkillService.HasInstance || ctx == null || string.IsNullOrEmpty(ctx.CasterId)) return;
            var center = ctx.Position != Vector3.zero ? ctx.Position : SkillEntityProxy.Position(ctx.CasterId);
            var casterId = ctx.CasterId;
            var radius = Radius;
            var damage = Damage + DamagePerLevel * (ctx.Level - 1);
            var includeSelf = IncludeSelf;
            var dmgType = DamageType;

            SkillService.Instance.ApplyBuff(casterId, new BuffInstance
            {
                BuffId = BUFF_ID,
                SourceId = casterId,
                Duration = Mathf.Max(0.05f, ImpactDelay),
                OnExpire = _ => Impact(casterId, center, radius, damage, includeSelf, dmgType),
            });
        }

        private static void Impact(string casterId, Vector3 center, float radius, float damage,
            bool includeSelf, string damageType)
        {
            if (damage <= 0f) return;
            var seen = new System.Collections.Generic.HashSet<string>();

            var count2D = Physics2D.OverlapCircle(center, radius, _contactFilter, _buffer2D);
            for (var i = 0; i < count2D; i++)
                DamageTarget(SkillEntityProxy.IdFrom(_buffer2D[i]), seen, casterId, includeSelf, damage, damageType, center);

            var count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
                DamageTarget(SkillEntityProxy.IdFrom(_buffer[i]), seen, casterId, includeSelf, damage, damageType, center);
        }

        private static void DamageTarget(string targetId, System.Collections.Generic.HashSet<string> seen,
            string casterId, bool includeSelf, float damage, string damageType, Vector3 center)
        {
            if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId)) return;
            if (!includeSelf && targetId == casterId) return;
            if (SkillEntityProxy.IsDead(targetId)) return;
            SkillEntityProxy.Damage(targetId, damage, casterId, damageType, center);
        }
    }
}
