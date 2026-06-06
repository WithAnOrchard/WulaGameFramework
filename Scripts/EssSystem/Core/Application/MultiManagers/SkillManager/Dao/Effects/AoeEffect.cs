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
            var count = Physics.OverlapSphereNonAlloc(center, r, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var targetId = SkillEntityProxy.IdFrom(_buffer[i]);
                if (string.IsNullOrEmpty(targetId)) continue;
                if (!IncludeSelf && targetId == ctx.CasterId) continue;

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
}
