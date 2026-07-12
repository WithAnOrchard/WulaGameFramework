using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Runtime;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

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
        public string SpriteId;
        public string ImpactSpriteId;
        public string ImpactCharacterConfigId;
        public string ImpactActionName = "Special";
        public float ImpactScale = 1f;
        public float ImpactLifetime = 0.35f;
        public float AreaDamageRadius;
        public float AreaDamageMultiplier = 1f;
        public string ImpactSfxId;
        public float ImpactSfxVolume = 1f;
        public bool SuppressTargetHitSfx;
        public List<ISkillEffect> HitEffects = new();
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public bool IgnoreStaticTargets;

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
                p.SpriteId = SpriteId;
                p.ImpactSpriteId = ImpactSpriteId;
                p.ImpactCharacterConfigId = ImpactCharacterConfigId;
                p.ImpactActionName = ImpactActionName;
                p.ImpactScale = ImpactScale;
                p.ImpactLifetime = ImpactLifetime;
                p.AreaDamageRadius = AreaDamageRadius;
                p.AreaDamageMultiplier = AreaDamageMultiplier;
                p.ImpactSfxId = ImpactSfxId;
                p.ImpactSfxVolume = ImpactSfxVolume;
                p.SuppressTargetHitSfx = SuppressTargetHitSfx;
                p.SourceContext = ctx;
                p.HitEffects = HitEffects;
                p.VisualScale = VisualScale;
                p.VisualRotationOffsetDegrees = VisualRotationOffsetDegrees;
                p.SortingOrder = SortingOrder;
                p.IgnoreStaticTargets = IgnoreStaticTargets;
            }
        }
    }

    public class HomingMultiProjectileEffect : ProjectileEffect
    {
        public int ProjectileCount = 4;
        public float SearchRadius = 8f;
        public float HomingTurnSpeed = 900f;
        public float SpawnSpread = 0.22f;
        public float OrbitSpawnRadius = 0.55f;
        public float InitialArcStrength = 0.55f;
        public float HomingDelay = 0.14f;

        private static readonly Collider[] _buffer3D = new Collider[64];
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;

        public override void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;

            var baseDir = ctx.Direction.sqrMagnitude > 0.001f
                ? ctx.Direction.normalized
                : new Vector3(root.localScale.x >= 0f ? 1f : -1f, 0f, 0f);
            baseDir.z = 0f;

            var origin = root.position + baseDir * ForwardOffset + Vector3.up * HeightOffset;
            var orbitCenter = root.position + Vector3.up * HeightOffset;
            var facing = Mathf.Abs(baseDir.x) > 0.01f ? Mathf.Sign(baseDir.x) : (root.localScale.x >= 0f ? 1f : -1f);
            var targets = PickTargets(orbitCenter, ctx.CasterId);
            var count = Mathf.Max(1, ProjectileCount);
            var damage = Damage + DamagePerLevel * (ctx.Level - 1);

            for (var i = 0; i < count; i++)
            {
                var targetId = targets.Count > 0 ? targets[i % targets.Count] : null;
                var targetPos = !string.IsNullOrEmpty(targetId) ? SkillEntityProxy.Position(targetId, origin + baseDir) : origin + baseDir * SearchRadius;
                var spawnPos = ResolveOrbitSpawn(orbitCenter, count, i, facing);
                var dir = ResolveInitialDirection(spawnPos, targetPos, orbitCenter, count, i, facing, baseDir, !string.IsNullOrEmpty(targetId));
                var go = new GameObject($"Projectile_{ctx.Definition?.Id}_{i}");
                go.transform.position = spawnPos;
                var p = go.AddComponent<SkillProjectile>();
                p.CasterId = ctx.CasterId;
                p.Velocity = dir * Speed;
                p.Damage = damage;
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
                p.AreaDamageRadius = AreaDamageRadius;
                p.AreaDamageMultiplier = AreaDamageMultiplier;
                p.ImpactSfxId = ImpactSfxId;
                p.ImpactSfxVolume = ImpactSfxVolume;
                p.SuppressTargetHitSfx = SuppressTargetHitSfx;
                p.SourceContext = ctx;
                p.HitEffects = HitEffects;
                p.HomingTargetId = targetId;
                p.HomingTurnSpeed = HomingTurnSpeed * (0.92f + 0.05f * (i % 3));
                p.HomingDelay = !string.IsNullOrEmpty(targetId) ? HomingDelay + 0.012f * i : 0f;
                p.HomingSearchRadius = SearchRadius;
                p.VisualScale = VisualScale;
                p.VisualRotationOffsetDegrees = VisualRotationOffsetDegrees;
                p.SortingOrder = SortingOrder;
                p.IgnoreStaticTargets = IgnoreStaticTargets;
            }
        }

        private Vector3 ResolveOrbitSpawn(Vector3 center, int count, int index, float facing)
        {
            var radius = Mathf.Max(0.05f, OrbitSpawnRadius + SpawnSpread);
            var step = count > 1 ? 360f / count : 0f;
            var angle = (-70f * facing) + step * index + Mathf.Sin((index + 1) * 2.13f) * 14f;
            var rad = angle * Mathf.Deg2Rad;
            var x = Mathf.Cos(rad) * radius * 0.82f;
            var y = Mathf.Sin(rad) * radius * 0.58f;
            return center + new Vector3(x, y, 0f);
        }

        private Vector3 ResolveInitialDirection(Vector3 spawnPos, Vector3 targetPos, Vector3 center,
            int count, int index, float facing, Vector3 fallbackDir, bool hasTarget)
        {
            var toTarget = targetPos - spawnPos;
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude <= 0.001f) toTarget = fallbackDir;
            toTarget.Normalize();

            if (!hasTarget) return toTarget;

            var radial = spawnPos - center;
            radial.z = 0f;
            if (radial.sqrMagnitude <= 0.001f) radial = fallbackDir;
            radial.Normalize();

            var tangentSign = ((index & 1) == 0 ? 1f : -1f) * facing;
            var tangent = new Vector3(-radial.y, radial.x, 0f) * tangentSign;
            var arc = Mathf.Clamp01(InitialArcStrength) * 0.42f;
            var bias = (radial * 0.22f + tangent * (0.18f + 0.04f * (index % 3))).normalized;
            var dir = Vector3.Slerp(toTarget, bias, arc);
            if (count > 1)
            {
                var fan = (index - (count - 1) * 0.5f) / Mathf.Max(1f, count - 1f);
                dir = Quaternion.Euler(0f, 0f, fan * 7f) * dir;
            }

            dir.z = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : fallbackDir.normalized;
        }

        private List<string> PickTargets(Vector3 origin, string casterId)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            if (!string.IsNullOrEmpty(casterId)) seen.Add(casterId);

            var count2D = Physics2D.OverlapCircle(origin, SearchRadius, _contactFilter, _buffer2D);
            for (var i = 0; i < count2D; i++)
                AddCandidate(_buffer2D[i], result, seen);

            var count3D = Physics.OverlapSphereNonAlloc(origin, SearchRadius, _buffer3D, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count3D; i++)
                AddCandidate(_buffer3D[i], result, seen);

            result.Sort((a, b) =>
                (SkillEntityProxy.Position(a) - origin).sqrMagnitude.CompareTo((SkillEntityProxy.Position(b) - origin).sqrMagnitude));
            return result;
        }

        private static void AddCandidate(Object targetObject, List<string> result, HashSet<string> seen)
        {
            var handle = ResolveHandle(targetObject);
            if (handle == null || IsStatic(handle)) return;

            var id = handle.InstanceId;
            if (string.IsNullOrEmpty(id) || !seen.Add(id)) return;
            if (SkillEntityProxy.IsDead(id)) return;
            result.Add(id);
        }

        private static EntityHandle ResolveHandle(Object targetObject)
        {
            return targetObject switch
            {
                Collider col => col.GetComponentInParent<EntityHandle>(),
                Collider2D col2D => col2D.GetComponentInParent<EntityHandle>(),
                GameObject go => go.GetComponentInParent<EntityHandle>(),
                Transform tr => tr.GetComponentInParent<EntityHandle>(),
                Component component => component.GetComponentInParent<EntityHandle>(),
                _ => null
            };
        }

        private static bool IsStatic(EntityHandle handle)
        {
            return handle?.Entity != null &&
                   (handle.Entity.Kind == EntityKind.Static ||
                    (handle.Entity.Config != null && handle.Entity.Config.Kind == EntityKind.Static));
        }
    }
}
