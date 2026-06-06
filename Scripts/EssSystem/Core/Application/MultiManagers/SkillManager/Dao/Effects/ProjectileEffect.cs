using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class ProjectileEffect : ISkillEffect
    {
        public float Speed = 12f;
        public float Damage = 8f;
        public float DamagePerLevel;
        public string DamageType = "projectile";
        public float Radius = 0.3f;
        public float MaxLifetime = 4f;
        public bool Pierce;

        public ProjectileEffect() { }
        public ProjectileEffect(float speed, float damage, float damagePerLevel = 0f,
            string damageType = "projectile", float radius = 0.3f, float maxLifetime = 4f, bool pierce = false)
        {
            Speed = speed;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
            Radius = radius;
            MaxLifetime = maxLifetime;
            Pierce = pierce;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f ? Mathf.Sign(ctx.Direction.x) : (root.localScale.x >= 0f ? 1f : -1f);
            var dir = ctx.Direction.sqrMagnitude > 0.001f ? ctx.Direction.normalized : new Vector3(dirX, 0f, 0f);

            var go = new GameObject($"Projectile_{ctx.Definition?.Id}");
            go.transform.position = root.position;
            var p = go.AddComponent<SkillProjectile>();
            p.CasterId = ctx.CasterId;
            p.Velocity = dir * Speed;
            p.Damage = Damage + DamagePerLevel * (ctx.Level - 1);
            p.DamageType = DamageType;
            p.Radius = Radius;
            p.MaxLifetime = MaxLifetime;
            p.Pierce = Pierce;
        }
    }
}
