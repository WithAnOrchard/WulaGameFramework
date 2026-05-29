using UnityEngine;

namespace EssSystem.Core.Platform.Windows
{
    /// <summary>
    /// 挂在主相机上，<see cref="OnRenderImage"/> 时把渲染结果送进
    /// <see cref="Material"/>（<c>Custom/MakeTransparent</c>）做后处理：
    /// 把与 <see cref="ColorKey"/> 接近的像素 alpha 写为 0。
    /// <para>仅适用于 <b>Built-in 渲染管线</b>。URP 下需改用 Renderer Feature；HDRP 不可用。</para>
    /// <para>必须在 PlayerSettings 里关闭 <b>"Use DXGI Flip Model Swapchain for D3D11"</b>，
    /// 否则 backbuffer 不带 alpha。</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public class TransparentRenderBlit : MonoBehaviour
    {
        [Tooltip("等于此颜色（在容差内）的像素将被写为 alpha=0。建议与相机背景色一致。")]
        public Color ColorKey = new Color(0f, 1f, 0f, 1f);

        [Tooltip("颜色容差（0.01 ≈ 1/255×2.55）。值越大越宽松，越容易吃到边缘像素。")]
        [Range(0f, 0.5f)] public float Margin = 0.01f;

        private Material _material;

        private static readonly int s_KeyId    = Shader.PropertyToID("_TransparentColorKey");
        private static readonly int s_MarginId = Shader.PropertyToID("_TransparencyMargin");

        private void Awake() => EnsureMaterial();

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            EnsureMaterial();
            if (_material == null) { Graphics.Blit(src, dst); return; }
            _material.SetColor(s_KeyId, ColorKey);
            _material.SetFloat(s_MarginId, Margin);
            Graphics.Blit(src, dst, _material);
        }

        private void EnsureMaterial()
        {
            if (_material != null) return;
            var sh = Resources.Load<Shader>("Shaders/MakeTransparent");
            if (sh == null) sh = Shader.Find("Custom/MakeTransparent");
            if (sh == null)
            {
                Debug.LogError("[TransparentRenderBlit] 找不到 Shader 'Custom/MakeTransparent'。");
                return;
            }
            _material = new Material(sh) { hideFlags = HideFlags.DontSave };
        }
    }
}
