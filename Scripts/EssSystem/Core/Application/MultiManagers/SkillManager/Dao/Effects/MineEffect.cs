using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class MineEffect : ISkillEffect
    {
        public float Duration = 8f;
        public float ArmTime = 0.35f;
        public float TriggerRadius = 1.25f;
        public float Radius = 2.2f;
        public bool IncludeSelf;
        public bool DetonateOnExpire = true;
        public float ForwardOffset = 0.9f;
        public float HeightOffset = 0.05f;
        public Color Color = new(1f, 0.45f, 0.18f, 0.95f);
        public List<ISkillEffect> SubEffects = new();

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ctx.CasterId)) return;
            var go = new GameObject($"SkillMine_{ctx.Definition?.Id}");
            go.transform.position = ResolvePosition(ctx);
            var runner = go.AddComponent<MineRunner>();
            runner.Initialize(ctx, this);
        }

        private Vector3 ResolvePosition(SkillEffectContext ctx)
        {
            var origin = SkillEntityProxy.Position(ctx.CasterId);
            var dir = ctx.Direction.sqrMagnitude > 0.001f ? ctx.Direction.normalized : Vector3.right;
            dir.z = 0f;
            return origin + dir * ForwardOffset + Vector3.up * HeightOffset;
        }

        private sealed class MineRunner : MonoBehaviour
        {
            private static readonly Collider[] Buffer3D = new Collider[96];
            private static readonly Collider2D[] Buffer2D = new Collider2D[96];
            private static readonly ContactFilter2D Filter2D = ContactFilter2D.noFilter;

            private SkillEffectContext _ctx;
            private List<ISkillEffect> _effects;
            private float _duration;
            private float _armTime;
            private float _triggerRadius;
            private float _radius;
            private bool _includeSelf;
            private bool _detonateOnExpire;
            private float _age;
            private bool _detonated;
            private LineRenderer _ring;
            private Color _color;

            public void Initialize(SkillEffectContext ctx, MineEffect effect)
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
                _armTime = Mathf.Max(0f, effect.ArmTime);
                _triggerRadius = Mathf.Max(0.05f, effect.TriggerRadius);
                _radius = Mathf.Max(0.05f, effect.Radius);
                _includeSelf = effect.IncludeSelf;
                _detonateOnExpire = effect.DetonateOnExpire;
                _color = effect.Color;
                BuildVisual();
                Destroy(gameObject, _duration + 1f);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                UpdateVisual();
                if (_detonated) return;
                if (_age >= _armTime && HasTriggerTarget())
                {
                    Detonate();
                    return;
                }
                if (_age >= _duration)
                {
                    if (_detonateOnExpire) Detonate();
                    else Destroy(gameObject);
                }
            }

            private bool HasTriggerTarget()
            {
                var center = transform.position;
                var count2D = Physics2D.OverlapCircle(center, _triggerRadius, Filter2D, Buffer2D);
                for (var i = 0; i < count2D; i++)
                    if (IsValidTarget(SkillEntityProxy.IdFrom(Buffer2D[i]))) return true;

                var count3D = Physics.OverlapSphereNonAlloc(center, _triggerRadius, Buffer3D, ~0, QueryTriggerInteraction.Collide);
                for (var i = 0; i < count3D; i++)
                    if (IsValidTarget(SkillEntityProxy.IdFrom(Buffer3D[i]))) return true;

                return false;
            }

            private bool IsValidTarget(string targetId)
            {
                if (string.IsNullOrEmpty(targetId)) return false;
                if (!_includeSelf && targetId == _ctx.CasterId) return false;
                return !SkillEntityProxy.IsDead(targetId);
            }

            private void Detonate()
            {
                _detonated = true;
                var seen = new HashSet<string>();
                var center = transform.position;

                var count2D = Physics2D.OverlapCircle(center, _radius, Filter2D, Buffer2D);
                for (var i = 0; i < count2D; i++)
                    ApplyToTarget(SkillEntityProxy.IdFrom(Buffer2D[i]), seen);

                var count3D = Physics.OverlapSphereNonAlloc(center, _radius, Buffer3D, ~0, QueryTriggerInteraction.Collide);
                for (var i = 0; i < count3D; i++)
                    ApplyToTarget(SkillEntityProxy.IdFrom(Buffer3D[i]), seen);

                Destroy(gameObject);
            }

            private void ApplyToTarget(string targetId, HashSet<string> seen)
            {
                if (!IsValidTarget(targetId) || !seen.Add(targetId)) return;
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
                _ring.positionCount = 40;
                _ring.widthMultiplier = 0.05f;
                _ring.sortingOrder = 412;
                _ring.material = SkillCueMaterials.SpriteMaterial;
                for (var i = 0; i < _ring.positionCount; i++)
                {
                    var angle = Mathf.PI * 2f * i / _ring.positionCount;
                    _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * _triggerRadius, Mathf.Sin(angle) * _triggerRadius * 0.35f, 0f));
                }
            }

            private void UpdateVisual()
            {
                if (_ring == null) return;
                var armed = _age >= _armTime;
                var pulse = armed ? 0.62f + Mathf.Sin(Time.time * 12f) * 0.28f : 0.28f;
                _ring.startColor = new Color(_color.r, _color.g, _color.b, pulse);
                _ring.endColor = new Color(1f, 1f, 1f, pulse * 0.45f);
            }
        }
    }
}
