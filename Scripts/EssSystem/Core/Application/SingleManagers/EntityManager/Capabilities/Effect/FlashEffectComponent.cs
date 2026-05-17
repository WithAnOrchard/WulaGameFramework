using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 受伤闪烁效果组件 —— 使用 <c>Sprites/Flash</c> shader 实现白色轮廓闪烁。
    /// <para>
    /// 原理：shader 内 <c>_FlashAmount</c> 控制 rgb 向 <c>_FlashColor</c> 的插值比例，
    /// alpha 不变 → 不透明像素变白，透明像素仍透明，保留形状。
    /// 使用 <see cref="MaterialPropertyBlock"/> 写属性，避免材质实例化。
    /// </para>
    /// </summary>
    public class FlashEffectComponent : IFlashEffect, ITickableCapability
    {
        private SpriteRenderer[] _renderers;
        private Material[] _originalMaterials;
        private readonly float _flashDuration;
        private readonly Color _flashColor;

        private MaterialPropertyBlock _mpb;
        private float _flashTimer;
        private bool _isFlashing;

        private static Material _sharedFlashMaterial;
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId  = Shader.PropertyToID("_FlashColor");

        /// <summary>
        /// 构造：传入任意一个 <see cref="SpriteRenderer"/>，自动搜索其 transform 下所有子 SpriteRenderer。
        /// </summary>
        public FlashEffectComponent(SpriteRenderer renderer, float flashDuration = 0.15f, Color? flashColor = null)
        {
            _flashDuration = flashDuration;
            _flashColor = flashColor ?? Color.white;
            if (renderer == null) return;
            _renderers = renderer.transform.GetComponentsInChildren<SpriteRenderer>();
        }

        /// <summary>
        /// 构造（Transform 重载）：从 <paramref name="root"/> 搜索所有子 SpriteRenderer。
        /// 框架 auto-attach 时使用此版本，传 host.transform 即可覆盖整个 entity 层级。
        /// </summary>
        public FlashEffectComponent(Transform root, float flashDuration = 0.15f, Color? flashColor = null)
        {
            _flashDuration = flashDuration;
            _flashColor = flashColor ?? Color.white;
            if (root == null) return;
            _renderers = root.GetComponentsInChildren<SpriteRenderer>();
        }

        public void OnAttach(Entity owner)
        {
            if (_renderers == null || _renderers.Length == 0) return;
            EnsureSharedMaterial();
            if (_sharedFlashMaterial == null) return; // shader 缺失，静默降级

            // 备份原始材质，替换为 flash 材质
            _originalMaterials = new Material[_renderers.Length];
            _mpb = new MaterialPropertyBlock();
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _originalMaterials[i] = _renderers[i].sharedMaterial;
                _renderers[i].sharedMaterial = _sharedFlashMaterial;
                // 先 Get 再 Set：SpriteRenderer 内部通过 MPB 设置 [PerRendererData] _MainTex，
                // 直接 Set 会丢失精灵贴图引用 → 显示全白。
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetFloat(FlashAmountId, 0f);
                _mpb.SetColor(FlashColorId, _flashColor);
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }

        public void OnDetach(Entity owner)
        {
            // 还原原始材质
            if (_renderers == null || _originalMaterials == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (_originalMaterials[i] != null)
                    _renderers[i].sharedMaterial = _originalMaterials[i];
                _renderers[i].SetPropertyBlock(null);
            }
        }

        public void OnFlash()
        {
            if (_renderers == null || _mpb == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetFloat(FlashAmountId, 1f);
                _renderers[i].SetPropertyBlock(_mpb);
            }
            _flashTimer = _flashDuration;
            _isFlashing = true;
        }

        public void Tick(float deltaTime)
        {
            if (!_isFlashing) return;
            _flashTimer -= deltaTime;
            if (_flashTimer > 0f) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetFloat(FlashAmountId, 0f);
                _renderers[i].SetPropertyBlock(_mpb);
            }
            _isFlashing = false;
        }

        // ─── 静态辅助 ─────────────────────────────────────────────
        private static void EnsureSharedMaterial()
        {
            if (_sharedFlashMaterial != null) return;
            var shader = Shader.Find("Sprites/Flash");
            if (shader == null)
            {
                Debug.LogWarning("[FlashEffectComponent] Sprites/Flash shader 未找到，闪烁效果将不可用");
                return;
            }
            _sharedFlashMaterial = new Material(shader) { name = "SpriteFlash (shared)" };
        }
    }
}
