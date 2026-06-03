using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace Demo.Cubic.VFX
{
    /// <summary>
    /// Cubic Z 轴打光特效 —— 从 +Z 方向投射一束体积光到目标位置。
    /// <para>由三部分组成：(1) 一盏 Spot Light（影响目标表面光照），(2) 一束体积光柱 Mesh（使用 URP <c>LightBeam.shader</c>，靠菲涅尔制造体积感），(3) 可选 Bloom 后处理增强（通过 LightManager "SetBloom" 事件）。</para>
    /// <para>所有灯光控制经由 <see cref="EssSystem.Core.Presentation.LightManager.LightManager"/> Event API（bare-string），不直接持有 Light 引用管理。</para>
    /// <para>注意：需要 URP 环境（LightManager 的 <c>URP_INSTALLED</c> 编译符号已定义）；未安装 URP 时 API 仍可调用，但 LightManager 不会响应（stub）。</para>
    /// </summary>
    public static class CubicZAxisLightVFX
    {
        // ─── LightManager 事件名（消费方按 §4.1 走 bare-string） ─────
        private const string EVT_REGISTER_LIGHT       = "RegisterLight";
        private const string EVT_UNREGISTER_LIGHT     = "UnregisterLight";
        private const string EVT_SET_LIGHT_INTENSITY  = "SetLightIntensity";
        private const string EVT_SET_LIGHT_RANGE      = "SetLightRange";
        private const string EVT_SET_BLOOM            = "SetBloom";

        // ─── 默认参数（无参快捷播放时使用）─────────────────────────
        private const string DEFAULT_BEAM_SHADER = "Cubic/LightBeam";

        // ─── 运行时单例状态（只允许一个 Z 轴打光实例在飞）──────────
        private static GameObject _beamRoot;       // 光束根 GameObject（包含 Spot Light + Beam Mesh）
        private static Coroutine  _playCoroutine;  // 动画协程
        private static Material   _beamMaterial;   // 运行时实例化材质（可调 _MainColor / _Intensity / _Alpha）

        /// <summary>
        /// 在目标位置播放 Z 轴打光特效（默认配置）。
        /// </summary>
        /// <param name="targetPosition">光束末端（即 Cubic / 目标）位置。</param>
        public static void Play(Vector3 targetPosition)
        {
            Play(targetPosition, CubicZAxisLightConfig.Default);
        }

        /// <summary>
        /// 在目标位置播放 Z 轴打光特效（自定义配置）。
        /// </summary>
        /// <param name="targetPosition">光束末端位置。</param>
        /// <param name="config">配置（颜色、强度、长度、持续时间、是否启用 Bloom 等）。</param>
        public static void Play(Vector3 targetPosition, CubicZAxisLightConfig config)
        {
            if (config == null) config = CubicZAxisLightConfig.Default;

            // 停掉任何正在播放的实例（重启）
            Stop();

            // 计算光束源点（target 的 +Z 方向，offset = beamLength）
            Vector3 sourcePos = targetPosition + Vector3.forward * config.BeamLength;
            Vector3 dir       = (targetPosition - sourcePos).normalized;

            // ─── 1) 创建光束根 GameObject（位于光源端，朝向 -Z）──
            _beamRoot = new GameObject("CubicZAxisLight");
            _beamRoot.transform.position = sourcePos;
            _beamRoot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            // ─── 2) Spot Light：注册到 LightManager，后续一切控制走 Event ─
            var spotGO  = new GameObject("ZAxisSpot");
            spotGO.transform.SetParent(_beamRoot.transform, false);
            spotGO.transform.localRotation = Quaternion.identity;  // Spot forward = -Z (= root.forward)
            var spot = spotGO.AddComponent<Light>();
            spot.type            = LightType.Spot;
            spot.color           = config.Color;
            spot.intensity       = 0f;                    // 起始 0，由协程渐入
            spot.range           = config.BeamLength * 1.2f;
            spot.spotAngle       = config.SpotAngle;
            spot.innerSpotAngle  = config.SpotAngle * 0.4f;
            spot.shadows         = config.CastShadows ? LightShadows.Soft : LightShadows.None;
            spot.renderMode      = LightRenderMode.ForcePixel;

            // 注册到 LightManager
            FireEvent(EVT_REGISTER_LIGHT, new List<object> { config.LightId, spot });

            // ─── 3) 体积光束 Mesh：URP LightBeam shader，圆柱体沿 -Z 拉长 ─
            var beamGO = new GameObject("ZAxisBeamMesh");
            beamGO.transform.SetParent(_beamRoot.transform, false);
            beamGO.transform.localPosition = new Vector3(0, 0, -config.BeamLength * 0.5f);
            beamGO.transform.localRotation = Quaternion.identity;

            var beamFilter = beamGO.AddComponent<MeshFilter>();
            beamFilter.sharedMesh = BuildBeamMesh(config);

            var beamRenderer = beamGO.AddComponent<MeshRenderer>();
            beamRenderer.shadowCastingMode     = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamRenderer.receiveShadows        = false;
            beamRenderer.lightProbeUsage       = UnityEngine.Rendering.LightProbeUsage.Off;
            beamRenderer.reflectionProbeUsage  = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            beamRenderer.allowOcclusionWhenDynamic = false;

            var shader = Shader.Find(DEFAULT_BEAM_SHADER);
            if (shader == null)
            {
                Debug.LogWarning("[CubicZAxisLightVFX] 找不到 Shader 'Cubic/LightBeam'，跳过体积光束（仅 Spot Light 生效）");
            }
            else
            {
                _beamMaterial = new Material(shader);
                _beamMaterial.SetColor("_MainColor",     config.Color);
                _beamMaterial.SetFloat("_Intensity",     config.BeamIntensity);
                _beamMaterial.SetFloat("_FresnelPower",  config.FresnelPower);
                _beamMaterial.SetFloat("_LengthFade",    config.LengthFade);
                _beamMaterial.SetFloat("_NoiseStrength", config.NoiseStrength);
                _beamMaterial.SetFloat("_NoiseScale",    config.NoiseScale);
                _beamMaterial.SetFloat("_ScrollSpeed",   config.ScrollSpeed);
                _beamMaterial.SetFloat("_Alpha",         0f);  // 渐入
                beamRenderer.sharedMaterial = _beamMaterial;
            }

            // ─── 4) 启动播放协程（淡入 → 持续 → 淡出 → 销毁）──
            _playCoroutine = CoroutineHost.Instance.StartCoroutine(PlayRoutine(config));
        }

        /// <summary>停止正在播放的 Z 轴打光特效（若有）。</summary>
        public static void Stop()
        {
            if (_playCoroutine != null)
            {
                CoroutineHost.Instance.StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }

            // 清理注册 + GameObject
            FireEvent(EVT_UNREGISTER_LIGHT, new List<object> { CubicZAxisLightConfig.Default.LightId });
            if (_beamRoot != null)
            {
                Object.Destroy(_beamRoot);
                _beamRoot = null;
            }
            if (_beamMaterial != null)
            {
                Object.Destroy(_beamMaterial);
                _beamMaterial = null;
            }
        }

        // ============================================================
        // 内部：播放动画
        // ============================================================
        private static IEnumerator PlayRoutine(CubicZAxisLightConfig cfg)
        {
            float t = 0f;
            // Fade in
            while (t < cfg.FadeInDuration && cfg.FadeInDuration > 0f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / cfg.FadeInDuration);
                SetBeamAlpha(k * cfg.MaxBeamAlpha);
                FireEvent(EVT_SET_LIGHT_INTENSITY, new List<object> { cfg.LightId, cfg.SpotIntensity * k, 0f });
                yield return null;
            }
            SetBeamAlpha(cfg.MaxBeamAlpha);
            FireEvent(EVT_SET_LIGHT_INTENSITY, new List<object> { cfg.LightId, cfg.SpotIntensity, 0f });

            // 可选 Bloom 增强（高光时刻）
            if (cfg.EnableBloomBoost)
            {
                FireEvent(EVT_SET_BLOOM, new List<object> { cfg.BloomBoostIntensity, null });
            }

            // Hold
            float hold = 0f;
            while (hold < cfg.HoldDuration)
            {
                hold += Time.deltaTime;
                yield return null;
            }

            // Fade out
            t = 0f;
            while (t < cfg.FadeOutDuration && cfg.FadeOutDuration > 0f)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / cfg.FadeOutDuration);
                SetBeamAlpha(cfg.MaxBeamAlpha * k);
                FireEvent(EVT_SET_LIGHT_INTENSITY, new List<object> { cfg.LightId, cfg.SpotIntensity * k, 0f });
                yield return null;
            }

            // 还原 Bloom（如果之前 boost 过）
            if (cfg.EnableBloomBoost)
            {
                FireEvent(EVT_SET_BLOOM, new List<object> { 0f, null });
            }

            // 清理
            FireEvent(EVT_UNREGISTER_LIGHT, new List<object> { cfg.LightId });
            if (_beamRoot != null)
            {
                Object.Destroy(_beamRoot);
                _beamRoot = null;
            }
            if (_beamMaterial != null)
            {
                Object.Destroy(_beamMaterial);
                _beamMaterial = null;
            }
            _playCoroutine = null;
        }

        private static void SetBeamAlpha(float a)
        {
            if (_beamMaterial != null) _beamMaterial.SetFloat("_Alpha", Mathf.Clamp01(a));
        }

        private static void FireEvent(string evt, List<object> args)
        {
            if (!EventProcessor.HasInstance) return;
            var r = EventProcessor.Instance.TriggerEventMethod(evt, args);
            if (!ResultCode.IsOk(r))
            {
                Debug.LogWarning($"[CubicZAxisLightVFX] 事件 {evt} 失败");
            }
        }

        // ============================================================
        // 内部：构造体积光束 Mesh
        //   - 沿 local -Z 拉长的圆柱（pivot 在源点）
        //   - UV.x = 周向角度 [0,1]；UV.y = 长度方向 [0,1]（0=源点，1=远端）
        //   - 法线指向径向外（X-Y 平面内）
        //   - 顶/底圆心顶点：UV.x=0.5（夹在中间，不参与菲涅尔）
        // ============================================================
        private static Mesh BuildBeamMesh(CubicZAxisLightConfig cfg)
        {
            const int SIDES = 8;   // 周向分段（像素风用 8 已足够）
            const int LEN   = 4;   // 长度分段

            int ringCount = LEN + 1;
            int vCount    = ringCount * (SIDES + 1);   // 每环 SIDES+1 个顶点（UV 缝闭合）
            int tCount    = LEN * SIDES * 6;

            var verts   = new Vector3[vCount];
            var normals = new Vector3[vCount];
            var uvs     = new Vector2[vCount];
            var tris    = new int[tCount];

            float radius = cfg.BeamRadius;
            float length = cfg.BeamLength;

            for (int r = 0; r < ringCount; r++)
            {
                float v  = (float)r / LEN;                  // 0 → 1
                float z  = -v * length;                      // 沿 -Z 拉长
                for (int s = 0; s <= SIDES; s++)
                {
                    int   idx = r * (SIDES + 1) + s;
                    float u   = (float)s / SIDES;            // 0 → 1（周向）
                    float ang = u * Mathf.PI * 2f;
                    float cx  = Mathf.Cos(ang);
                    float cy  = Mathf.Sin(ang);

                    verts[idx]   = new Vector3(cx * radius, cy * radius, z);
                    normals[idx] = new Vector3(cx, cy, 0f);  // 径向外
                    uvs[idx]     = new Vector2(u, v);
                }
            }

            int ti = 0;
            for (int r = 0; r < LEN; r++)
            {
                for (int s = 0; s < SIDES; s++)
                {
                    int a = r * (SIDES + 1) + s;
                    int b = a + 1;
                    int c = a + (SIDES + 1);
                    int d = c + 1;
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var mesh = new Mesh { name = "CubicZAxisBeam" };
            mesh.vertices  = verts;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ============================================================
        // 协程承载器：使用 LightManager.MonoBehaviour（或自带 Host）做 Coroutine runner
        // ============================================================
        private class CoroutineHost : MonoBehaviour
        {
            private static CoroutineHost _instance;
            public static CoroutineHost Instance
            {
                get
                {
                    if (_instance != null) return _instance;
                    var go = new GameObject("CubicZAxisLightVFX.CoroutineHost");
                    Object.DontDestroyOnLoad(go);
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _instance = go.AddComponent<CoroutineHost>();
                    return _instance;
                }
            }
        }
    }

    /// <summary>
    /// Cubic Z 轴打光特效配置。
    /// </summary>
    [System.Serializable]
    public class CubicZAxisLightConfig
    {
        // ─── 灯光 ─────────────────────────────────────────────
        /// <summary>Spot Light 注册到 LightManager 的 ID（要保证唯一）。</summary>
        public string LightId = "cubic_z_axis_light";

        /// <summary>光束颜色（HDR 推荐）。</summary>
        public Color Color = new Color(1f, 0.85f, 0.4f, 1f);

        /// <summary>Spot Light 强度峰值。</summary>
        public float SpotIntensity = 12f;

        /// <summary>Spot Light 锥角（度，0-179）。</summary>
        public float SpotAngle = 35f;

        /// <summary>是否投影。</summary>
        public bool CastShadows = false;

        // ─── 光束 Mesh ─────────────────────────────────────────
        /// <summary>光束长度（世界单位，+Z 方向上离目标的距离）。</summary>
        public float BeamLength = 6f;

        /// <summary>光束半径（圆柱横截面半径）。</summary>
        public float BeamRadius = 0.8f;

        /// <summary>体积光束整体强度（HDR 系数）。</summary>
        public float BeamIntensity = 1.6f;

        /// <summary>菲涅尔指数（越大边缘越集中，2~4 通用）。</summary>
        public float FresnelPower = 2.0f;

        /// <summary>沿光束方向的衰减（0-1，越大衰减越快）。</summary>
        public float LengthFade = 0.8f;

        /// <summary>噪声扰动强度（模拟尘埃，0-1）。</summary>
        public float NoiseStrength = 0.25f;

        /// <summary>噪声尺度。</summary>
        public float NoiseScale = 4f;

        /// <summary>噪声滚动速度。</summary>
        public float ScrollSpeed = 1.2f;

        /// <summary>体积光束最大 Alpha（峰值时）。</summary>
        public float MaxBeamAlpha = 0.9f;

        // ─── 时序 ─────────────────────────────────────────────
        public float FadeInDuration  = 0.25f;
        public float HoldDuration    = 0.6f;
        public float FadeOutDuration = 0.5f;

        // ─── Bloom 增强（可选）─────────────────────────────────
        public bool  EnableBloomBoost       = true;
        public float BloomBoostIntensity    = 1.8f;

        /// <summary>默认配置（快捷 Play 用的"温暖金黄、持续 1.35s"档位）。</summary>
        public static CubicZAxisLightConfig Default => new CubicZAxisLightConfig
        {
            LightId = "cubic_z_axis_light_default",
            Color = new Color(1f, 0.82f, 0.45f, 1f),
            SpotIntensity = 12f,
            SpotAngle = 35f,
            BeamLength = 6f,
            BeamRadius = 0.85f,
            BeamIntensity = 1.6f,
            FresnelPower = 2.0f,
            LengthFade = 0.8f,
            NoiseStrength = 0.3f,
            NoiseScale = 4f,
            ScrollSpeed = 1.4f,
            MaxBeamAlpha = 0.9f,
            FadeInDuration  = 0.25f,
            HoldDuration    = 0.6f,
            FadeOutDuration = 0.5f,
            EnableBloomBoost = true,
            BloomBoostIntensity = 1.8f,
        };
    }
}
