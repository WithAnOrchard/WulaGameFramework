using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class MultiShotEffect : ISkillEffect
    {
        public int ProjectileCount = 3;
        public float SpreadAngleDeg = 30f;
        public float Speed = 12f;
        public float Damage = 6f;
        public float DamagePerLevel;
        public float Radius = 0.3f;
        public float MaxLifetime = 3f;
        public bool Pierce;
        public string DamageType = "projectile";

        public MultiShotEffect() { }
        public MultiShotEffect(int projectileCount, float spreadAngleDeg, float speed, float damage,
            float damagePerLevel = 0f, bool pierce = false, string damageType = "projectile")
        {
            ProjectileCount = Mathf.Max(1, projectileCount);
            SpreadAngleDeg = spreadAngleDeg;
            Speed = speed;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            Pierce = pierce;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;

            Vector2 baseDir = ctx.Direction.sqrMagnitude > 0.001f
                ? ((Vector2)ctx.Direction).normalized
                : (root.localScale.x >= 0f ? Vector2.right : Vector2.left);

            var damage = Damage + DamagePerLevel * (ctx.Level - 1);
            var startAngle = -SpreadAngleDeg * 0.5f;
            var step = ProjectileCount > 1 ? SpreadAngleDeg / (ProjectileCount - 1) : 0f;

            for (var i = 0; i < ProjectileCount; i++)
            {
                var rad = (startAngle + step * i) * Mathf.Deg2Rad;
                var cos = Mathf.Cos(rad);
                var sin = Mathf.Sin(rad);
                var dir = new Vector2(baseDir.x * cos - baseDir.y * sin, baseDir.x * sin + baseDir.y * cos);

                var go = new GameObject($"Projectile_{ctx.Definition?.Id}_{i}");
                go.transform.position = root.position;
                var p = go.AddComponent<SkillProjectile>();
                p.CasterId = ctx.CasterId;
                p.Velocity = (Vector3)(dir * Speed);
                p.Damage = damage;
                p.DamageType = DamageType;
                p.Radius = Radius;
                p.MaxLifetime = MaxLifetime;
                p.Pierce = Pierce;
            }
        }
    }
}
