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
            var count = Physics.OverlapSphereNonAlloc(origin, Range, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var targetId = SkillEntityProxy.IdFrom(_buffer[i]);
                if (string.IsNullOrEmpty(targetId)) continue;
                if (!IncludeSelf && targetId == ctx.CasterId) continue;

                var toTarget = SkillEntityProxy.Position(targetId) - origin;
                if (toTarget.sqrMagnitude < 0.0001f) continue;
                if (Vector3.Dot(dir, toTarget.normalized) < cosThreshold) continue;

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
}
