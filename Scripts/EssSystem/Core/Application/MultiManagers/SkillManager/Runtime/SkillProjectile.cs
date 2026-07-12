using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Runtime
{
    /// <summary>
    /// 投射物运行时 —— 沿初始 <see cref="Velocity"/> 飞行，命中可反查 entityId 的目标即结算伤害（3D 物理版）。
    /// <list type="bullet">
    /// <item>纯 Update 推进 transform（不依赖 2D Rigidbody）。</item>
    /// <item><see cref="MaxLifetime"/> 秒后自动销毁。</item>
    /// <item>命中检测：<c>Physics.OverlapSphereNonAlloc</c> 3D 球内 collider。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillProjectile : MonoBehaviour
    {
        public string CasterId;
        public Vector3 Velocity;
        public float Damage;
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
        public float AreaDamageRadius;
        public float AreaDamageMultiplier = 1f;
        public string ImpactSfxId;
        public float ImpactSfxVolume = 1f;
        public bool SuppressTargetHitSfx;
        public SkillEffectContext SourceContext;
        public System.Collections.Generic.List<ISkillEffect> HitEffects;
        public string HomingTargetId;
        public float HomingTurnSpeed = 720f;
        public float HomingDelay;
        public float HomingSearchRadius;
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public bool IgnoreStaticTargets;

        private float _aliveTime;
        private float _homingAge;
        private System.Collections.Generic.HashSet<string> _hit;
        private static int _impactSeq;
        private static readonly Collider[] _buffer = new Collider[16];
        private static readonly Collider2D[] _buffer2D = new Collider2D[16];
        private static readonly Collider[] _areaBuffer = new Collider[64];
        private static readonly Collider2D[] _areaBuffer2D = new Collider2D[64];
        private static readonly Collider[] _homingBuffer = new Collider[64];
        private static readonly Collider2D[] _homingBuffer2D = new Collider2D[64];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;
        private static Sprite _fallbackSprite;
        private static Material _effectMaterial;

        private void Start()
        {
            EnsureVisual();
            EnsureParticleTrail();
        }

        private void Update()
        {
            UpdateHoming();
            transform.position += Velocity * Time.deltaTime;
            RotateVisualToVelocity();

            _aliveTime += Time.deltaTime;
            if (_aliveTime >= MaxLifetime) { Destroy(gameObject); return; }

            if (TryHit2D()) return;
            TryHit3D();
        }

        private bool TryHit2D()
        {
            var count = Physics2D.OverlapCircle(transform.position, Radius, _contactFilter, _buffer2D);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer2D[i];
                if (col == null) continue;
                if (TryDamageTarget(col)) return true;
            }
            return false;
        }

        private bool TryHit3D()
        {
            var count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                if (TryDamageTarget(col)) return true;
            }
            return false;
        }

        private bool TryDamageTarget(Object targetObject)
        {
            var handle = ResolveHandle(targetObject);
            if (IgnoreStaticTargets && IsStaticTarget(handle)) return false;

            var targetId = !string.IsNullOrEmpty(handle?.InstanceId) ? handle.InstanceId : SkillEntityProxy.IdFrom(targetObject);
            if (string.IsNullOrEmpty(targetId) || targetId == CasterId) return false;

            if (Pierce)
            {
                _hit ??= new System.Collections.Generic.HashSet<string>();
                if (!_hit.Add(targetId)) return false;
            }

            if (SkillEntityProxy.IsDead(targetId)) return false;
            var impactPosition = transform.position;
            if (Damage > 0f)
                SkillEntityProxy.Damage(targetId, Damage, CasterId, DamageType, impactPosition, SuppressTargetHitSfx);
            ApplyHitEffects(targetId, impactPosition);
            ApplyAreaDamage(targetId, impactPosition);
            SpawnImpact();
            PlayImpactSfx();

            if (!Pierce) { Destroy(gameObject); return true; }
            return false;
        }

        private void ApplyHitEffects(string targetId, Vector3 impactPosition)
        {
            if (HitEffects == null || HitEffects.Count == 0) return;
            for (var i = 0; i < HitEffects.Count; i++)
            {
                var effect = HitEffects[i];
                if (effect == null) continue;
                var ctx = new SkillEffectContext
                {
                    CasterId = CasterId,
                    TargetId = targetId,
                    Definition = SourceContext?.Definition,
                    Instance = SourceContext?.Instance,
                    Direction = Velocity.sqrMagnitude > 0.001f ? Velocity.normalized : SourceContext?.Direction ?? Vector3.right,
                    Position = impactPosition,
                };
                effect.Apply(ctx);
            }
        }

        private void UpdateHoming()
        {
            if (string.IsNullOrEmpty(HomingTargetId) && HomingSearchRadius <= 0f) return;
            _homingAge += Time.deltaTime;
            if (_homingAge < HomingDelay) return;

            if (!IsValidHomingTarget(HomingTargetId))
            {
                HomingTargetId = FindHomingTarget();
                if (string.IsNullOrEmpty(HomingTargetId)) return;
            }

            var speed = Velocity.magnitude;
            if (speed <= 0.001f) return;

            var desired = SkillEntityProxy.Position(HomingTargetId) - transform.position;
            desired.z = 0f;
            if (desired.sqrMagnitude <= 0.0001f) return;

            var desiredDir = desired.normalized;
            if (HomingTurnSpeed <= 0f)
            {
                Velocity = desiredDir * speed;
                return;
            }

            var turnT = 1f - Mathf.Exp(-Mathf.Max(0.01f, HomingTurnSpeed) * Mathf.Deg2Rad * Time.deltaTime);
            var currentDir = Velocity.normalized;
            var nextDir = Vector3.Slerp(currentDir, desiredDir, Mathf.Clamp01(turnT));
            Velocity = nextDir.normalized * speed;
        }

        private bool IsValidHomingTarget(string targetId)
        {
            if (string.IsNullOrEmpty(targetId) || targetId == CasterId) return false;
            if (SkillEntityProxy.IsDead(targetId, true)) return false;
            return SkillEntityProxy.Root(targetId) != null;
        }

        private string FindHomingTarget()
        {
            if (HomingSearchRadius <= 0f) return null;

            var origin = transform.position;
            var bestTargetId = (string)null;
            var bestDistance = float.MaxValue;

            var count2D = Physics2D.OverlapCircle(origin, HomingSearchRadius, _contactFilter, _homingBuffer2D);
            for (var i = 0; i < count2D; i++)
                ConsiderHomingCandidate(_homingBuffer2D[i], origin, ref bestTargetId, ref bestDistance);

            var count3D = Physics.OverlapSphereNonAlloc(origin, HomingSearchRadius, _homingBuffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count3D; i++)
                ConsiderHomingCandidate(_homingBuffer[i], origin, ref bestTargetId, ref bestDistance);

            return bestTargetId;
        }

        private void ConsiderHomingCandidate(Object targetObject, Vector3 origin,
            ref string bestTargetId, ref float bestDistance)
        {
            var handle = ResolveHandle(targetObject);
            if (handle == null || IsStaticTarget(handle)) return;

            var targetId = handle.InstanceId;
            if (!IsValidHomingTarget(targetId)) return;
            if (Pierce && _hit != null && _hit.Contains(targetId)) return;

            var distance = (SkillEntityProxy.Position(targetId, origin) - origin).sqrMagnitude;
            if (distance >= bestDistance) return;

            bestDistance = distance;
            bestTargetId = targetId;
        }

        private void ApplyAreaDamage(string primaryTargetId, Vector3 center)
        {
            if (AreaDamageRadius <= 0f || Damage <= 0f) return;
            var amount = Damage * Mathf.Max(0f, AreaDamageMultiplier);
            if (amount <= 0f) return;

            var damaged = new System.Collections.Generic.HashSet<string>();
            if (!string.IsNullOrEmpty(CasterId)) damaged.Add(CasterId);
            if (!string.IsNullOrEmpty(primaryTargetId)) damaged.Add(primaryTargetId);

            var count2D = Physics2D.OverlapCircle(center, AreaDamageRadius, _contactFilter, _areaBuffer2D);
            for (var i = 0; i < count2D; i++)
            {
                var col = _areaBuffer2D[i];
                if (col == null) continue;
                TryDamageAreaTarget(col, damaged, amount, center);
            }

            var count3D = Physics.OverlapSphereNonAlloc(center, AreaDamageRadius, _areaBuffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count3D; i++)
            {
                var col = _areaBuffer[i];
                if (col == null) continue;
                TryDamageAreaTarget(col, damaged, amount, center);
            }
        }

        private void TryDamageAreaTarget(Object targetObject, System.Collections.Generic.HashSet<string> damaged,
            float amount, Vector3 sourcePosition)
        {
            var handle = ResolveHandle(targetObject);
            if (IgnoreStaticTargets && IsStaticTarget(handle)) return;

            var targetId = !string.IsNullOrEmpty(handle?.InstanceId) ? handle.InstanceId : SkillEntityProxy.IdFrom(targetObject);
            if (string.IsNullOrEmpty(targetId) || !damaged.Add(targetId)) return;
            if (SkillEntityProxy.IsDead(targetId)) return;

            SkillEntityProxy.Damage(targetId, amount, CasterId, DamageType, sourcePosition, SuppressTargetHitSfx);
        }

        private void EnsureVisual()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = ResolveSprite(SpriteId) ?? FallbackSprite();
            sr.sortingOrder = SortingOrder;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, VisualScale);
            RotateVisualToVelocity();
        }

        private void EnsureParticleTrail()
        {
            if (!TryResolveTrailColors(out var colorA, out var colorB)) return;

            var particles = new GameObject("SkillParticleTrail");
            particles.transform.SetParent(transform, false);
            particles.transform.localPosition = Vector3.zero;

            var ps = particles.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = MaxLifetime;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.44f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.58f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = DamageType == "lightning" ? 90f : 58f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.08f, Radius * 0.85f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = EffectMaterial();
            renderer.sortingOrder = SortingOrder - 1;
            ps.Play();

            BuildRibbonTrail(colorA, colorB);
        }

        private void BuildRibbonTrail(Color colorA, Color colorB)
        {
            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = DamageType == "lightning" ? 0.20f : 0.28f;
            trail.minVertexDistance = 0.02f;
            trail.numCapVertices = 3;
            trail.numCornerVertices = 3;
            trail.startWidth = Mathf.Max(0.10f, Radius * 0.72f);
            trail.endWidth = 0.01f;
            trail.startColor = WithAlpha(colorA, 0.62f);
            trail.endColor = WithAlpha(colorB, 0f);
            trail.material = EffectMaterial();
            trail.sortingOrder = SortingOrder - 2;
        }

        private bool TryResolveTrailColors(out Color colorA, out Color colorB)
        {
            switch ((DamageType ?? string.Empty).ToLowerInvariant())
            {
                case "fire":
                    colorA = new Color(1f, 0.32f, 0.02f, 1f);
                    colorB = new Color(1f, 0.88f, 0.18f, 0.58f);
                    return true;
                case "frost":
                case "ice":
                    colorA = new Color(0.86f, 1f, 1f, 1f);
                    colorB = new Color(0.24f, 0.78f, 1f, 0.62f);
                    return true;
                case "lightning":
                case "thunder":
                    colorA = new Color(1f, 1f, 0.32f, 1f);
                    colorB = new Color(0.25f, 0.95f, 1f, 0.66f);
                    return true;
                case "arcane":
                    colorA = new Color(0.98f, 0.22f, 1f, 1f);
                    colorB = new Color(0.25f, 0.72f, 1f, 0.58f);
                    return true;
                default:
                    colorA = default;
                    colorB = default;
                    return false;
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void RotateVisualToVelocity()
        {
            if (Velocity.sqrMagnitude <= 0.0001f) return;
            var angle = Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + VisualRotationOffsetDegrees);
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

        private static bool IsStaticTarget(EntityHandle handle)
        {
            if (handle?.Entity == null) return false;
            if (handle.Entity.Kind == EntityKind.Static) return true;
            return handle.Entity.Config != null && handle.Entity.Config.Kind == EntityKind.Static;
        }

        private void SpawnImpact()
        {
            if (SpawnImpactCharacter()) return;

            var sprite = ResolveSprite(ImpactSpriteId);
            if (sprite == null) return;

            var go = new GameObject($"Impact_{DamageType}");
            go.transform.position = transform.position;
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, ImpactScale);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrder + 1;
            Destroy(go, Mathf.Max(0.01f, ImpactLifetime));
        }

        private bool SpawnImpactCharacter()
        {
            return SpawnCharacterEffect(ImpactCharacterConfigId, ImpactActionName, transform.position,
                ImpactScale, ImpactLifetime);
        }

        private void PlayImpactSfx()
        {
            if (string.IsNullOrEmpty(ImpactSfxId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                "PlaySFX",
                new System.Collections.Generic.List<object> { ImpactSfxId, Mathf.Max(0f, ImpactSfxVolume) });
        }

        public static bool SpawnCharacterEffect(string configId, string actionName, Vector3 position,
            float scale, float lifetime)
        {
            if (string.IsNullOrEmpty(configId) || !EventProcessor.HasInstance) return false;

            var instanceId = $"SkillEffect_{++_impactSeq}";
            var result = EventProcessor.Instance.TriggerEventMethod(
                "CreateCharacter",
                new System.Collections.Generic.List<object> { configId, instanceId, null, position });

            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not Transform root) return false;

            EventProcessor.Instance.TriggerEventMethod(
                "SetCharacterScale",
                new System.Collections.Generic.List<object> { instanceId, Vector3.one * Mathf.Max(0.01f, scale) });

            if (!string.IsNullOrEmpty(actionName))
            {
                EventProcessor.Instance.TriggerEventMethod(
                    "PlayCharacterAction",
                    new System.Collections.Generic.List<object> { instanceId, actionName });
            }

            var lifetimeComponent = root.gameObject.AddComponent<ImpactCharacterLifetime>();
            lifetimeComponent.Initialize(instanceId, Mathf.Max(0.01f, lifetime));
            return true;
        }

        private static Sprite ResolveSprite(string spriteId)
        {
            if (string.IsNullOrEmpty(spriteId) || !EventProcessor.HasInstance) return null;
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetSprite", new System.Collections.Generic.List<object> { spriteId });
            return ResultCode.IsOk(result) && result.Count > 1 ? result[1] as Sprite : null;
        }

        private static Sprite FallbackSprite()
        {
            if (_fallbackSprite != null) return _fallbackSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, new Color(1f, 0.42f, 0.12f, 1f));
            tex.Apply();
            _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _fallbackSprite;
        }

        private static Material EffectMaterial()
        {
            if (_effectMaterial != null) return _effectMaterial;
            _effectMaterial = new Material(Shader.Find("Sprites/Default"));
            return _effectMaterial;
        }

        private sealed class ImpactCharacterLifetime : MonoBehaviour
        {
            private string _instanceId;
            private float _remaining;
            private bool _destroyed;

            public void Initialize(string instanceId, float lifetime)
            {
                _instanceId = instanceId;
                _remaining = lifetime;
            }

            private void Update()
            {
                if (_destroyed) return;
                _remaining -= Time.deltaTime;
                if (_remaining > 0f) return;

                _destroyed = true;
                if (EventProcessor.HasInstance)
                {
                    EventProcessor.Instance.TriggerEventMethod(
                        "DestroyCharacter",
                        new System.Collections.Generic.List<object> { _instanceId });
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
