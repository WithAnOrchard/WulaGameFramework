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
        public string SpriteId;
        public string ImpactSpriteId;
        public string ImpactCharacterConfigId;
        public string ImpactActionName = "Special";
        public float ImpactScale = 1f;
        public float ImpactLifetime = 0.35f;
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public float ForwardOffset = 0.7f;
        public float HeightOffset = 0.7f;
        public bool IgnoreStaticTargets;

        public ProjectileEffect() { }
        public ProjectileEffect(float speed, float damage, float damagePerLevel = 0f,
            string damageType = "projectile", float radius = 0.3f, float maxLifetime = 4f, bool pierce = false,
            string spriteId = null, string impactSpriteId = null, float visualScale = 1f,
            int sortingOrder = 260, float forwardOffset = 0.7f, float heightOffset = 0.7f,
            string impactCharacterConfigId = null, string impactActionName = "Special",
            float impactScale = 1f, float impactLifetime = 0.35f,
            float visualRotationOffsetDegrees = 0f, bool ignoreStaticTargets = false)
        {
            Speed = speed;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
            Radius = radius;
            MaxLifetime = maxLifetime;
            Pierce = pierce;
            SpriteId = spriteId;
            ImpactSpriteId = impactSpriteId;
            ImpactCharacterConfigId = impactCharacterConfigId;
            ImpactActionName = impactActionName;
            ImpactScale = impactScale;
            ImpactLifetime = impactLifetime;
            VisualScale = visualScale;
            VisualRotationOffsetDegrees = visualRotationOffsetDegrees;
            SortingOrder = sortingOrder;
            ForwardOffset = forwardOffset;
            HeightOffset = heightOffset;
            IgnoreStaticTargets = ignoreStaticTargets;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f ? Mathf.Sign(ctx.Direction.x) : (root.localScale.x >= 0f ? 1f : -1f);
            var dir = ctx.Direction.sqrMagnitude > 0.001f ? ctx.Direction.normalized : new Vector3(dirX, 0f, 0f);

            var go = new GameObject($"Projectile_{ctx.Definition?.Id}");
            go.transform.position = root.position + dir * ForwardOffset + Vector3.up * HeightOffset;
            var p = go.AddComponent<SkillProjectile>();
            p.CasterId = ctx.CasterId;
            p.Velocity = dir * Speed;
            p.Damage = Damage + DamagePerLevel * (ctx.Level - 1);
            p.DamageType = DamageType;
            p.Radius = Radius;
            p.MaxLifetime = MaxLifetime;
            p.Pierce = Pierce;
            p.SpriteId = SpriteId;
            p.ImpactSpriteId = ImpactSpriteId;
            p.ImpactCharacterConfigId = ImpactCharacterConfigId;
            p.ImpactActionName = ImpactActionName;
            p.ImpactScale = ImpactScale;
            p.ImpactLifetime = ImpactLifetime;
            p.VisualScale = VisualScale;
            p.VisualRotationOffsetDegrees = VisualRotationOffsetDegrees;
            p.SortingOrder = SortingOrder;
            p.IgnoreStaticTargets = IgnoreStaticTargets;
        }
    }
}
