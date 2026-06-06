using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

namespace Demo.Cubic.Map
{
    /// <summary>
    /// Cubic 运行时后期处理 —— 程序化建 Global Volume + Bloom / Vignette / ColorAdjustments。
    /// <para>
    /// <b>为什么运行时建</b>：同 <see cref="CubicSceneDecor"/>，避开 .asset / .unity 二进制编辑。运行时建 VolumeProfile、Add overrides、设值，
    /// git diff 友好，调参在面板里改完直接 Play 看效果。
    /// </para>
    /// <para>
    /// <b>前置要求</b>：URP Asset (UniversalRenderPipelineAsset) 的 Renderer Feature 列表里要启用 <b>Post Processing</b>，
    /// 否则本类建的 Volume 不会生效。本类同时尝试给主相机开 <see cref="UniversalAdditionalCameraData.renderPostProcessing"/>，
    /// 把"相机级"开关也保证 ON。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CubicPostProcessing : MonoBehaviour
    {
        [Header("总开关")]
        [SerializeField] private bool _enablePostProcessing = true;

        [Header("Bloom（让篝火 / 水晶辉光）")]
        [SerializeField] private float _bloomIntensity = 1.2f;
        [SerializeField] private float _bloomThreshold = 0.85f;
        [SerializeField] private float _bloomScatter   = 0.7f;

        [Header("Vignette（暗角）")]
        [SerializeField, Range(0f, 1f)] private float _vignetteIntensity  = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _vignetteSmoothness = 0.5f;

        [Header("ColorAdjustments（调色温 / 饱和度 / 曝光）")]
        [SerializeField] private float _postExposure = 0.05f;
        [SerializeField] private float _contrast    = 5f;
        [SerializeField] private float _saturation  = 12f;
        [SerializeField] private Color _colorFilter = new Color(1f, 0.96f, 0.88f, 1f);

        private void Start()
        {
            if (!_enablePostProcessing) return;
            if (!TryGetComponent<CubicMap>(out var map)) return;
            var cam = map.GetMainCamera();
            if (cam == null) return;

            // ① 强制开 URP Renderer 级 post-processing（绕过 .asset 配置 —— 没这一步相机开了 PP 也被 renderer 拦掉）
            TryEnableRendererPostProcessing();
            // ② 强制开主相机级 post-processing
            TryEnableCameraPostProcessing(cam);
            // ③ 运行时建 Global Volume + 各种 override
            BuildVolume();
        }

        // ════════════════════════════════════════════════════════════
        //  相机级 post-processing 开关
        // ════════════════════════════════════════════════════════════

        private static void TryEnableCameraPostProcessing(Camera cam)
        {
            try
            {
                var urpData = cam.GetUniversalAdditionalCameraData();
                urpData.renderPostProcessing = true;
                urpData.renderShadows = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CubicPostProcessing] 无法访问 URP 相机数据：{e.Message}。请确认 URP 包已安装且 URP Asset 启用了 Post Processing。");
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Renderer 级 post-processing 开关（绕过 .asset 配置）
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 强制把当前 URP Asset 关联的所有 <see cref="ScriptableRendererData.postProcessing"/> 设为 true。
        /// <para>
        /// 用途：URP 17 把 Post Processing 的总开关放在了 Renderer Data 上（public 字段，Inspector 不显示），
        /// 很多项目拿到的 URP-ForwardRenderer.asset 这个字段默认是 false，结果相机勾了 renderPostProcessing 也会
        /// 弹出 "Post-processing is currently disabled on the current Universal Render Pipeline renderer" 警告
        /// 且 Volume 不生效。本方法在 Play 时把内存里的 RendererData.postProcessing 全部刷成 true,绕过 .asset 配置。
        /// </para>
        /// </summary>
        private static void TryEnableRendererPostProcessing()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;

            var renderers = pipeline.rendererDataList;
            if (renderers == null) return;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!(r is UniversalRendererData)) continue;

                // URP 17 把 postProcessing 改成 internal 了,外部代码拿不到,反射兜底
                // 既找字段也找属性,两种声明方式都覆盖
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
                var t = r.GetType();
                var f = t.GetField("postProcessing", flags);
                if (f != null && f.FieldType == typeof(bool))
                {
                    f.SetValue(r, true);
                    continue;
                }
                var p = t.GetProperty("postProcessing", flags);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
                {
                    p.SetValue(r, true);
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  Volume + Overrides
        // ════════════════════════════════════════════════════════════

        private void BuildVolume()
        {
            // 避免 Reload Domain / 重 Play 时重复创建
            if (GameObject.Find("Global Volume (Cubic)") != null) return;

            var go = new GameObject("Global Volume (Cubic)");
            go.transform.SetParent(transform, false);

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0;
            volume.weight = 1f;

            // 运行时 VolumeProfile：ScriptableObject.CreateInstance 出来再 Add 各 override
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "CubicPostFXProfile";
            volume.profile = profile;

            AddBloom(profile);
            AddVignette(profile);
            AddColorAdjustments(profile);
        }

        private void AddBloom(VolumeProfile profile)
        {
            var bloom = profile.Add<Bloom>();
            bloom.intensity.overrideState = true;
            bloom.intensity.value = _bloomIntensity;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = _bloomThreshold;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = _bloomScatter;
        }

        private void AddVignette(VolumeProfile profile)
        {
            var v = profile.Add<Vignette>();
            v.intensity.overrideState = true;
            v.intensity.value = _vignetteIntensity;
            v.smoothness.overrideState = true;
            v.smoothness.value = _vignetteSmoothness;
            v.color.overrideState = true;
            v.color.value = Color.black;
        }

        private void AddColorAdjustments(VolumeProfile profile)
        {
            var ca = profile.Add<ColorAdjustments>();
            ca.postExposure.overrideState = true;
            ca.postExposure.value = _postExposure;
            ca.contrast.overrideState = true;
            ca.contrast.value = _contrast;
            ca.saturation.overrideState = true;
            ca.saturation.value = _saturation;
            ca.colorFilter.overrideState = true;
            ca.colorFilter.value = _colorFilter;
        }
    }
}
