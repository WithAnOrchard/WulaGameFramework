using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class ChainLightningEffect : ISkillEffect
    {
        public float BaseDamage = 15f;
        public float DamagePerLevel;
        public int MaxJumps = 4;
        public float JumpRadius = 4f;
        public float FalloffPerJump = 0.8f;
        public string DamageType = "lightning";
        public float VisualDuration = 0.16f;
        public float VisualWidth = 0.07f;
        public float SparkRadius = 0.22f;

        private static readonly Collider[] _buffer = new Collider[64];
        private static readonly Collider2D[] _buffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;
        private static Material _lightningMaterial;

        public ChainLightningEffect() { }
        public ChainLightningEffect(float baseDamage, int maxJumps = 4, float jumpRadius = 4f,
            float falloffPerJump = 0.8f, float damagePerLevel = 0f, string damageType = "lightning")
        {
            BaseDamage = baseDamage;
            MaxJumps = maxJumps;
            JumpRadius = jumpRadius;
            FalloffPerJump = falloffPerJump;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.CasterId)) return;
            var first = !string.IsNullOrEmpty(ctx.TargetId) ? ctx.TargetId : PickNearest(ctx.CasterId, ctx.CasterId);
            if (string.IsNullOrEmpty(first)) return;
            var baseDmg = BaseDamage + DamagePerLevel * (ctx.Level - 1);
            var hit = new HashSet<string> { ctx.CasterId };
            var current = first;
            for (var i = 0; i < MaxJumps && !string.IsNullOrEmpty(current); i++)
            {
                if (!hit.Add(current)) break;
                if (SkillEntityProxy.IsDead(current)) break;
                var currentPosition = SkillEntityProxy.Position(current);
                var damage = baseDmg * Mathf.Pow(Mathf.Clamp01(FalloffPerJump), i);
                SkillEntityProxy.Damage(current, damage, ctx.CasterId, DamageType, currentPosition, true);
                SpawnImpactSpark(currentPosition, i);

                var next = i < MaxJumps - 1 ? PickNearest(current, null, hit) : null;
                if (!string.IsNullOrEmpty(next))
                    SpawnChainVisual(currentPosition, SkillEntityProxy.Position(next), i);
                current = next;
            }
        }

        private string PickNearest(string fromId, string excludeId, HashSet<string> excluded = null)
        {
            var origin = SkillEntityProxy.Position(fromId);
            var count2D = Physics2D.OverlapCircle(origin, JumpRadius, _contactFilter, _buffer2D);
            string best = null;
            var bestDist = float.MaxValue;

            for (var i = 0; i < count2D; i++)
                CheckCandidate(_buffer2D[i], origin, excludeId, excluded, ref best, ref bestDist);

            var count = Physics.OverlapSphereNonAlloc(origin, JumpRadius, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
                CheckCandidate(_buffer[i], origin, excludeId, excluded, ref best, ref bestDist);

            return best;
        }

        private static void CheckCandidate(Object targetObject, Vector3 origin, string excludeId,
            HashSet<string> excluded, ref string best, ref float bestDist)
        {
            var handle = ResolveHandle(targetObject);
            if (handle == null || IsStatic(handle)) return;

            var id = handle.InstanceId;
            if (string.IsNullOrEmpty(id) || id == excludeId || (excluded != null && excluded.Contains(id))) return;
            if (SkillEntityProxy.IsDead(id)) return;
            var dist = (SkillEntityProxy.Position(id) - origin).sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = id;
            }
        }

        private void SpawnChainVisual(Vector3 from, Vector3 to, int index)
        {
            var go = new GameObject($"ChainLightning_{index}");
            var positions = BuildJaggedPath(from, to, index, 7, 0.18f);
            CreateLine(go, "Glow", positions, VisualWidth * 3.1f,
                new Color(0.18f, 0.72f, 1f, 0.34f), new Color(0.92f, 0.35f, 1f, 0.22f), 288);
            CreateLine(go, "Core", positions, VisualWidth,
                new Color(1f, 0.98f, 0.42f, 1f), new Color(0.55f, 0.96f, 1f, 0.95f), 291);

            SpawnBranches(go, positions, index);
            SpawnArcParticles(go.transform, from, to, index);
            Object.Destroy(go, Mathf.Max(0.03f, VisualDuration));
        }

        private void SpawnImpactSpark(Vector3 position, int index)
        {
            var go = new GameObject($"ChainLightningSpark_{index}");
            go.transform.position = position;

            for (var i = 0; i < 5; i++)
            {
                var angle = (index * 31f + i * 72f) * Mathf.Deg2Rad;
                var end = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                    (SparkRadius * (0.65f + (i % 2) * 0.35f));
                CreateLine(go, $"Spark_{i}", new[] { position, end }, VisualWidth * 0.42f,
                    new Color(1f, 1f, 0.52f, 0.95f), new Color(0.38f, 0.9f, 1f, 0f), 292);
            }

            SpawnArcParticles(go.transform, position + Vector3.left * 0.05f, position + Vector3.right * 0.05f, index + 20);
            Object.Destroy(go, Mathf.Max(0.04f, VisualDuration * 1.25f));
        }

        private static Vector3[] BuildJaggedPath(Vector3 from, Vector3 to, int index, int points, float amplitude)
        {
            points = Mathf.Max(2, points);
            var result = new Vector3[points];
            var delta = to - from;
            var normal = new Vector3(-delta.y, delta.x, 0f).normalized;
            var distanceScale = Mathf.Clamp(delta.magnitude, 0.6f, 5f);
            var amp = amplitude * distanceScale;

            for (var i = 0; i < points; i++)
            {
                var t = i / (float)(points - 1);
                var jitter = i == 0 || i == points - 1
                    ? 0f
                    : Mathf.Sin((index + 1) * 19.37f + i * 11.71f) * amp * (0.45f + (i % 2) * 0.35f);
                result[i] = Vector3.Lerp(from, to, t) + normal * jitter;
            }

            return result;
        }

        private static void SpawnBranches(GameObject root, Vector3[] positions, int index)
        {
            if (positions == null || positions.Length < 4) return;
            for (var i = 1; i < positions.Length - 1; i += 2)
            {
                var prev = positions[i - 1];
                var next = positions[i + 1];
                var tangent = (next - prev).normalized;
                var normal = new Vector3(-tangent.y, tangent.x, 0f);
                var sign = ((index + i) & 1) == 0 ? 1f : -1f;
                var length = 0.18f + (i % 3) * 0.06f;
                var branch = new[] { positions[i], positions[i] + normal * sign * length + tangent * 0.05f };
                CreateLine(root, $"Branch_{i}", branch, 0.028f,
                    new Color(0.96f, 1f, 0.75f, 0.86f), new Color(0.42f, 0.9f, 1f, 0f), 292);
            }
        }

        private static void SpawnArcParticles(Transform parent, Vector3 from, Vector3 to, int index)
        {
            if (parent == null) return;

            var particles = new GameObject($"ArcParticles_{index}");
            particles.transform.SetParent(parent, false);
            particles.transform.position = Vector3.Lerp(from, to, 0.5f);

            var ps = particles.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.12f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.15f, 2.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 0.48f, 1f),
                new Color(0.22f, 0.96f, 1f, 0.92f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = LightningMaterial();
            renderer.sortingOrder = 294;
            ps.Play();
        }

        private static LineRenderer CreateLine(GameObject root, string name, Vector3[] positions,
            float width, Color start, Color end, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = positions.Length;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.startWidth = Mathf.Max(0.005f, width);
            line.endWidth = Mathf.Max(0.003f, width * 0.65f);
            line.startColor = start;
            line.endColor = end;
            line.sortingOrder = sortingOrder;
            line.material = LightningMaterial();
            line.SetPositions(positions);
            return line;
        }

        private static Material LightningMaterial()
        {
            if (_lightningMaterial != null) return _lightningMaterial;
            _lightningMaterial = new Material(Shader.Find("Sprites/Default"));
            return _lightningMaterial;
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
