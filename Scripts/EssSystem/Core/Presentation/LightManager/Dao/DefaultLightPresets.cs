using UnityEngine;

namespace EssSystem.Core.Presentation.LightManager.Dao
{
    /// <summary>
    /// 内置 4 个昼夜预设 —— 启动时若 <c>_registerDefaultPresets = true</c> 自动注册到 <see cref="LightService"/>。
    /// 业务侧可用同 Name 覆盖。
    /// </summary>
    public static class DefaultLightPresets
    {
        public static LightPreset Dawn => new LightPreset("Dawn")
        {
            SunColor = new Color(1f, 0.7f, 0.5f),
            SunIntensity = 0.8f,
            SunEulerAngles = new Vector3(10f, -30f, 0f),
            AmbientColor = new Color(0.5f, 0.45f, 0.45f),
            AmbientIntensity = 0.9f,
            FogEnabled = true,
            FogColor = new Color(0.85f, 0.7f, 0.6f),
            FogDensity = 0.012f,
            BloomIntensity = 1.4f,
            VignetteIntensity = 0.15f,
            ColorAdjustmentsExposure = 0f,
            ColorAdjustmentsSaturation = 5f,
        };

        public static LightPreset Noon => new LightPreset("Noon")
        {
            SunColor = Color.white,
            SunIntensity = 1.3f,
            SunEulerAngles = new Vector3(60f, -30f, 0f),
            AmbientColor = new Color(0.55f, 0.6f, 0.65f),
            AmbientIntensity = 1f,
            FogEnabled = false,
            FogDensity = 0.005f,
            BloomIntensity = 0.8f,
            VignetteIntensity = 0.1f,
            ColorAdjustmentsExposure = 0f,
            ColorAdjustmentsSaturation = 0f,
        };

        public static LightPreset Dusk => new LightPreset("Dusk")
        {
            SunColor = new Color(1f, 0.55f, 0.4f),
            SunIntensity = 0.7f,
            SunEulerAngles = new Vector3(8f, -120f, 0f),
            AmbientColor = new Color(0.45f, 0.35f, 0.4f),
            AmbientIntensity = 0.85f,
            FogEnabled = true,
            FogColor = new Color(0.65f, 0.4f, 0.45f),
            FogDensity = 0.015f,
            BloomIntensity = 1.6f,
            VignetteIntensity = 0.2f,
            ColorAdjustmentsExposure = -0.2f,
            ColorAdjustmentsSaturation = 10f,
            ColorAdjustmentsContrast = 5f,
        };

        public static LightPreset Night => new LightPreset("Night")
        {
            SunColor = new Color(0.4f, 0.5f, 0.7f),     // 模拟月光
            SunIntensity = 0.25f,
            SunEulerAngles = new Vector3(-60f, -30f, 0f), // 太阳在下，月光方向
            AmbientColor = new Color(0.15f, 0.2f, 0.3f),
            AmbientIntensity = 0.5f,
            FogEnabled = true,
            FogColor = new Color(0.1f, 0.12f, 0.18f),
            FogDensity = 0.02f,
            BloomIntensity = 1.2f,
            VignetteIntensity = 0.35f,
            ColorAdjustmentsExposure = -0.5f,
            ColorAdjustmentsSaturation = -15f,
            ColorAdjustmentsContrast = 10f,
        };

        /// <summary>4 个内置预设按昼夜顺序排列。</summary>
        public static LightPreset[] All => new[] { Dawn, Noon, Dusk, Night };
    }
}
