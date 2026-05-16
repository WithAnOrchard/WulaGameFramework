// URP 检测：此 Manager 仅在 URP package 安装后启用。
// 启动时 LightManagerInstaller (Editor) 会自动同步 URP_INSTALLED 编译符号。
// 项目未装 URP 时走文件末尾 #else 分支的 stub 实现（不引用任何 URP 类型）。
#if URP_INSTALLED
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.LightManager.Dao;

namespace EssSystem.Core.Presentation.LightManager
{
    /// <summary>
    /// 灯光管理器（**URP 专用**）—— 主光 / 环境光 / 雾 / 天空盒 / 后处理 Volume / 动态 Light / Light2D 统一控制 + 昼夜预设。
    /// <para>本 Manager 依赖 URP package（<c>UnityEngine.Rendering.Universal</c>）。Built-in Render Pipeline 项目请勿挂载。</para>
    /// <para>与 <c>VoxelLightManager</c> 正交：本 Manager 处理 URP 场景级（sun + sky + post-FX）；VoxelLightManager 处理体素地图内的光照传播。</para>
    /// </summary>
    [Manager(7)]
    public class LightManager : Manager<LightManager>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        // —— Sun ——
        public const string EVT_SET_SUN_COLOR     = "SetSunColor";
        public const string EVT_SET_SUN_INTENSITY = "SetSunIntensity";
        public const string EVT_SET_SUN_ROTATION  = "SetSunRotation";

        // —— Environment ——
        public const string EVT_SET_AMBIENT = "SetAmbientLight";
        public const string EVT_SET_FOG     = "SetFog";
        public const string EVT_SET_SKYBOX  = "SetSkybox";

        // —— URP 后处理 Volume ——
        public const string EVT_SET_BLOOM                = "SetBloom";
        public const string EVT_SET_VIGNETTE             = "SetVignette";
        public const string EVT_SET_CHROMATIC_ABERRATION = "SetChromaticAberration";
        public const string EVT_SET_COLOR_ADJUSTMENTS    = "SetColorAdjustments";

        // —— Presets ——
        public const string EVT_REGISTER_PRESET = "RegisterLightPreset";
        public const string EVT_APPLY_PRESET    = "ApplyLightPreset";

        // —— Dynamic 3D Lights ——
        public const string EVT_REGISTER_LIGHT      = "RegisterLight";
        public const string EVT_SET_LIGHT_INTENSITY = "SetLightIntensity";

        // —— Dynamic 2D Lights（URP 2D Light2D）——
        public const string EVT_REGISTER_LIGHT_2D      = "RegisterLight2D";
        public const string EVT_SET_LIGHT_2D_INTENSITY = "SetLight2DIntensity";
        public const string EVT_SET_LIGHT_2D_COLOR     = "SetLight2DColor";

        // ============================================================
        // Inspector
        // ============================================================
        [Header("Sun (Directional Light)")]
        [Tooltip("主光源；为空启动时自动找场景中的 Directional Light（首个）")]
        [SerializeField] private Light _sunLight;

        [Header("Volume (URP 后处理)")]
        [Tooltip("全局 Volume 引用；为空且 _autoCreateVolume = true 时自动建立")]
        [SerializeField] private Volume _globalVolume;

        [Tooltip("启动时自动创建一个 Global Volume（包含 Bloom/Vignette/CA/ColorAdjustments 4 个 effect）")]
        [SerializeField] private bool _autoCreateVolume = true;

        [Header("Default Presets")]
        [Tooltip("启动时注册 4 个内置预设（Dawn/Noon/Dusk/Night）；同名业务可覆盖")]
        [SerializeField] private bool _registerDefaultPresets = true;

        [Tooltip("启动时是否自动应用 Service 中保存的 CurrentPresetName")]
        [SerializeField] private bool _autoApplySavedPreset = true;

        public LightService Service => LightService.Instance;

        // ============================================================
        // 运行时缓存
        // ============================================================
        private VolumeProfile        _profile;
        private Bloom                _bloom;
        private Vignette             _vignette;
        private ChromaticAberration  _ca;
        private ColorAdjustments     _colorAdjustments;

