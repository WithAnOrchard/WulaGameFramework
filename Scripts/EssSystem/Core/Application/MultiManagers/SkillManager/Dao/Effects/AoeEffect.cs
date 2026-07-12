using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class AoeEffect : ISkillEffect
    {
        public float Radius;
        public float RadiusPerLevel;
        public List<ISkillEffect> SubEffects = new();
        public bool IncludeSelf;

        private static readonly Collider[] _buffer = new Collider[64];
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;

        public AoeEffect(float radius, float radiusPerLevel = 0f, bool includeSelf = false)
        {
            Radius = radius;
            RadiusPerLevel = radiusPerLevel;
            IncludeSelf = includeSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null) return;
            var r = Radius + RadiusPerLevel * (ctx.Level - 1);
            var center = ctx.Position != Vector3.zero ? ctx.Position : SkillEntityProxy.Position(ctx.CasterId);
            var seen = new HashSet<string>();

            var count2D = Physics2D.OverlapCircle(center, r, _contactFilter, _buffer2D);
            for (var i = 0; i < count2D; i++)
                ApplyToTarget(ctx, SkillEntityProxy.IdFrom(_buffer2D[i]), seen);

            var count = Physics.OverlapSphereNonAlloc(center, r, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                ApplyToTarget(ctx, SkillEntityProxy.IdFrom(_buffer[i]), seen);
            }
        }

        private void ApplyToTarget(SkillEffectContext ctx, string targetId, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId)) return;
            if (!IncludeSelf && targetId == ctx.CasterId) return;

            var subCtx = new SkillEffectContext
            {
                CasterId = ctx.CasterId,
                TargetId = targetId,
                Definition = ctx.Definition,
                Instance = ctx.Instance,
                Direction = ctx.Direction,
                Position = ctx.Position,
            };
            foreach (var effect in SubEffects)
                effect.Apply(subCtx);
        }
    }
}
