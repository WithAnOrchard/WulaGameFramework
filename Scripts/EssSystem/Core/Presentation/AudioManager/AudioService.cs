using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Presentation.AudioManager
{
    /// <summary>
    /// 音频服务 - 纯 C# 单例，用于音频配置的持久化存储
    /// </summary>
    public class AudioService : Service<AudioService>
    {
        // ─── 数据分类 ────────────────────────────────────────────────

        private const string CATEGORY_SETTINGS = "Settings";
        private const string CATEGORY_RESOURCES = "Resources";

        // ─── 设置键名 ────────────────────────────────────────────────

        private const string KEY_MASTER_VOLUME = "MasterVolume";
        private const string KEY_BGM_VOLUME = "BGMVolume";
        private const string KEY_SFX_VOLUME = "SFXVolume";
        private const string KEY_BGM_PATH = "BGMPath";
        private const string KEY_SFX_PATH_PREFIX = "SFXPath_";

        // ─── 默认值 ───────────────────────────────────────────────────

        private const float DEFAULT_MASTER_VOLUME = 1f;
        private const float DEFAULT_BGM_VOLUME = 1f;
        private const float DEFAULT_SFX_VOLUME = 1f;

        // ─── 属性 ─────────────────────────────────────────────────────

        public float MasterVolume
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_MASTER_VOLUME);
            set => SetData(CATEGORY_SETTINGS, KEY_MASTER_VOLUME, value);
        }

        public float BGMVolume
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_BGM_VOLUME);
            set => SetData(CATEGORY_SETTINGS, KEY_BGM_VOLUME, value);
        }

        public float SFXVolume
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_SFX_VOLUME);
            set => SetData(CATEGORY_SETTINGS, KEY_SFX_VOLUME, value);
        }

        public string CurrentBGMPath
        {
            get => GetData<string>(CATEGORY_SETTINGS, KEY_BGM_PATH);
            set => SetData(CATEGORY_SETTINGS, KEY_BGM_PATH, value);
        }

        // ─── 初始化 ─────────────────────────────────────────────────

        protected override void Initialize()
        {
            base.Initialize();
            EnsureDefaultSettings();
        }

        private void EnsureDefaultSettings()
        {
            if (!HasData(CATEGORY_SETTINGS, KEY_MASTER_VOLUME))
                MasterVolume = DEFAULT_MASTER_VOLUME;

            if (!HasData(CATEGORY_SETTINGS, KEY_BGM_VOLUME))
                BGMVolume = DEFAULT_BGM_VOLUME;

            if (!HasData(CATEGORY_SETTINGS, KEY_SFX_VOLUME))
                SFXVolume = DEFAULT_SFX_VOLUME;
        }

        // ─── 资源路径管理 ───────────────────────────────────────────

        /// <summary>获取音效资源路径</summary>
        public string GetSFXPath(string sfxName)
        {
            var key = KEY_SFX_PATH_PREFIX + sfxName;
            return GetData<string>(CATEGORY_RESOURCES, key);
        }

        /// <summary>设置音效资源路径</summary>
        public void SetSFXPath(string sfxName, string path)
        {
            var key = KEY_SFX_PATH_PREFIX + sfxName;
            SetData(CATEGORY_RESOURCES, key, path);
        }

        /// <summary>移除音效资源路径</summary>
        public bool RemoveSFXPath(string sfxName)
        {
            var key = KEY_SFX_PATH_PREFIX + sfxName;
            return RemoveData(CATEGORY_RESOURCES, key);
        }

        // ─── 持久化 ─────────────────────────────────────────────────

        /// <summary>重置所有设置为默认值</summary>
        public void ResetToDefaults()
        {
            MasterVolume = DEFAULT_MASTER_VOLUME;
            BGMVolume = DEFAULT_BGM_VOLUME;
            SFXVolume = DEFAULT_SFX_VOLUME;
            CurrentBGMPath = null;

            Log("音频设置已重置为默认值", Color.yellow);
        }

        // ─── Transient Category ───────────────────────────────────────

        /// <summary>Resources 分类不持久化到磁盘（运行时映射）</summary>
        protected override bool IsTransientCategory(string category)
        {
            return category == CATEGORY_RESOURCES;
        }
    }
}