        private readonly Dictionary<string, Light>   _lights3D = new();
        private readonly Dictionary<string, Light2D> _lights2D = new();

        private Coroutine _presetCoroutine;
        private LightPreset _currentPresetData;

        // ============================================================
        // 生命周期
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            EnsureSunLight();
            EnsureVolume();

            if (_registerDefaultPresets && Service != null)
                foreach (var p in DefaultLightPresets.All) Service.RegisterPreset(p);

            if (_autoApplySavedPreset && Service != null)
            {
                var saved = Service.CurrentPresetName;
                if (!string.IsNullOrEmpty(saved)) ApplyPreset(saved, 0f);
            }

            Log($"LightManager 初始化完成（sun={(_sunLight != null ? _sunLight.name : "无")}, volume={(_globalVolume != null ? "✓" : "无")}）", Color.green);
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        private void EnsureSunLight()
        {
            if (_sunLight != null) return;
            // 找场景中第一盏 Directional Light
#if UNITY_2023_1_OR_NEWER
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
            var lights = Object.FindObjectsOfType<Light>();
#endif
            foreach (var l in lights)
                if (l != null && l.type == LightType.Directional) { _sunLight = l; break; }
        }

        private void EnsureVolume()
        {
            if (_globalVolume != null) { CacheVolumeEffects(); return; }
            if (!_autoCreateVolume) return;

            var go = new GameObject("LightVolume_Global");
            go.transform.SetParent(transform, false);
            _globalVolume = go.AddComponent<Volume>();
            _globalVolume.isGlobal = true;
            _globalVolume.priority = 1f;

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "LightManager_Runtime";
            _globalVolume.profile = _profile;

            // 添加 4 个核心 effect
            _bloom            = _profile.Add<Bloom>(true);
            _vignette         = _profile.Add<Vignette>(true);
            _ca               = _profile.Add<ChromaticAberration>(true);
            _colorAdjustments = _profile.Add<ColorAdjustments>(true);

            Log("LightManager 自动创建全局 Volume + 4 个 Effect", Color.green);
        }

        /// <summary>缓存 Inspector 注入的 Volume 内的 4 个 effect 引用。</summary>
        private void CacheVolumeEffects()
        {
            if (_globalVolume == null || _globalVolume.profile == null) return;
            _profile = _globalVolume.profile;
            _profile.TryGet(out _bloom);
            _profile.TryGet(out _vignette);
            _profile.TryGet(out _ca);
            _profile.TryGet(out _colorAdjustments);
        }

        // ============================================================
        // C# API：Sun
        // ============================================================
        public void SetSunColor(Color c)        { if (_sunLight != null) _sunLight.color = c; }
        public void SetSunIntensity(float i)    { if (_sunLight != null) _sunLight.intensity = Mathf.Max(0f, i); }
        public void SetSunRotation(Vector3 e)   { if (_sunLight != null) _sunLight.transform.rotation = Quaternion.Euler(e); }

        // ============================================================
        // C# API：Environment
        // ============================================================
        public void SetAmbient(Color color, float? intensity = null)
        {
            RenderSettings.ambientLight = color;
            if (intensity.HasValue) RenderSettings.ambientIntensity = Mathf.Max(0f, intensity.Value);
        }

        public void SetFog(bool enable, Color? color = null, float? density = null)
        {
            RenderSettings.fog = enable;
            if (color.HasValue)   RenderSettings.fogColor   = color.Value;
            if (density.HasValue) RenderSettings.fogDensity = Mathf.Max(0f, density.Value);
        }

        /// <summary>切换天空盒（走 ResourceManager 加载 Material）。</summary>
        public bool SetSkybox(string resourcesPath)
        {
            if (string.IsNullOrEmpty(resourcesPath) || !EventProcessor.HasInstance) return false;
            // §4.1 跨模块 bare-string："GetMaterial"
            var r = EventProcessor.Instance.TriggerEventMethod("GetMaterial", new List<object> { resourcesPath });
            if (!ResultCode.IsOk(r) || r.Count < 2 || !(r[1] is Material mat)) return false;
            RenderSettings.skybox = mat;
            DynamicGI.UpdateEnvironment();   // 让环境光重新烘焙
            return true;
        }

