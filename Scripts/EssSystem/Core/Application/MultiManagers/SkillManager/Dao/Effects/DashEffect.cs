using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class DashEffect : ISkillEffect
    {
        public const string BUFF_DASH_INVUL = "dash_invulnerable";
        public float Speed;
        public float SpeedPerLevel;
        public bool KeepVerticalVelocity = true;
        public float VerticalKick;
        public float InvulnerableDuration;
        public float DashDuration = 0.26f;

        public DashEffect(float speed, float speedPerLevel = 0f, float invulDuration = 0f,
            bool keepVerticalVelocity = true, float verticalKick = 0f, float dashDuration = 0.26f)
        {
            Speed = speed;
            SpeedPerLevel = speedPerLevel;
            InvulnerableDuration = invulDuration;
            KeepVerticalVelocity = keepVerticalVelocity;
            VerticalKick = verticalKick;
            DashDuration = dashDuration;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var rb = root.GetComponent<Rigidbody>();
            var rb2D = root.GetComponent<Rigidbody2D>();

            var visual = root.Find("Visual");
            var facingScale = visual != null ? visual.localScale.x : root.localScale.x;
            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f
                ? Mathf.Sign(ctx.Direction.x)
                : (facingScale >= 0f ? 1f : -1f);
            var speed = Speed + SpeedPerLevel * (ctx.Level - 1);
            var duration = Mathf.Clamp(DashDuration, 0.08f, 0.55f);
            if (rb != null)
            {
                var vy = KeepVerticalVelocity ? rb.linearVelocity.y : VerticalKick;
                rb.linearVelocity = new Vector3(dirX * speed, vy, 0f);
            }
            if (rb2D != null)
            {
                var vy = KeepVerticalVelocity ? rb2D.linearVelocity.y : VerticalKick;
                rb2D.linearVelocity = new Vector2(dirX * speed, vy);
            }
            var runtime = root.GetComponent<DashRuntime>() ?? root.gameObject.AddComponent<DashRuntime>();
            runtime.Begin(ctx.CasterId, dirX, speed, duration, KeepVerticalVelocity, VerticalKick);

            if (visual != null)
            {
                var s = visual.localScale;
                s.x = dirX > 0f ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
                visual.localScale = s;
            }

            if (InvulnerableDuration > 0f && SkillService.HasInstance &&
                SkillEntityProxy.TryGetDamageReduction(ctx.CasterId, out var original))
            {
                SkillEntityProxy.SetDamageReduction(ctx.CasterId, 1f);
                SkillService.Instance.ApplyBuff(ctx.CasterId, new BuffInstance
                {
                    BuffId = BUFF_DASH_INVUL,
                    SourceId = ctx.CasterId,
                    Duration = InvulnerableDuration,
                    OnExpire = _ => SkillEntityProxy.SetDamageReduction(ctx.CasterId, original),
                });
            }
        }

        private sealed class DashRuntime : MonoBehaviour
        {
            private const float AfterimageInterval = 0.028f;
            private const float AfterimageLifetime = 0.34f;

            private string _entityId;
            private Rigidbody _rb;
            private Rigidbody2D _rb2D;
            private float _dirX;
            private float _speed;
            private float _duration;
            private float _elapsed;
            private float _afterimageTimer;
            private bool _keepVerticalVelocity;
            private float _verticalKick;
            private SpriteRenderer[] _renderers;

            public void Begin(string entityId, float dirX, float speed, float duration,
                bool keepVerticalVelocity, float verticalKick)
            {
                _entityId = entityId;
                _rb = GetComponent<Rigidbody>();
                _rb2D = GetComponent<Rigidbody2D>();
                _dirX = dirX >= 0f ? 1f : -1f;
                _speed = Mathf.Max(0f, speed);
                _duration = Mathf.Max(0.01f, duration);
                _elapsed = 0f;
                _afterimageTimer = 0f;
                _keepVerticalVelocity = keepVerticalVelocity;
                _verticalKick = verticalKick;
                _renderers = ResolveCharacterRenderers();
                SpawnAfterimage();
                enabled = true;
            }

            private void Update()
            {
                var dt = Time.deltaTime;
                _elapsed += dt;
                _afterimageTimer -= dt;

                if (_afterimageTimer <= 0f)
                {
                    SpawnAfterimage();
                    _afterimageTimer = AfterimageInterval;
                }

                var delta = new Vector3(_dirX * _speed * dt, 0f, 0f);
                var nextPosition = transform.position + delta;
                if (_rb != null)
                {
                    var vy = _keepVerticalVelocity ? _rb.linearVelocity.y : _verticalKick;
                    _rb.linearVelocity = new Vector3(_dirX * _speed, vy, 0f);
                    _rb.position = nextPosition;
                    transform.position = nextPosition;
                }
                else if (_rb2D != null)
                {
                    var vy = _keepVerticalVelocity ? _rb2D.linearVelocity.y : _verticalKick;
                    _rb2D.linearVelocity = new Vector2(_dirX * _speed, vy);
                    _rb2D.position = nextPosition;
                    transform.position = nextPosition;
                }
                else
                {
                    transform.position = nextPosition;
                }

                SkillEntityProxy.SetPosition(_entityId, nextPosition);

                if (_elapsed >= _duration)
                {
                    if (_rb != null) _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                    if (_rb2D != null) _rb2D.linearVelocity = new Vector2(0f, _rb2D.linearVelocity.y);
                    enabled = false;
                }
            }

            private void SpawnAfterimage()
            {
                if (_renderers == null || _renderers.Length == 0) return;

                var root = new GameObject("DashAfterimage");
                root.transform.position = transform.position;
                root.transform.rotation = transform.rotation;
                root.transform.localScale = Vector3.one;

                for (var i = 0; i < _renderers.Length; i++)
                {
                    var source = _renderers[i];
                    if (!IsValidAfterimageSource(source)) continue;

                    var ghost = new GameObject(source.name + "_Afterimage");
                    ghost.transform.SetParent(root.transform, false);
                    ghost.transform.position = source.transform.position;
                    ghost.transform.rotation = source.transform.rotation;
                    ghost.transform.localScale = source.transform.lossyScale;

                    var sr = ghost.AddComponent<SpriteRenderer>();
                    sr.sprite = source.sprite;
                    sr.flipX = source.flipX;
                    sr.flipY = source.flipY;
                    sr.sortingLayerID = source.sortingLayerID;
                    sr.sortingOrder = source.sortingOrder + 1;
                    sr.color = new Color(0.38f, 0.92f, 1f, 0.64f);
                }

                var fade = root.AddComponent<DashAfterimageFade>();
                fade.Begin(AfterimageLifetime);
            }

            private SpriteRenderer[] ResolveCharacterRenderers()
            {
                var visual = transform.Find("Visual");
                var sourceRoot = visual != null ? visual : transform;
                var all = sourceRoot.GetComponentsInChildren<SpriteRenderer>(true);
                var result = new System.Collections.Generic.List<SpriteRenderer>(all.Length);
                for (var i = 0; i < all.Length; i++)
                {
                    if (IsValidAfterimageSource(all[i]))
                        result.Add(all[i]);
                }
                return result.ToArray();
            }

            private static bool IsValidAfterimageSource(SpriteRenderer source)
            {
                if (source == null || source.sprite == null || !source.enabled) return false;
                var tr = source.transform;
                while (tr != null)
                {
                    var n = tr.name;
                    if (n.IndexOf("Range", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Attack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Indicator", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Gizmo", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Collider", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        return false;
                    tr = tr.parent;
                }
                return true;
            }
        }

        private sealed class DashAfterimageFade : MonoBehaviour
        {
            private float _duration;
            private float _elapsed;
            private SpriteRenderer[] _renderers;
            private Color[] _startColors;

            public void Begin(float duration)
            {
                _duration = Mathf.Max(0.01f, duration);
                _elapsed = 0f;
                _renderers = GetComponentsInChildren<SpriteRenderer>(true);
                _startColors = new Color[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++)
                    _startColors[i] = _renderers[i] != null ? _renderers[i].color : Color.clear;
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(_elapsed / _duration);
                var alpha = 1f - t;
                for (var i = 0; i < _renderers.Length; i++)
                {
                    var sr = _renderers[i];
                    if (sr == null) continue;
                    var c = _startColors[i];
                    c.a *= alpha;
                    sr.color = c;
                }
                if (_elapsed >= _duration)
                    Destroy(gameObject);
            }
        }
    }
}
