using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class ZoneEffect : ISkillEffect
    {
        public float Duration = 4f;
        public float TickInterval = 0.5f;
        public float Radius = 2.5f;
        public float RadiusPerLevel;
        public bool IncludeSelf;
        public float ForwardOffset;
        public float HeightOffset = 0.05f;
        public Color Color = new(0.6f, 0.95f, 1f, 0.85f);
        public List<ISkillEffect> SubEffects = new();

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.CasterId)) return;
            var center = ResolveCenter(ctx);
            var go = new GameObject($"SkillZone_{ctx.Definition?.Id}");
            go.transform.position = center;
            var runner = go.AddComponent<ZoneRunner>();
            runner.Initialize(ctx, this);
        }

        private Vector3 ResolveCenter(SkillEffectContext ctx)
        {
            if (ctx.Position != Vector3.zero)
                return ctx.Position + Vector3.up * HeightOffset;

            var origin = SkillEntityProxy.Position(ctx.CasterId);
            var dir = ctx.Direction.sqrMagnitude > 0.001f ? ctx.Direction.normalized : Vector3.right;
            dir.z = 0f;
            return origin + dir * ForwardOffset + Vector3.up * HeightOffset;
        }

        private sealed class ZoneRunner : MonoBehaviour
        {
            private static readonly Collider[] Buffer3D = new Collider[96];
            private static readonly Collider2D[] Buffer2D = new Collider2D[96];
            private static readonly ContactFilter2D Filter2D = ContactFilter2D.noFilter;

            private SkillEffectContext _ctx;
            private List<ISkillEffect> _effects;
            private float _duration;
            private float _tickInterval;
            private float _radius;
            private bool _includeSelf;
            private float _age;
            private float _tickTimer;
            private LineRenderer _ring;
            private Color _color;

            public void Initialize(SkillEffectContext ctx, ZoneEffect effect)
            {
                _ctx = new SkillEffectContext
                {
                    CasterId = ctx.CasterId,
                    Definition = ctx.Definition,
                    Instance = ctx.Instance,
                    Direction = ctx.Direction,
                    Position = transform.position,
                };
                _effects = effect.SubEffects ?? new List<ISkillEffect>();
                _duration = Mathf.Max(0.05f, effect.Duration);
                _tickInterval = Mathf.Max(0.05f, effect.TickInterval);
                _radius = Mathf.Max(0.05f, effect.Radius + effect.RadiusPerLevel * (ctx.Level - 1));
                _includeSelf = effect.IncludeSelf;
                _color = effect.Color;
                BuildVisual();
                TickZone();
                Destroy(gameObject, _duration + 0.5f);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                _tickTimer += Time.deltaTime;
                UpdateVisual();
                if (_tickTimer >= _tickInterval)
                {
                    _tickTimer -= _tickInterval;
                    TickZone();
                }
                if (_age >= _duration)
                    Destroy(gameObject);
            }

            private void TickZone()
            {
                if (_effects == null || _effects.Count == 0) return;
                var seen = new HashSet<string>();
                var center = transform.position;

                var count2D = Physics2D.OverlapCircle(center, _radius, Filter2D, Buffer2D);
                for (var i = 0; i < count2D; i++)
                    ApplyToTarget(SkillEntityProxy.IdFrom(Buffer2D[i]), seen);

                var count3D = Physics.OverlapSphereNonAlloc(center, _radius, Buffer3D, ~0, QueryTriggerInteraction.Collide);
                for (var i = 0; i < count3D; i++)
                    ApplyToTarget(SkillEntityProxy.IdFrom(Buffer3D[i]), seen);
            }

            private void ApplyToTarget(string targetId, HashSet<string> seen)
            {
                if (string.IsNullOrEmpty(targetId) || !seen.Add(targetId)) return;
                if (!_includeSelf && targetId == _ctx.CasterId) return;
                if (SkillEntityProxy.IsDead(targetId)) return;

                _ctx.TargetId = targetId;
                _ctx.Position = transform.position;
                for (var i = 0; i < _effects.Count; i++)
                    _effects[i]?.Apply(_ctx);
            }

            private void BuildVisual()
            {
                _ring = gameObject.AddComponent<LineRenderer>();
                _ring.useWorldSpace = false;
                _ring.loop = true;
                _ring.positionCount = 64;
                _ring.widthMultiplier = 0.045f;
                _ring.sortingOrder = 410;
                _ring.material = SkillCueMaterials.SpriteMaterial;
                for (var i = 0; i < _ring.positionCount; i++)
                {
                    var angle = Mathf.PI * 2f * i / _ring.positionCount;
                    _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius * 0.35f, 0f));
                }
            }

            private void UpdateVisual()
            {
                if (_ring == null) return;
                var life = _duration > 0f ? Mathf.Clamp01(_age / _duration) : 1f;
                var pulse = 0.72f + Mathf.Sin(Time.time * 8f) * 0.18f;
                var alpha = Mathf.Lerp(0.78f, 0f, Mathf.Max(0f, life - 0.72f) / 0.28f) * pulse;
                _ring.startColor = new Color(_color.r, _color.g, _color.b, alpha);
                _ring.endColor = new Color(1f, 1f, 1f, alpha * 0.55f);
            }
        }
    }
}