        // ============================================================
        // C# API：URP 后处理 Volume
        // ============================================================
        public void SetBloom(float intensity, float? threshold = null)
        {
            if (_bloom == null) return;
            var scale = Service != null ? Service.BloomIntensityScale : 1f;
            _bloom.intensity.overrideState = true;
            _bloom.intensity.value = Mathf.Max(0f, intensity * scale);
            if (threshold.HasValue) { _bloom.threshold.overrideState = true; _bloom.threshold.value = threshold.Value; }
        }

        public void SetVignette(float intensity, Color? color = null)
        {
            if (_vignette == null) return;
            var enabled = Service == null || Service.VignetteEnabled;
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = enabled ? Mathf.Clamp01(intensity) : 0f;
            if (color.HasValue) { _vignette.color.overrideState = true; _vignette.color.value = color.Value; }
        }

        public void SetChromaticAberration(float strength)
        {
            if (_ca == null) return;
            var enabled = Service == null || Service.ChromaticAberrationEnabled;
            _ca.intensity.overrideState = true;
            _ca.intensity.value = enabled ? Mathf.Clamp01(strength) : 0f;
        }

        public void SetColorAdjustments(float exposure, float? saturation = null, float? contrast = null)
        {
            if (_colorAdjustments == null) return;
            _colorAdjustments.postExposure.overrideState = true;
            _colorAdjustments.postExposure.value = exposure;
            if (saturation.HasValue) { _colorAdjustments.saturation.overrideState = true; _colorAdjustments.saturation.value = Mathf.Clamp(saturation.Value, -100f, 100f); }
            if (contrast.HasValue)   { _colorAdjustments.contrast.overrideState   = true; _colorAdjustments.contrast.value   = Mathf.Clamp(contrast.Value,   -100f, 100f); }
        }

        // ============================================================
        // C# API：Presets
        // ============================================================
        public void RegisterPreset(LightPreset preset) => Service?.RegisterPreset(preset);

        public bool ApplyPreset(string name, float duration = 0f)
        {
            var to = Service?.GetPreset(name);
            if (to == null) { LogWarning($"ApplyPreset 失败，未注册: {name}"); return false; }

            if (_presetCoroutine != null) StopCoroutine(_presetCoroutine);
            var from = _currentPresetData ?? CaptureCurrentAsPreset();
            if (duration <= 0f) { ApplyPresetImmediate(to); _currentPresetData = to; }
            else _presetCoroutine = StartCoroutine(BlendPresetRoutine(from, to, duration));

            if (Service != null) Service.CurrentPresetName = name;
            return true;
        }

        private LightPreset CaptureCurrentAsPreset()
        {
            var p = new LightPreset("__current__");
            if (_sunLight != null)
            {
                p.SunColor       = _sunLight.color;
                p.SunIntensity   = _sunLight.intensity;
                p.SunEulerAngles = _sunLight.transform.rotation.eulerAngles;
            }
            p.AmbientColor     = RenderSettings.ambientLight;
            p.AmbientIntensity = RenderSettings.ambientIntensity;
            p.FogEnabled       = RenderSettings.fog;
            p.FogColor         = RenderSettings.fogColor;
            p.FogDensity       = RenderSettings.fogDensity;

            if (_bloom    != null) p.BloomIntensity            = _bloom.intensity.value;
            if (_vignette != null) p.VignetteIntensity         = _vignette.intensity.value;
            if (_ca       != null) p.ChromaticAberrationStrength = _ca.intensity.value;
            if (_colorAdjustments != null)
            {
                p.ColorAdjustmentsExposure   = _colorAdjustments.postExposure.value;
                p.ColorAdjustmentsSaturation = _colorAdjustments.saturation.value;
                p.ColorAdjustmentsContrast   = _colorAdjustments.contrast.value;
            }
            return p;
        }

