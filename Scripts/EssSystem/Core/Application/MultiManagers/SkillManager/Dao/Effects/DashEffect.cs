using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class DashEffect : ISkillEffect
    {
        public const string BUFF_DASH_INVUL = "dash_invulnerable";
        public float Speed;
        public float SpeedPerLevel;
        public bool KeepVerticalVelocity = true;
        public float VerticalKick;
        public float InvulnerableDuration;

        public DashEffect(float speed, float speedPerLevel = 0f, float invulDuration = 0f,
            bool keepVerticalVelocity = true, float verticalKick = 0f)
        {
            Speed = speed;
            SpeedPerLevel = speedPerLevel;
            InvulnerableDuration = invulDuration;
            KeepVerticalVelocity = keepVerticalVelocity;
            VerticalKick = verticalKick;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) return;

            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f
                ? Mathf.Sign(ctx.Direction.x)
                : (root.localScale.x >= 0f ? 1f : -1f);
            var vy = KeepVerticalVelocity ? rb.linearVelocity.y : VerticalKick;
            rb.linearVelocity = new Vector3(dirX * (Speed + SpeedPerLevel * (ctx.Level - 1)), vy, 0f);

            var visual = root.Find("Visual");
            if (visual != null)
            {
                var s = visual.localScale;
                s.x = dirX > 0f ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
                visual.localScale = s;
            }

            if (InvulnerableDuration > 0f && SkillService.HasInstance &&
                SkillEntityProxy.TryGetDamageReduction(ctx.CasterId, out var original))
            {
                SkillEntityProxy.SetDamageReduction(ctx.CasterId, 1f);
                SkillService.Instance.ApplyBuff(ctx.CasterId, new BuffInstance
                {
                    BuffId = BUFF_DASH_INVUL,
                    SourceId = ctx.CasterId,
                    Duration = InvulnerableDuration,
                    OnExpire = _ => SkillEntityProxy.SetDamageReduction(ctx.CasterId, original),
                });
            }
        }
    }
}
