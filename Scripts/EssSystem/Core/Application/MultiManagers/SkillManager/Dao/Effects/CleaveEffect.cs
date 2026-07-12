using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class CleaveEffect : ISkillEffect
    {
        public float Range = 3f;
        public float HalfAngleDeg = 45f;
        public List<ISkillEffect> SubEffects = new();
        public bool IncludeSelf;

        private static readonly Collider[] _buffer = new Collider[64];
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;

        public CleaveEffect() { }
        public CleaveEffect(float range, float halfAngleDeg = 45f, bool includeSelf = false)
        {
            Range = range;
            HalfAngleDeg = halfAngleDeg;
            IncludeSelf = includeSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var origin = root.position;
            var dir = ctx.Direction.sqrMagnitude > 0.001f
                ? ctx.Direction.normalized
                : (root.localScale.x >= 0f ? Vector3.right : Vector3.left);
            var cosThreshold = Mathf.Cos(HalfAngleDeg * Mathf.Deg2Rad);
            var seen = new HashSet<string>();

            var count2D = Physics2D.OverlapCircle(origin, Range, _contactFilter, _buffer2D);
            for (var i = 0; i < count2D; i++)
                ApplyToTarget(ctx, origin, dir, cosThreshold, SkillEntityProxy.IdFrom(_buffer2D[i]), seen);

            var count = Physics.OverlapSphereNonAlloc(origin, Range, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                ApplyToTarget(ctx, origin, dir, cosThreshold, SkillEntityProxy.IdFrom(_buffer[i]), seen);
            }
        }

        private void ApplyToTarget(SkillEffectContext ctx, Vector3 origin, Vector3 dir,
            float cosThreshold, string targetId, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId)) return;
            if (!IncludeSelf && targetId == ctx.CasterId) return;

            var toTarget = SkillEntityProxy.Position(targetId) - origin;
            if (toTarget.sqrMagnitude < 0.0001f) return;
            if (Vector3.Dot(dir, toTarget.normalized) < cosThreshold) return;

            var subCtx = new SkillEffectContext
            {
                CasterId = ctx.CasterId,
                TargetId = targetId,
                Definition = ctx.Definition,
                Instance = ctx.Instance,
                Direction = ctx.Direction,
                Position = ctx.Position,
            };
            for (var s = 0; s < SubEffects.Count; s++)
                SubEffects[s].Apply(subCtx);
        }
    }
}
