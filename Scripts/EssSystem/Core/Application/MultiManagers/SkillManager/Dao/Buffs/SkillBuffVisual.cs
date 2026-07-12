using EssSystem.Core.Application.MultiManagers.SkillManager;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs
{
    /// <summary>
    /// Generic runtime visual attached to an entity while a buff is active.
    /// Effects create it with a profile and dispose the returned handle on buff expiry.
    /// </summary>
    public static class SkillBuffVisual
    {
        public static SkillBuffVisualHandle Attach(string entityId, SkillBuffVisualProfile profile)
        {
            if (string.IsNullOrEmpty(entityId)) return null;
            profile ??= SkillBuffVisualProfile.Default();

            var root = SkillEntityProxy.Root(entityId);
            if (root == null) return null;

            var objectName = BuildObjectName(profile.VisualId);
            var existing = root.Find(objectName);
            if (existing != null) Object.Destroy(existing.gameObject);

            var go = new GameObject(objectName);
            go.transform.SetParent(root, false);

            var controller = go.AddComponent<SkillBuffVisualController>();
            controller.Initialize(root, profile);
            return new SkillBuffVisualHandle(controller);
        }

        private static string BuildObjectName(string visualId)
        {
            return $"__SkillBuffVisual_{(string.IsNullOrEmpty(visualId) ? "default" : visualId)}";
        }
    }

    public sealed class SkillBuffVisualHandle
    {
        private SkillBuffVisualController _controller;

        internal SkillBuffVisualHandle(SkillBuffVisualController controller)
        {
            _controller = controller;
        }

        public void StopAndDestroy()
        {
            if (_controller == null) return;
            _controller.StopAndDestroy();
            _controller = null;
        }
    }

    public sealed class SkillBuffVisualProfile
    {
        public string VisualId = "buff";
        public float Duration = 1f;
        public Vector3 LocalOffset = Vector3.zero;

        public Color PrimaryColor = new(1f, 0.15f, 0.18f, 1f);
        public Color SecondaryColor = new(1f, 0.42f, 0.44f, 0.65f);
        public Color HighlightColor = new(1f, 0.72f, 0.72f, 0.45f);

        public bool UseParticles = true;
        public bool UsePulseRing = true;
        public float ParticleRate = 16f;
        public float ParticleLifetimeMin = 0.45f;
        public float ParticleLifetimeMax = 0.95f;
        public float ParticleSpeedMin = 0.08f;
        public float ParticleSpeedMax = 0.34f;
        public float ParticleSizeMin = 0.045f;
        public float ParticleSizeMax = 0.115f;
        public float ParticleHorizontalVelocity = 0.08f;
        public float ParticleVerticalVelocityMin = 0.18f;
        public float ParticleVerticalVelocityMax = 0.55f;

        public float WidthScale = 1f;
        public float HeightScale = 1f;
        public float MinLocalWidth = 0.55f;
        public float MaxLocalWidth = 1.45f;
        public float MinLocalHeight = 0.75f;
        public float MaxLocalHeight = 1.9f;

        public float RingRadiusScale = 0.58f;
        public float RingHeightScale = 0.34f;
        public float RingWidth = 0.035f;
        public float PulseSpeed = 1.65f;
        public float PulseScaleMin = 0.78f;
        public float PulseScaleMax = 1.08f;
        public float PulseAlphaMin = 0.16f;
        public float PulseAlphaMax = 0.38f;

        public int SortingOrder = 430;
        public float StopDestroyDelay = 0.45f;

        public static SkillBuffVisualProfile Default()
        {
            return new SkillBuffVisualProfile();
        }

        public static SkillBuffVisualProfile Bloodthirst(float duration)
        {
            return new SkillBuffVisualProfile
            {
                VisualId = "bloodthirst",
                Duration = duration,
                PrimaryColor = new Color(1f, 0.06f, 0.12f, 0.92f),
                SecondaryColor = new Color(1f, 0.38f, 0.42f, 0.55f),
                HighlightColor = new Color(1f, 0.55f, 0.55f, 0.45f),
                ParticleRate = 16f,
                ParticleLifetimeMin = 0.45f,
                ParticleLifetimeMax = 0.95f,
                ParticleSpeedMin = 0.08f,
                ParticleSpeedMax = 0.34f,
                ParticleSizeMin = 0.045f,
                ParticleSizeMax = 0.115f,
                RingRadiusScale = 0.58f,
                RingHeightScale = 0.34f,
                SortingOrder = 430,
            };
        }
    }

    internal sealed class SkillBuffVisualController : MonoBehaviour
    {
        private static Material _sharedMaterial;

        private ParticleSystem _particles;
        private LineRenderer _pulse;
        private SkillBuffVisualProfile _profile;
        private float _age;
        private float _baseRadius = 0.65f;

        public void Initialize(Transform root, SkillBuffVisualProfile profile)
        {
            _profile = profile ?? SkillBuffVisualProfile.Default();
            _profile.Duration = Mathf.Max(0.1f, _profile.Duration);

            var bounds = ResolveVisualBounds(root);
            var worldFoot = new Vector3(root.position.x, bounds.min.y, root.position.z);
            var localFoot = root.InverseTransformPoint(worldFoot);
            transform.localPosition = localFoot + _profile.LocalOffset;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            var scale = root.lossyScale;
            var sx = Mathf.Max(0.01f, Mathf.Abs(scale.x));
            var sy = Mathf.Max(0.01f, Mathf.Abs(scale.y));
            var localWidth = Mathf.Clamp(bounds.size.x / sx * _profile.WidthScale,
                _profile.MinLocalWidth, _profile.MaxLocalWidth);
            var localHeight = Mathf.Clamp(bounds.size.y / sy * _profile.HeightScale,
                _profile.MinLocalHeight, _profile.MaxLocalHeight);
            _baseRadius = Mathf.Max(0.25f, localWidth * _profile.RingRadiusScale);

            if (_profile.UseParticles) BuildParticles(localWidth, localHeight);
            if (_profile.UsePulseRing) BuildPulseRing();
        }

        public void StopAndDestroy()
        {
            if (_particles != null) _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(gameObject, Mathf.Max(0f, _profile?.StopDestroyDelay ?? 0.35f));
        }

        private void Update()
        {
            if (_profile == null) return;
            _age += Time.deltaTime;

            if (_pulse != null)
            {
                var t = Mathf.PingPong(_age * _profile.PulseSpeed, 1f);
                var radius = _baseRadius * Mathf.Lerp(_profile.PulseScaleMin, _profile.PulseScaleMax, t);
                var alpha = Mathf.Lerp(_profile.PulseAlphaMin, _profile.PulseAlphaMax, 1f - t);
                _pulse.startColor = WithAlpha(_profile.PrimaryColor, alpha);
                _pulse.endColor = WithAlpha(_profile.HighlightColor, alpha * 0.45f);
                WriteEllipse(_pulse, radius, radius * _profile.RingHeightScale);
            }

            if (_age >= _profile.Duration) StopAndDestroy();
        }

        private void BuildParticles(float width, float height)
        {
            var go = new GameObject("BuffParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * (height * 0.5f);
            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.1f, _profile.Duration);
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(_profile.ParticleLifetimeMin, _profile.ParticleLifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_profile.ParticleSpeedMin, _profile.ParticleSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(_profile.ParticleSizeMin, _profile.ParticleSizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(_profile.PrimaryColor, _profile.SecondaryColor);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = _particles.emission;
            emission.rateOverTime = Mathf.Max(0f, _profile.ParticleRate);

            var shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, height, 0.05f);

            var velocity = _particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-_profile.ParticleHorizontalVelocity, _profile.ParticleHorizontalVelocity);
            velocity.y = new ParticleSystem.MinMaxCurve(_profile.ParticleVerticalVelocityMin, _profile.ParticleVerticalVelocityMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var color = _particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(_profile));

            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = SharedMaterial;
            renderer.sortingOrder = _profile.SortingOrder;
            _particles.Play();
        }

        private void BuildPulseRing()
        {
            var go = new GameObject("BuffPulse");
            go.transform.SetParent(transform, false);
            _pulse = go.AddComponent<LineRenderer>();
            _pulse.useWorldSpace = false;
            _pulse.loop = true;
            _pulse.positionCount = 40;
            _pulse.widthMultiplier = _profile.RingWidth;
            _pulse.material = SharedMaterial;
            _pulse.sortingOrder = _profile.SortingOrder - 1;
            WriteEllipse(_pulse, _baseRadius, _baseRadius * _profile.RingHeightScale);
        }

        private static Bounds ResolveVisualBounds(Transform root)
        {
            var bounds = new Bounds(root.position + Vector3.up * 0.55f, Vector3.one);
            var hasBounds = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || renderer.GetComponentInParent<SkillBuffVisualController>() != null) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private static void WriteEllipse(LineRenderer line, float radiusX, float radiusY)
        {
            if (line == null) return;
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f));
            }
        }

        private static Gradient BuildFadeGradient(SkillBuffVisualProfile profile)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(WithoutAlpha(profile.PrimaryColor), 0f),
                    new GradientColorKey(WithoutAlpha(profile.SecondaryColor), 0.55f),
                    new GradientColorKey(WithoutAlpha(profile.HighlightColor), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(Mathf.Clamp01(profile.PrimaryColor.a), 0.18f),
                    new GradientAlphaKey(0f, 1f),
                });
            return gradient;
        }

        private static Color WithoutAlpha(Color color) => new(color.r, color.g, color.b, 1f);

        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        private static Material SharedMaterial
        {
            get
            {
                if (_sharedMaterial != null) return _sharedMaterial;
                _sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                return _sharedMaterial;
            }
        }
    }
}