        private void ApplyPresetImmediate(LightPreset p)
        {
            SetSunColor(p.SunColor);
            SetSunIntensity(p.SunIntensity);
            SetSunRotation(p.SunEulerAngles);

            SetAmbient(p.AmbientColor, p.AmbientIntensity);
            SetFog(p.FogEnabled, p.FogColor, p.FogDensity);
            if (!string.IsNullOrEmpty(p.SkyboxResourcesPath)) SetSkybox(p.SkyboxResourcesPath);

            SetBloom(p.BloomIntensity);
            SetVignette(p.VignetteIntensity);
            SetChromaticAberration(p.ChromaticAberrationStrength);
            SetColorAdjustments(p.ColorAdjustmentsExposure, p.ColorAdjustmentsSaturation, p.ColorAdjustmentsContrast);
        }

        private IEnumerator BlendPresetRoutine(LightPreset from, LightPreset to, float duration)
        {
            float t = 0f;
            // 离散字段：在中点切换
            bool   discreteApplied  = false;

            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.0001f, duration);
                var k = Mathf.Clamp01(t);

                if (_sunLight != null)
                {
                    _sunLight.color     = Color.Lerp(from.SunColor, to.SunColor, k);
                    _sunLight.intensity = Mathf.Lerp(from.SunIntensity, to.SunIntensity, k);
                    _sunLight.transform.rotation = Quaternion.Slerp(
                        Quaternion.Euler(from.SunEulerAngles), Quaternion.Euler(to.SunEulerAngles), k);
                }

                RenderSettings.ambientLight     = Color.Lerp(from.AmbientColor, to.AmbientColor, k);
                RenderSettings.ambientIntensity = Mathf.Lerp(from.AmbientIntensity, to.AmbientIntensity, k);

                if (RenderSettings.fog)
                {
                    RenderSettings.fogColor   = Color.Lerp(from.FogColor, to.FogColor, k);
                    RenderSettings.fogDensity = Mathf.Lerp(from.FogDensity, to.FogDensity, k);
                }

                SetBloom(Mathf.Lerp(from.BloomIntensity, to.BloomIntensity, k));
                SetVignette(Mathf.Lerp(from.VignetteIntensity, to.VignetteIntensity, k));
                SetChromaticAberration(Mathf.Lerp(from.ChromaticAberrationStrength, to.ChromaticAberrationStrength, k));
                SetColorAdjustments(
                    Mathf.Lerp(from.ColorAdjustmentsExposure,   to.ColorAdjustmentsExposure,   k),
                    Mathf.Lerp(from.ColorAdjustmentsSaturation, to.ColorAdjustmentsSaturation, k),
                    Mathf.Lerp(from.ColorAdjustmentsContrast,   to.ColorAdjustmentsContrast,   k));

