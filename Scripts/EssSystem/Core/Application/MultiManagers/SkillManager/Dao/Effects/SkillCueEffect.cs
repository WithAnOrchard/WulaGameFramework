using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class SkillCueEffect : ISkillEffect, ISkillCastStartEffect
    {
        public string SfxId;
        public float SfxVolume = 1f;
        public Color Color = new(0.75f, 0.95f, 1f, 1f);
        public float Radius = 1.2f;
        public float Duration = 0.45f;
        public float HeightOffset = 0.35f;
        public int BurstCount = 42;
        public bool CueAtTarget;
        public bool CueAtPosition;
        public bool PlayOnCastStart = true;
        public bool PlayOnApply;

        public void OnCastStart(SkillEffectContext ctx)
        {
            if (PlayOnCastStart) Play(ctx);
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (PlayOnApply) Play(ctx);
        }

        private void Play(SkillEffectContext ctx)
        {
            if (ctx == null) return;
            PlaySfx();
            SpawnVisual(ResolvePosition(ctx));
        }

        private void PlaySfx()
        {
            if (string.IsNullOrEmpty(SfxId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod("PlaySFX", new List<object> { SfxId, SfxVolume });
        }

        private Vector3 ResolvePosition(SkillEffectContext ctx)
        {
            if (CueAtTarget && !string.IsNullOrEmpty(ctx.TargetId))
                return ResolveEntityFootPosition(ctx.TargetId, SkillEntityProxy.Position(ctx.TargetId)) + Vector3.up * HeightOffset;
            if (CueAtPosition && ctx.Position != Vector3.zero)
                return ctx.Position + Vector3.up * HeightOffset;
            return ResolveEntityFootPosition(ctx.CasterId, SkillEntityProxy.Position(ctx.CasterId, ctx.Position));
        }

        private static Vector3 ResolveEntityFootPosition(string entityId, Vector3 fallback)
        {
            var root = SkillEntityProxy.Root(entityId);
            if (root == null) return fallback;

            if (!TryResolveBounds(root, out var bounds))
                return root.position;

            return new Vector3(root.position.x, bounds.min.y, root.position.z);
        }

        private static bool TryResolveBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(root.position, Vector3.zero);
            var hasBounds = false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }

            var colliders2D = root.GetComponentsInChildren<Collider2D>(true);
            for (var i = 0; i < colliders2D.Length; i++)
            {
                var collider = colliders2D[i];
                if (collider == null) continue;
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(collider.bounds);
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null) continue;
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(collider.bounds);
            }

            return hasBounds;
        }

        private void SpawnVisual(Vector3 position)
        {
            if (Duration <= 0f || Radius <= 0f) return;

            var root = new GameObject("SkillCue");
            root.transform.position = position;
            Object.Destroy(root, Duration + 1.2f);

            CreateRing(root.transform, Radius, Color, Duration, 0.055f, 1f);
            CreateRing(root.transform, Radius * 0.68f, UnityEngine.Color.Lerp(Color, UnityEngine.Color.white, 0.45f), Duration * 0.82f, 0.035f, 0.65f);
            CreateParticles(root.transform, Color);
        }

        private void CreateParticles(Transform parent, Color color)
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.05f, Duration);
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, Duration * 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(Radius * 0.9f, Radius * 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(color.r, color.g, color.b, 0.95f),
                new Color(1f, 1f, 1f, 0.72f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(BurstCount, 8, 160)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.08f, Radius * 0.35f);
            shape.arc = 360f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-Radius * 0.28f, Radius * 0.28f);
            velocity.y = new ParticleSystem.MinMaxCurve(Radius * 0.32f, Radius * 1.15f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 420;
            renderer.material = SkillCueMaterials.SpriteMaterial;
            ps.Play();
        }

        private static void CreateRing(Transform parent, float radius, Color color,
            float duration, float width, float startScale)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * startScale;

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 48;
            lr.widthMultiplier = width;
            lr.sortingOrder = 418;
            lr.material = SkillCueMaterials.SpriteMaterial;
            lr.startColor = new Color(color.r, color.g, color.b, 0.95f);
            lr.endColor = new Color(1f, 1f, 1f, 0.55f);
            for (var i = 0; i < lr.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / lr.positionCount;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.35f, 0f));
            }

            var pulse = go.AddComponent<SkillCuePulse>();
            pulse.Duration = duration;
            pulse.Color = color;
            pulse.TargetScale = 1.18f;
        }

        private sealed class SkillCuePulse : MonoBehaviour
        {
            public float Duration = 0.45f;
            public float TargetScale = 1.15f;
            public Color Color = Color.white;

            private LineRenderer _line;
            private float _age;
            private Vector3 _startScale;

            private void Awake()
            {
                _line = GetComponent<LineRenderer>();
                _startScale = transform.localScale;
            }

            private void Update()
            {
                _age += Time.deltaTime;
                var t = Duration > 0f ? Mathf.Clamp01(_age / Duration) : 1f;
                var ease = 1f - Mathf.Pow(1f - t, 2f);
                transform.localScale = Vector3.Lerp(_startScale, Vector3.one * TargetScale, ease);
                if (_line != null)
                {
                    var a = Mathf.Lerp(0.95f, 0f, t);
                    _line.startColor = new Color(Color.r, Color.g, Color.b, a);
                    _line.endColor = new Color(1f, 1f, 1f, a * 0.55f);
                }
                if (t >= 1f) Destroy(gameObject);
            }
        }
    }

    internal static class SkillCueMaterials
    {
        private static Material _spriteMaterial;

        public static Material SpriteMaterial
        {
            get
            {
                if (_spriteMaterial != null) return _spriteMaterial;
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                _spriteMaterial = new Material(shader);
                return _spriteMaterial;
            }
        }
    }
}
