using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class JumpSlashEffect : ISkillEffect
    {
        public const string BUFF_JUMP_SLASH = "jump_slash_pending";
        public float JumpUp = 9f;
        public float JumpForward = 4f;
        public float AirTime = 0.45f;
        public float SlamDownVelocity = -16f;
        public float ImpactRadius = 2.5f;
        public float BaseDamage = 12f;
        public float DamagePerLevel;
        public string DamageType = "jump_slash";

        private static readonly Collider[] _buffer = new Collider[64];

        public JumpSlashEffect() { }
        public JumpSlashEffect(float jumpUp, float jumpForward, float airTime,
            float slamDownVelocity, float impactRadius, float baseDamage,
            float damagePerLevel = 0f, string damageType = "jump_slash")
        {
            JumpUp = jumpUp;
            JumpForward = jumpForward;
            AirTime = airTime;
            SlamDownVelocity = slamDownVelocity;
            ImpactRadius = impactRadius;
            BaseDamage = baseDamage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var rb = root.GetComponent<Rigidbody>();
            if (rb == null) return;

            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f ? Mathf.Sign(ctx.Direction.x) : (root.localScale.x >= 0f ? 1f : -1f);
            rb.linearVelocity = new Vector3(dirX * JumpForward, JumpUp, 0f);

            if (!SkillService.HasInstance) return;
            var casterId = ctx.CasterId;
            var damage = BaseDamage + DamagePerLevel * (ctx.Level - 1);
            var radius = ImpactRadius;
            var slam = SlamDownVelocity;
            var dmgType = DamageType;

            SkillService.Instance.ApplyBuff(casterId, new BuffInstance
            {
                BuffId = BUFF_JUMP_SLASH,
                SourceId = casterId,
                Duration = Mathf.Max(0.05f, AirTime),
                OnExpire = _ => Slam(casterId, rb, root, slam, radius, damage, dmgType),
            });
        }

        private static void Slam(string casterId, Rigidbody rb, Transform root,
            float slamVelocity, float radius, float damage, string damageType)
        {
            if (rb == null || root == null) return;
            rb.linearVelocity = new Vector3(0f, slamVelocity, 0f);
            if (damage <= 0f) return;
            var center = root.position;
            var count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var targetId = SkillEntityProxy.IdFrom(_buffer[i]);
                if (string.IsNullOrEmpty(targetId) || targetId == casterId) continue;
                if (SkillEntityProxy.IsDead(targetId)) continue;
                SkillEntityProxy.Damage(targetId, damage, casterId, damageType, center);
            }
        }
    }
}