                if (!discreteApplied && k >= 0.5f)
                {
                    discreteApplied = true;
                    if (from.FogEnabled != to.FogEnabled) RenderSettings.fog = to.FogEnabled;
                    if (!string.IsNullOrEmpty(to.SkyboxResourcesPath) &&
                        to.SkyboxResourcesPath != from.SkyboxResourcesPath)
                        SetSkybox(to.SkyboxResourcesPath);
                }
                yield return null;
            }
            ApplyPresetImmediate(to);
            _currentPresetData  = to;
            _presetCoroutine    = null;
        }

        // ============================================================
        // C# API：Dynamic Lights（3D & 2D）
        // ============================================================
        public void RegisterLight(string id, Light light)
        {
            if (string.IsNullOrEmpty(id) || light == null) return;
            _lights3D[id] = light;
        }

        public void SetLightIntensity(string id, float target, float duration = 0f)
        {
            if (!_lights3D.TryGetValue(id, out var light) || light == null) return;
            if (duration <= 0f) { light.intensity = Mathf.Max(0f, target); return; }
            StartCoroutine(TweenLight3DIntensity(light, target, duration));
        }

        public void RegisterLight2D(string id, Light2D light)
        {
            if (string.IsNullOrEmpty(id) || light == null) return;
            _lights2D[id] = light;
        }

        public void SetLight2DIntensity(string id, float target, float duration = 0f)
        {
            if (!_lights2D.TryGetValue(id, out var light) || light == null) return;
            if (duration <= 0f) { light.intensity = Mathf.Max(0f, target); return; }
            StartCoroutine(TweenLight2DIntensity(light, target, duration));
        }

        public void SetLight2DColor(string id, Color color)
        {
            if (_lights2D.TryGetValue(id, out var light) && light != null) light.color = color;
        }

        private IEnumerator TweenLight3DIntensity(Light light, float target, float duration)
        {
            var from = light.intensity;
            float t = 0f;
            while (t < 1f && light != null)
            {
                t += Time.deltaTime / duration;
                light.intensity = Mathf.Lerp(from, target, Mathf.Clamp01(t));
                yield return null;
            }
            if (light != null) light.intensity = target;
        }

        private IEnumerator TweenLight2DIntensity(Light2D light, float target, float duration)
        {
            var from = light.intensity;
            float t = 0f;
            while (t < 1f && light != null)
            {
                t += Time.deltaTime / duration;
                light.intensity = Mathf.Lerp(from, target, Mathf.Clamp01(t));
                yield return null;
            }
            if (light != null) light.intensity = target;
        }

        // ============================================================
        // Event API
        // ============================================================
        [Event(EVT_SET_SUN_COLOR)]
        public List<object> OnSetSunColor(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Color c)) return ResultCode.Fail("参数 [Color]");
            SetSunColor(c); return ResultCode.Ok();
        }

        [Event(EVT_SET_SUN_INTENSITY)]
        public List<object> OnSetSunIntensity(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is float v)) return ResultCode.Fail("参数 [float]");
            SetSunIntensity(v); return ResultCode.Ok();
        }

        [Event(EVT_SET_SUN_ROTATION)]
        public List<object> OnSetSunRotation(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Vector3 e)) return ResultCode.Fail("参数 [Vector3 euler]");
            SetSunRotation(e); return ResultCode.Ok();
        }

        [Event(EVT_SET_AMBIENT)]
        public List<object> OnSetAmbient(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Color c)) return ResultCode.Fail("参数 [Color, float intensity?]");
            var i = data.Count > 1 && data[1] is float f ? (float?)f : null;
            SetAmbient(c, i); return ResultCode.Ok();
        }

        [Event(EVT_SET_FOG)]
        public List<object> OnSetFog(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is bool en)) return ResultCode.Fail("参数 [bool enable, Color? color, float? density]");
            var c = data.Count > 1 && data[1] is Color cc ? (Color?)cc : null;
            var d = data.Count > 2 && data[2] is float ff ? (float?)ff : null;
            SetFog(en, c, d); return ResultCode.Ok();
        }

        [Event(EVT_SET_SKYBOX)]
        public List<object> OnSetSkybox(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string p) || string.IsNullOrEmpty(p))
                return ResultCode.Fail("参数 [string resourcesPath]");
            return SetSkybox(p) ? ResultCode.Ok(p) : ResultCode.Fail($"加载天空盒失败: {p}");
        }

        [Event(EVT_SET_BLOOM)]
        public List<object> OnSetBloom(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is float i)) return ResultCode.Fail("参数 [float intensity, float? threshold]");
            var th = data.Count > 1 && data[1] is float t ? (float?)t : null;
            SetBloom(i, th); return ResultCode.Ok();
        }

        [Event(EVT_SET_VIGNETTE)]
        public List<object> OnSetVignette(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is float i)) return ResultCode.Fail("参数 [float intensity, Color? color]");
            var c = data.Count > 1 && data[1] is Color cc ? (Color?)cc : null;
            SetVignette(i, c); return ResultCode.Ok();
        }

        [Event(EVT_SET_CHROMATIC_ABERRATION)]
        public List<object> OnSetCA(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is float s)) return ResultCode.Fail("参数 [float strength]");
            SetChromaticAberration(s); return ResultCode.Ok();
        }

        [Event(EVT_SET_COLOR_ADJUSTMENTS)]
        public List<object> OnSetColorAdjustments(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is float ex)) return ResultCode.Fail("参数 [float postExposure, float? saturation, float? contrast]");
            var sat = data.Count > 1 && data[1] is float s ? (float?)s : null;
            var con = data.Count > 2 && data[2] is float c ? (float?)c : null;
            SetColorAdjustments(ex, sat, con); return ResultCode.Ok();
        }

        [Event(EVT_REGISTER_PRESET)]
        public List<object> OnRegisterPreset(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is LightPreset p)) return ResultCode.Fail("参数 [LightPreset]");
            RegisterPreset(p); return ResultCode.Ok(p.Name);
        }

        [Event(EVT_APPLY_PRESET)]
        public List<object> OnApplyPreset(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string name) || string.IsNullOrEmpty(name))
                return ResultCode.Fail("参数 [string presetName, float duration?]");
            var dur = data.Count > 1 && data[1] is float d ? d : 0f;
            return ApplyPreset(name, dur) ? ResultCode.Ok(name) : ResultCode.Fail($"未注册预设: {name}");
        }

        [Event(EVT_REGISTER_LIGHT)]
        public List<object> OnRegisterLight(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is Light l))
                return ResultCode.Fail("参数 [string id, Light]");
            RegisterLight(id, l); return ResultCode.Ok(id);
        }

        [Event(EVT_SET_LIGHT_INTENSITY)]
        public List<object> OnSetLightIntensity(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is float t))
                return ResultCode.Fail("参数 [string id, float intensity, float? duration]");
            var dur = data.Count > 2 && data[2] is float d ? d : 0f;
            SetLightIntensity(id, t, dur); return ResultCode.Ok(id);
        }

        [Event(EVT_REGISTER_LIGHT_2D)]
        public List<object> OnRegisterLight2D(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is Light2D l))
                return ResultCode.Fail("参数 [string id, Light2D]");
            RegisterLight2D(id, l); return ResultCode.Ok(id);
        }

        [Event(EVT_SET_LIGHT_2D_INTENSITY)]
        public List<object> OnSetLight2DIntensity(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is float t))
                return ResultCode.Fail("参数 [string id, float intensity, float? duration]");
            var dur = data.Count > 2 && data[2] is float d ? d : 0f;
            SetLight2DIntensity(id, t, dur); return ResultCode.Ok(id);
        }

        [Event(EVT_SET_LIGHT_2D_COLOR)]
        public List<object> OnSetLight2DColor(List<object> data)
        {
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is Color c))
                return ResultCode.Fail("参数 [string id, Color]");
            SetLight2DColor(id, c); return ResultCode.Ok(id);
        }
    }
}

