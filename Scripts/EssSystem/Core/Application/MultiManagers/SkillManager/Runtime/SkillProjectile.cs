using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.SkillManager;
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
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public bool IgnoreStaticTargets;

        private float _aliveTime;
        private System.Collections.Generic.HashSet<string> _hit;
        private static int _impactSeq;
        private static readonly Collider[] _buffer = new Collider[16];
        private static readonly Collider2D[] _buffer2D = new Collider2D[16];
        private static readonly ContactFilter2D _contactFilter = ContactFilter2D.noFilter;
        private static Sprite _fallbackSprite;

        private void Start()
        {
            EnsureVisual();
        }

        private void Update()
        {
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
            SkillEntityProxy.Damage(targetId, Damage, CasterId, DamageType, transform.position);
            SpawnImpact();

            if (!Pierce) { Destroy(gameObject); return true; }
            return false;
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
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, VisualScale);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrder + 1;
            Destroy(go, 0.18f);
        }

        private bool SpawnImpactCharacter()
        {
            if (string.IsNullOrEmpty(ImpactCharacterConfigId) || !EventProcessor.HasInstance) return false;

            var instanceId = $"Impact_{DamageType}_{++_impactSeq}";
            var result = EventProcessor.Instance.TriggerEventMethod(
                "CreateCharacter",
                new System.Collections.Generic.List<object> { ImpactCharacterConfigId, instanceId, null, transform.position });

            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not Transform root) return false;

            EventProcessor.Instance.TriggerEventMethod(
                "SetCharacterScale",
                new System.Collections.Generic.List<object> { instanceId, Vector3.one * Mathf.Max(0.01f, ImpactScale) });

            if (!string.IsNullOrEmpty(ImpactActionName))
            {
                EventProcessor.Instance.TriggerEventMethod(
                    "PlayCharacterAction",
                    new System.Collections.Generic.List<object> { instanceId, ImpactActionName });
            }

            var lifetime = root.gameObject.AddComponent<ImpactCharacterLifetime>();
            lifetime.Initialize(instanceId, Mathf.Max(0.01f, ImpactLifetime));
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
