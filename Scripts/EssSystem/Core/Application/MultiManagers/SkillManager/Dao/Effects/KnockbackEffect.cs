using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class KnockbackEffect : ISkillEffect
    {
        public float Force = 6f;
        public float UpwardForce = 1.5f;

        public KnockbackEffect() { }
        public KnockbackEffect(float force, float upKick = 1.5f)
        {
            Force = force;
            UpwardForce = upKick;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetRoot = SkillEntityProxy.Root(ctx?.TargetId);
            if (targetRoot == null) return;
            var rb = targetRoot.GetComponent<Rigidbody>();
            var rb2D = targetRoot.GetComponent<Rigidbody2D>();
            if (rb == null && rb2D == null) return;
            var casterPos = string.IsNullOrEmpty(ctx.CasterId)
                ? ctx.Position
                : SkillEntityProxy.Position(ctx.CasterId, ctx.Position);
            var dx = targetRoot.position.x - casterPos.x;
            var dirX = Mathf.Abs(dx) < 0.001f ? 1f : Mathf.Sign(dx);
            if (rb != null)
                rb.linearVelocity = new Vector3(dirX * Force, UpwardForce, 0f);
            if (rb2D != null)
                rb2D.linearVelocity = new Vector2(dirX * Force, UpwardForce);
        }
    }
}