#else   // ─── URP 未安装：stub 实现（不引用任何 URP 类型） ───────────

using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Presentation.LightManager
{
    /// <summary>
    /// LightManager **stub**（URP 未安装时的占位实现）。
    /// <para>启动时打印警告，引导通过菜单 <c>Tools/EssSystem/LightManager/Install URP Package</c> 安装 URP；
    /// 安装完成后 <see cref="EssSystem.Core.Presentation.LightManager.Editor.LightManagerInstaller"/> 自动写入 <c>URP_INSTALLED</c> 编译符号，重编译后切到完整实现。</para>
    /// <para>本 stub 不声明 EVT_* 常量（§4.1 禁止跨模块直接引用 EVT_ 常量；调用方一律 bare-string）。
    /// 完整实现的常量列表见同文件 <c>#if URP_INSTALLED</c> 分支。</para>
    /// </summary>
    [Manager(7)]
    public class LightManager : Manager<LightManager>
    {
        protected override void Initialize()
        {
            base.Initialize();
            LogWarning(
                "[LightManager] URP 未安装，本 Manager 已停用（stub 模式）。\n" +
                "→ 通过菜单 [Tools/EssSystem/LightManager/Install URP Package] 自动安装 URP；\n" +
                "→ 安装完成后 Unity 会自动重编译并启用完整实现。");
        }
    }
}

#endif