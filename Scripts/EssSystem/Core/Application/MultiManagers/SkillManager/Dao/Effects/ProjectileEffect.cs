using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class ProjectileEffect : ISkillEffect, ISkillCastStartEffect
    {
        public float Speed = 12f;
        public float Damage = 8f;
        public float DamagePerLevel;
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
        public string CastCharacterConfigId;
        public string CastActionName = "Special";
        public float CastScale = 1f;
        public float CastLifetime = 0.25f;
        public float CastForwardOffset = 0.2f;
        public float CastHeightOffset = 0.35f;
        public string CastSfxId;
        public float CastSfxVolume = 1f;
        public string CastFlashPartId;
        public float CastFlashDuration = 0.16f;
        public Color CastFlashColor = Color.white;
        public List<ISkillEffect> HitEffects = new();
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public float ForwardOffset = 0.7f;
        public float HeightOffset = 0.7f;
        public bool IgnoreStaticTargets;

        public ProjectileEffect() { }
        public ProjectileEffect(float speed, float damage, float damagePerLevel = 0f,
            string damageType = "projectile", float radius = 0.3f, float maxLifetime = 4f, bool pierce = false,
            string spriteId = null, string impactSpriteId = null, float visualScale = 1f,
            int sortingOrder = 260, float forwardOffset = 0.7f, float heightOffset = 0.7f,
            string impactCharacterConfigId = null, string impactActionName = "Special",
            float impactScale = 1f, float impactLifetime = 0.35f,
            float visualRotationOffsetDegrees = 0f, bool ignoreStaticTargets = false,
            float areaDamageRadius = 0f, float areaDamageMultiplier = 1f,
            string impactSfxId = null, float impactSfxVolume = 1f,
            bool suppressTargetHitSfx = false,
            string castCharacterConfigId = null, string castActionName = "Special",
            float castScale = 1f, float castLifetime = 0.25f,
            float castForwardOffset = 0.2f, float castHeightOffset = 0.35f,
            string castSfxId = null, float castSfxVolume = 1f,
            string castFlashPartId = null, float castFlashDuration = 0.16f,
            Color? castFlashColor = null,
            List<ISkillEffect> hitEffects = null)
        {
            Speed = speed;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
            Radius = radius;
            MaxLifetime = maxLifetime;
            Pierce = pierce;
            SpriteId = spriteId;
            ImpactSpriteId = impactSpriteId;
            ImpactCharacterConfigId = impactCharacterConfigId;
            ImpactActionName = impactActionName;
            ImpactScale = impactScale;
            ImpactLifetime = impactLifetime;
            AreaDamageRadius = areaDamageRadius;
            AreaDamageMultiplier = areaDamageMultiplier;
            ImpactSfxId = impactSfxId;
            ImpactSfxVolume = impactSfxVolume;
            SuppressTargetHitSfx = suppressTargetHitSfx;
            CastCharacterConfigId = castCharacterConfigId;
            CastActionName = castActionName;
            CastScale = castScale;
            CastLifetime = castLifetime;
            CastForwardOffset = castForwardOffset;
            CastHeightOffset = castHeightOffset;
            CastSfxId = castSfxId;
            CastSfxVolume = castSfxVolume;
            CastFlashPartId = castFlashPartId;
            CastFlashDuration = castFlashDuration;
            CastFlashColor = castFlashColor ?? Color.white;
            HitEffects = hitEffects ?? new List<ISkillEffect>();
            VisualScale = visualScale;
            VisualRotationOffsetDegrees = visualRotationOffsetDegrees;
            SortingOrder = sortingOrder;
            ForwardOffset = forwardOffset;
            HeightOffset = heightOffset;
            IgnoreStaticTargets = ignoreStaticTargets;
        }

        public void OnCastStart(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;

            if (!string.IsNullOrEmpty(CastCharacterConfigId))
            {
                var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f ? Mathf.Sign(ctx.Direction.x) : (root.localScale.x >= 0f ? 1f : -1f);
                var dir = ctx.Direction.sqrMagnitude > 0.001f ? ctx.Direction.normalized : new Vector3(dirX, 0f, 0f);
                var castPosition = root.position + dir * CastForwardOffset + Vector3.up * CastHeightOffset;
                SkillProjectile.SpawnCharacterEffect(CastCharacterConfigId, CastActionName, castPosition,
                    CastScale, CastLifetime);
            }

            PlayCastPartAction(root, ctx);
            PlayCastSfx();
        }

        public virtual void Apply(SkillEffectContext ctx)
        {
            var root = SkillEntityProxy.Root(ctx?.CasterId);
            if (root == null) return;
            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f ? Mathf.Sign(ctx.Direction.x) : (root.localScale.x >= 0f ? 1f : -1f);
            var dir = ctx.Direction.sqrMagnitude > 0.001f ? ctx.Direction.normalized : new Vector3(dirX, 0f, 0f);

            var go = new GameObject($"Projectile_{ctx.Definition?.Id}");
            go.transform.position = root.position + dir * ForwardOffset + Vector3.up * HeightOffset;
            var p = go.AddComponent<SkillProjectile>();
            p.CasterId = ctx.CasterId;
            p.Velocity = dir * Speed;
            p.Damage = Damage + DamagePerLevel * (ctx.Level - 1);
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

        private void PlayCastSfx()
        {
            if (string.IsNullOrEmpty(CastSfxId) || !EssSystem.Core.Base.Event.EventProcessor.HasInstance) return;
            EssSystem.Core.Base.Event.EventProcessor.Instance.TriggerEventMethod(
                "PlaySFX",
                new System.Collections.Generic.List<object> { CastSfxId, Mathf.Max(0f, CastSfxVolume) });
        }

        private void PlayCastPartAction(Transform root, SkillEffectContext ctx)
        {
            if (string.IsNullOrEmpty(CastFlashPartId)) return;
            if (!TryPlayPartAction(ctx?.CasterId, "Attack", CastFlashPartId)) return;

            var renderers = ResolvePartRenderers(root, CastFlashPartId);
            if (renderers == null || renderers.Length == 0) return;
            var runner = root.gameObject.AddComponent<CastPartTintRunner>();
            runner.Initialize(renderers, CastFlashDuration, CastFlashColor);
        }

        private static bool TryPlayPartAction(string casterId, string actionName, string partId)
        {
            if (string.IsNullOrEmpty(casterId) || !EssSystem.Core.Base.Event.EventProcessor.HasInstance) return false;
            var result = EssSystem.Core.Base.Event.EventProcessor.Instance.TriggerEventMethod(
                "PlayCharacterAction",
                new System.Collections.Generic.List<object> { casterId, actionName, partId });
            return EssSystem.Core.Base.Util.ResultCode.IsOk(result);
        }

        private static SpriteRenderer[] ResolvePartRenderers(Transform root, string partId)
        {
            if (root == null || string.IsNullOrEmpty(partId)) return null;

            var exact = root.Find(partId);
            if (exact != null)
            {
                var exactRenderers = exact.GetComponentsInChildren<SpriteRenderer>(true);
                if (exactRenderers != null && exactRenderers.Length > 0) return exactRenderers;
            }

            var all = root.GetComponentsInChildren<SpriteRenderer>(true);
            var matched = new System.Collections.Generic.List<SpriteRenderer>();
            for (var i = 0; i < all.Length; i++)
            {
                var sr = all[i];
                if (sr == null) continue;
                var tr = sr.transform;
                while (tr != null && tr != root)
                {
                    if (tr.name.IndexOf(partId, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matched.Add(sr);
                        break;
                    }
                    tr = tr.parent;
                }
            }
            return matched.Count > 0 ? matched.ToArray() : null;
        }

        private sealed class CastPartTintRunner : MonoBehaviour
        {
            private SpriteRenderer[] _renderers;
            private Color[] _originalColors;
            private Color _castColor;
            private float _duration;
            private float _elapsed;

            public void Initialize(SpriteRenderer[] renderers, float duration, Color castColor)
            {
                _renderers = renderers;
                _duration = Mathf.Max(0.03f, duration);
                _castColor = castColor;

                _originalColors = new Color[_renderers.Length];
                for (var i = 0; i < _renderers.Length; i++)
                {
                    var sr = _renderers[i];
                    if (sr == null) continue;
                    _originalColors[i] = sr.color;
                    sr.color = WithOriginalAlpha(_castColor, sr.color.a);
                }
            }

            private void Update()
            {
                _elapsed += Time.deltaTime;
                if (_elapsed < _duration) return;
                Restore();
                Destroy(this);
            }

            private void OnDestroy()
            {
                Restore();
            }

            private void Restore()
            {
                if (_renderers == null) return;
                for (var i = 0; i < _renderers.Length; i++)
                {
                    var sr = _renderers[i];
                    if (sr == null) continue;
                    if (_originalColors != null && i < _originalColors.Length)
                        sr.color = _originalColors[i];
                }
                _renderers = null;
            }

            private static Color WithOriginalAlpha(Color color, float alpha)
            {
                color.a = alpha;
                return color;
            }
        }
    }
}
