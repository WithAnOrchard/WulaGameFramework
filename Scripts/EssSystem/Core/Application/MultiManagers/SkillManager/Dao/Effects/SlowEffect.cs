using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class SlowEffect : ISkillEffect
    {
        public string BuffId = "slow";
        public float Multiplier = 0.5f;
        public float Duration = 3f;
        public bool ApplyToSelf;

        public SlowEffect() { }

        public SlowEffect(string buffId, float multiplier, float duration, bool applyToSelf = false)
        {
            BuffId = buffId;
            Multiplier = multiplier;
            Duration = duration;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            if (!SkillEntityProxy.TryGetSpeedMultiplier(targetId, out var original)) return;
            SkillEntityProxy.SetSpeedMultiplier(targetId, original * Mathf.Max(0f, Multiplier));
            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Duration,
                OnExpire = _ => SkillEntityProxy.SetSpeedMultiplier(targetId, original),
            });
        }
    }

    public class FreezeEffect : ISkillEffect
    {
        public string BuffId = "freeze";
        public float Duration = 2f;
        public float Damage = 8f;
        public float DamagePerLevel;
        public string DamageType = "frost";

        public FreezeEffect() { }

        public FreezeEffect(string buffId, float duration, float damage,
            float damagePerLevel = 0f, string damageType = "frost")
        {
            BuffId = string.IsNullOrEmpty(buffId) ? "freeze" : buffId;
            Duration = duration;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            if (SkillEntityProxy.IsDead(targetId)) return;

            var totalDamage = Damage + DamagePerLevel * (ctx.Level - 1);
            if (totalDamage > 0f)
                SkillEntityProxy.Damage(targetId, totalDamage, ctx.CasterId, DamageType, ctx.Position, true);

            var hasSpeed = SkillEntityProxy.TryGetSpeedMultiplier(targetId, out var originalSpeed);
            if (hasSpeed) SkillEntityProxy.SetSpeedMultiplier(targetId, 0f);

            var pushedControl = SkillEntityProxy.PushControl(targetId, "Stun");
            var root = SkillEntityProxy.Root(targetId);
            var visual = root != null ? root.gameObject.AddComponent<FreezeVisualRunner>() : null;
            visual?.Initialize(root, Mathf.Max(0.05f, Duration));

            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Mathf.Max(0.05f, Duration),
                OnExpire = _ =>
                {
                    if (hasSpeed) SkillEntityProxy.SetSpeedMultiplier(targetId, originalSpeed);
                    if (pushedControl) SkillEntityProxy.PopControl(targetId, "Stun");
                    if (visual != null) visual.Release();
                },
            });
        }

        private sealed class FreezeVisualRunner : MonoBehaviour
        {
            private SpriteRenderer[] _renderers;
            private Color[] _colors;
            private GameObject _iceRoot;
            private float _remaining;
            private float _duration;
            private bool _released;
            private static Sprite _iceShardSprite;
            private static Material _frostParticleMaterial;

            public void Initialize(Transform root, float duration)
            {
                _remaining = duration;
                _duration = duration;
                _renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                _colors = new Color[_renderers.Length];
                var freeze = new Color(0.55f, 0.92f, 1f, 1f);
                for (var i = 0; i < _renderers.Length; i++)
                {
                    var sr = _renderers[i];
                    if (sr == null) continue;
                    _colors[i] = sr.color;
                    sr.color = Color.Lerp(sr.color, freeze, 0.35f);
                }

                BuildIceOverlay(root);
            }

            private void Update()
            {
                _remaining -= Time.deltaTime;
                PulseIceAlpha();
                if (_remaining > 0f) return;
                Release();
            }

            private void OnDestroy()
            {
                Restore();
            }

            public void Release()
            {
                if (_released) return;
                _released = true;
                Restore();
                if (_iceRoot != null) Destroy(_iceRoot);
                Destroy(this);
            }

            private void Restore()
            {
                if (_renderers == null || _colors == null) return;
                for (var i = 0; i < _renderers.Length && i < _colors.Length; i++)
                {
                    if (_renderers[i] != null)
                        _renderers[i].color = _colors[i];
                }
                _renderers = null;
                _colors = null;
            }

            private void BuildIceOverlay(Transform root)
            {
                if (_renderers == null || _renderers.Length == 0) return;

                var bounds = ResolveBounds();
                if (bounds.size.sqrMagnitude <= 0.0001f) return;

                _iceRoot = new GameObject("FreezeIceOverlay");
                _iceRoot.transform.SetParent(root, false);
                _iceRoot.transform.position = bounds.center;

                var maxSorting = ResolveMaxSortingOrder();
                var icePrisonSprite = ResolveSprite("Common/Effects/SkillStatus/freeze_ice_prison");
                if (icePrisonSprite != null)
                {
                    BuildIcePrisonSprite(root, bounds, icePrisonSprite, maxSorting);
                    BuildFrostParticles(maxSorting + 20);
                    return;
                }

                var sprite = IceShardSprite();
                var points = new[]
                {
                    new Vector2(-0.42f, 0.32f), new Vector2(0.36f, 0.38f),
                    new Vector2(-0.18f, 0.10f), new Vector2(0.18f, 0.08f),
                    new Vector2(-0.35f, -0.18f), new Vector2(0.34f, -0.16f),
                    new Vector2(-0.10f, -0.38f), new Vector2(0.10f, -0.42f),
                };

                var width = Mathf.Max(0.2f, bounds.size.x);
                var height = Mathf.Max(0.35f, bounds.size.y);
                for (var i = 0; i < points.Length; i++)
                {
                    var shard = new GameObject($"IceChunk_{i}");
                    shard.transform.SetParent(_iceRoot.transform, false);
                    shard.transform.localPosition = new Vector3(points[i].x * width, points[i].y * height, -0.02f);
                    shard.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + i * 23f);
                    shard.transform.localScale = Vector3.one * (0.38f + (i % 3) * 0.08f);

                    var sr = shard.AddComponent<SpriteRenderer>();
                    sr.sprite = sprite;
                    sr.color = new Color(0.72f, 0.96f, 1f, 0.86f);
                    sr.sortingOrder = maxSorting + 8 + i;
                }

                BuildFrostParticles(maxSorting + 20);
            }

            private void BuildIcePrisonSprite(Transform root, Bounds bounds, Sprite sprite, int maxSorting)
            {
                var prison = new GameObject("IcePrisonSprite");
                prison.transform.SetParent(_iceRoot.transform, false);
                prison.transform.position = bounds.center + Vector3.up * (bounds.size.y * 0.04f);

                var spriteSize = sprite.bounds.size;
                var desiredWidth = Mathf.Max(0.35f, bounds.size.x * 1.55f);
                var desiredHeight = Mathf.Max(0.55f, bounds.size.y * 1.35f);
                var parentScale = root != null ? root.lossyScale : Vector3.one;
                var scaleX = spriteSize.x > 0.001f ? desiredWidth / spriteSize.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)) : 1f;
                var scaleY = spriteSize.y > 0.001f ? desiredHeight / spriteSize.y / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)) : 1f;
                prison.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                var sr = prison.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.82f, 0.97f, 1f, 0.88f);
                sr.sortingOrder = maxSorting + 14;

                var shine = new GameObject("IcePrisonShine");
                shine.transform.SetParent(prison.transform, false);
                shine.transform.localPosition = new Vector3(-0.06f, 0.05f, -0.01f);
                shine.transform.localScale = new Vector3(1.04f, 1.04f, 1f);
                var shineRenderer = shine.AddComponent<SpriteRenderer>();
                shineRenderer.sprite = sprite;
                shineRenderer.color = new Color(1f, 1f, 1f, 0.18f);
                shineRenderer.sortingOrder = maxSorting + 15;
            }

            private Bounds ResolveBounds()
            {
                var hasBounds = false;
                var bounds = new Bounds(transform.position, Vector3.zero);
                for (var i = 0; i < _renderers.Length; i++)
                {
                    var sr = _renderers[i];
                    if (sr == null || !sr.enabled || sr.sprite == null) continue;
                    if (!hasBounds)
                    {
                        bounds = sr.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(sr.bounds);
                    }
                }
                return hasBounds ? bounds : new Bounds(transform.position, Vector3.one);
            }

            private int ResolveMaxSortingOrder()
            {
                var sorting = 0;
                for (var i = 0; i < _renderers.Length; i++)
                {
                    var sr = _renderers[i];
                    if (sr != null) sorting = Mathf.Max(sorting, sr.sortingOrder);
                }
                return sorting;
            }

            private void BuildFrostParticles(int sortingOrder)
            {
                if (_iceRoot == null) return;

                var particles = new GameObject("FrostParticles");
                particles.transform.SetParent(_iceRoot.transform, false);
                var ps = particles.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.duration = Mathf.Max(0.1f, _duration);
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.95f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.20f, 0.62f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.065f, 0.16f);
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.95f, 1f, 1f, 1f),
                    new Color(0.35f, 0.86f, 1f, 0.72f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = ps.emission;
                emission.rateOverTime = 54f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.72f;

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.30f, 0.82f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.material = FrostParticleMaterial();
                renderer.sortingOrder = sortingOrder;
                ps.Play();
            }

            private void PulseIceAlpha()
            {
                if (_iceRoot == null) return;
                var alpha = 0.72f + Mathf.Sin(Time.time * 18f) * 0.12f;
                var scale = 1f + Mathf.Sin(Time.time * 8.5f) * 0.025f;
                _iceRoot.transform.localScale = new Vector3(scale, scale, 1f);
                var shards = _iceRoot.GetComponentsInChildren<SpriteRenderer>(true);
                for (var i = 0; i < shards.Length; i++)
                {
                    var sr = shards[i];
                    if (sr == null) continue;
                    var c = sr.color;
                    c.a = Mathf.Clamp01(alpha + (i % 2) * 0.08f);
                    sr.color = c;
                }
            }

            private static Sprite ResolveSprite(string spriteId)
            {
                if (string.IsNullOrEmpty(spriteId) || !EventProcessor.HasInstance) return null;
                var result = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite", new System.Collections.Generic.List<object> { spriteId });
                return ResultCode.IsOk(result) && result.Count > 1 ? result[1] as Sprite : null;
            }

            private static Sprite IceShardSprite()
            {
                if (_iceShardSprite != null) return _iceShardSprite;

                var tex = new Texture2D(9, 9, TextureFormat.RGBA32, false);
                var clear = new Color(0f, 0f, 0f, 0f);
                for (var y = 0; y < tex.height; y++)
                for (var x = 0; x < tex.width; x++)
                    tex.SetPixel(x, y, clear);

                var fill = new Color(0.42f, 0.92f, 1f, 0.9f);
                var edge = new Color(0.88f, 1f, 1f, 1f);
                var pixels = new[]
                {
                    new Vector2Int(4, 0), new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1),
                    new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(5, 2), new Vector2Int(6, 2),
                    new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3), new Vector2Int(5, 3), new Vector2Int(6, 3),
                    new Vector2Int(1, 4), new Vector2Int(2, 4), new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4), new Vector2Int(7, 4),
                    new Vector2Int(2, 5), new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5),
                    new Vector2Int(3, 6), new Vector2Int(4, 6), new Vector2Int(5, 6),
                    new Vector2Int(4, 7),
                };

                for (var i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    tex.SetPixel(p.x, p.y, i % 3 == 0 ? edge : fill);
                }
                tex.Apply();
                _iceShardSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 16f);
                return _iceShardSprite;
            }

            private static Material FrostParticleMaterial()
            {
                if (_frostParticleMaterial != null) return _frostParticleMaterial;
                _frostParticleMaterial = new Material(Shader.Find("Sprites/Default"));
                return _frostParticleMaterial;
            }
        }
    }
}
