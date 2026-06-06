using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class TeleportEffect : ISkillEffect
    {
        public bool UseAbsolutePosition;
        public float Distance = 5f;
        public float DistancePerLevel;

        public TeleportEffect() { }
        public TeleportEffect(float distance, float distancePerLevel = 0f, bool useAbsolutePosition = false)
        {
            Distance = distance;
            DistancePerLevel = distancePerLevel;
            UseAbsolutePosition = useAbsolutePosition;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.CasterId)) return;
            var root = SkillEntityProxy.Root(ctx.CasterId);
            var current = SkillEntityProxy.Position(ctx.CasterId);
            Vector3 targetPos;
            if (UseAbsolutePosition && ctx.Position != Vector3.zero)
            {
                targetPos = ctx.Position;
            }
            else
            {
                var dir = ctx.Direction;
                if (dir.sqrMagnitude < 0.001f)
                {
                    var face = root != null && root.localScale.x < 0f ? -1f : 1f;
                    dir = new Vector3(face, 0f, 0f);
                }
                else dir = dir.normalized;
                targetPos = current + dir * (Distance + DistancePerLevel * (ctx.Level - 1));
            }
            SkillEntityProxy.SetPosition(ctx.CasterId, targetPos);
        }
    }
}
