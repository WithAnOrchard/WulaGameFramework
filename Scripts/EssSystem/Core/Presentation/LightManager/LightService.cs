using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Presentation.LightManager.Dao;

namespace EssSystem.Core.Presentation.LightManager
{
    /// <summary>
    /// 灯光服务 —— 持久化预设 + 玩家无障碍偏好。
    /// </summary>
    public class LightService : Service<LightService>
    {
        // ─── 数据分类 ────────────────────────────────────────────────
        public  const string CATEGORY_PRESETS  = "Presets";   // 预设（持久化）
        private const string CATEGORY_SETTINGS = "Settings";

        // ─── 设置键 ──────────────────────────────────────────────────
        private const string KEY_BLOOM_SCALE         = "BloomIntensityScale";
        private const string KEY_VIGNETTE_ENABLED    = "VignetteEnabled";
        private const string KEY_CA_ENABLED          = "ChromaticAberrationEnabled";
        private const string KEY_CURRENT_PRESET_NAME = "CurrentPresetName";

        // ─── 默认值 ───────────────────────────────────────────────────
        private const float DEFAULT_BLOOM_SCALE      = 1f;
        private const bool  DEFAULT_VIGNETTE_ENABLED = true;
        private const bool  DEFAULT_CA_ENABLED       = true;

        // ─── 属性 ─────────────────────────────────────────────────────
        /// <summary>玩家偏好：Bloom 强度全局倍率（0~2）；0 = 关闭闪光敏感效果。</summary>
        public float BloomIntensityScale
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_BLOOM_SCALE);
            set => SetData(CATEGORY_SETTINGS, KEY_BLOOM_SCALE, Mathf.Clamp(value, 0f, 2f));
        }

        public bool VignetteEnabled
        {
            get => GetData<bool>(CATEGORY_SETTINGS, KEY_VIGNETTE_ENABLED);
            set => SetData(CATEGORY_SETTINGS, KEY_VIGNETTE_ENABLED, value);
        }

        public bool ChromaticAberrationEnabled
        {
            get => GetData<bool>(CATEGORY_SETTINGS, KEY_CA_ENABLED);
            set => SetData(CATEGORY_SETTINGS, KEY_CA_ENABLED, value);
        }

        /// <summary>当前应用的预设名（启动时自动恢复）。</summary>
        public string CurrentPresetName
        {
            get => GetData<string>(CATEGORY_SETTINGS, KEY_CURRENT_PRESET_NAME);
            set => SetData(CATEGORY_SETTINGS, KEY_CURRENT_PRESET_NAME, value);
        }

        // ─── 初始化 ─────────────────────────────────────────────────
        protected override void Initialize()
        {
            base.Initialize();
            EnsureDefaults();
        }

        private void EnsureDefaults()
        {
            if (!HasData(CATEGORY_SETTINGS, KEY_BLOOM_SCALE))      BloomIntensityScale        = DEFAULT_BLOOM_SCALE;
            if (!HasData(CATEGORY_SETTINGS, KEY_VIGNETTE_ENABLED)) VignetteEnabled            = DEFAULT_VIGNETTE_ENABLED;
            if (!HasData(CATEGORY_SETTINGS, KEY_CA_ENABLED))       ChromaticAberrationEnabled = DEFAULT_CA_ENABLED;
        }

        // ─── 预设管理 ────────────────────────────────────────────────
        public void RegisterPreset(LightPreset preset)
        {
            if (preset == null || string.IsNullOrEmpty(preset.Name))
            {
                LogWarning("忽略空预设或缺 Name 的预设");
                return;
            }
            SetData(CATEGORY_PRESETS, preset.Name, preset);
            Log($"注册灯光预设: {preset.Name}", Color.blue);
        }

        public LightPreset GetPreset(string name) =>
            string.IsNullOrEmpty(name) ? null : GetData<LightPreset>(CATEGORY_PRESETS, name);

        public IEnumerable<LightPreset> GetAllPresets()
        {
            if (!_dataStorage.TryGetValue(CATEGORY_PRESETS, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is LightPreset p) yield return p;
        }

        public bool RemovePreset(string name) =>
            !string.IsNullOrEmpty(name) && RemoveData(CATEGORY_PRESETS, name);
    }
}
