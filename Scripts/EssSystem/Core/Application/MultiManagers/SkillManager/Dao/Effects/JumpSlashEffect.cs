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
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;

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
            var rb2D = root.GetComponent<Rigidbody2D>();
            if (rb == null && rb2D == null) return;

            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f ? Mathf.Sign(ctx.Direction.x) : (root.localScale.x >= 0f ? 1f : -1f);
            if (rb != null)
                rb.linearVelocity = new Vector3(dirX * JumpForward, JumpUp, 0f);
            if (rb2D != null)
                rb2D.linearVelocity = new Vector2(dirX * JumpForward, JumpUp);

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
                OnExpire = _ => Slam(casterId, rb, rb2D, root, slam, radius, damage, dmgType),
            });
        }

        private static void Slam(string casterId, Rigidbody rb, Rigidbody2D rb2D, Transform root,
            float slamVelocity, float radius, float damage, string damageType)
        {
            if (root == null) return;
            if (rb != null)
                rb.linearVelocity = new Vector3(0f, slamVelocity, 0f);
            if (rb2D != null)
                rb2D.linearVelocity = new Vector2(0f, slamVelocity);
            if (damage <= 0f) return;
            var center = root.position;
            var seen = new System.Collections.Generic.HashSet<string>();

            var count2D = Physics2D.OverlapCircle(center, radius, _contactFilter, _buffer2D);
            for (var i = 0; i < count2D; i++)
                DamageTarget(SkillEntityProxy.IdFrom(_buffer2D[i]), seen, casterId, damage, damageType, center);

            var count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
                DamageTarget(SkillEntityProxy.IdFrom(_buffer[i]), seen, casterId, damage, damageType, center);
        }

        private static void DamageTarget(string targetId, System.Collections.Generic.HashSet<string> seen,
            string casterId, float damage, string damageType, Vector3 center)
        {
            if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId) || targetId == casterId) return;
            if (SkillEntityProxy.IsDead(targetId)) return;
            SkillEntityProxy.Damage(targetId, damage, casterId, damageType, center);
        }
    }
}
