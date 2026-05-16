using System;
using UnityEngine;

namespace EssSystem.Core.Presentation.LightManager.Dao
{
    /// <summary>
    /// 灯光预设 —— 把"主光 + 环境 + 雾 + 后处理"打包成一个可序列化整体，<see cref="LightManager"/> 通过 <c>EVT_APPLY_PRESET</c> 切换并支持时间插值。
    /// </summary>
    [Serializable]
    public class LightPreset
    {
        /// <summary>预设名（唯一 key）。</summary>
        public string Name;

        // ─── Sun（Directional Light）─────────────────────────────
        public Color   SunColor       = Color.white;
        public float   SunIntensity   = 1f;
        public Vector3 SunEulerAngles = new Vector3(50f, -30f, 0f);

        // ─── 环境光 / 雾 / 天空盒 ────────────────────────────────
        public Color   AmbientColor     = new Color(0.45f, 0.5f, 0.55f);
        public float   AmbientIntensity = 1f;

        public bool  FogEnabled = false;
        public Color FogColor   = new Color(0.5f, 0.5f, 0.55f);
        public float FogDensity = 0.01f;

        /// <summary>天空盒材质 Resources 路径（空 = 不切换）。</summary>
        public string SkyboxResourcesPath;

        // ─── URP 后处理（Volume 中 4 个核心 Effect 的关键参数）────
        public float BloomIntensity             = 1f;
        public float VignetteIntensity          = 0.2f;
        public float ChromaticAberrationStrength = 0f;
        public float ColorAdjustmentsExposure   = 0f;
        public float ColorAdjustmentsSaturation = 0f;
        public float ColorAdjustmentsContrast   = 0f;

        public LightPreset() { }
        public LightPreset(string name) { Name = name; }
    }
}
