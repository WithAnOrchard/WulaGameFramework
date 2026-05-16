using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Presentation.CameraManager
{
    /// <summary>
    /// 相机服务 —— 持久化用户偏好（跟随平滑、震屏强度倍率、默认缩放等）。
    /// </summary>
    public class CameraService : Service<CameraService>
    {
        // ─── 数据分类 ────────────────────────────────────────────────
        private const string CATEGORY_SETTINGS = "Settings";

        // ─── 设置键名 ────────────────────────────────────────────────
        private const string KEY_FOLLOW_SMOOTH_TIME       = "FollowSmoothTime";
        private const string KEY_SHAKE_INTENSITY_MULT     = "ShakeIntensityMultiplier";
        private const string KEY_DEFAULT_ORTHO_SIZE       = "DefaultOrthoSize";
        private const string KEY_DEFAULT_FIELD_OF_VIEW    = "DefaultFieldOfView";

        // ─── 默认值 ───────────────────────────────────────────────────
        private const float DEFAULT_FOLLOW_SMOOTH_TIME    = 0.15f;
        private const float DEFAULT_SHAKE_INTENSITY_MULT  = 1f;
        private const float DEFAULT_ORTHO_SIZE            = 5f;
        private const float DEFAULT_FIELD_OF_VIEW         = 60f;

        // ─── 属性 ─────────────────────────────────────────────────────
        public float FollowSmoothTime
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_FOLLOW_SMOOTH_TIME);
            set => SetData(CATEGORY_SETTINGS, KEY_FOLLOW_SMOOTH_TIME, value);
        }

        /// <summary>震屏强度全局倍率（0~2）；0 = 关闭震屏（晕动症辅助）。</summary>
        public float ShakeIntensityMultiplier
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_SHAKE_INTENSITY_MULT);
            set => SetData(CATEGORY_SETTINGS, KEY_SHAKE_INTENSITY_MULT, Mathf.Clamp(value, 0f, 2f));
        }

        public float DefaultOrthoSize
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_DEFAULT_ORTHO_SIZE);
            set => SetData(CATEGORY_SETTINGS, KEY_DEFAULT_ORTHO_SIZE, value);
        }

        public float DefaultFieldOfView
        {
            get => GetData<float>(CATEGORY_SETTINGS, KEY_DEFAULT_FIELD_OF_VIEW);
            set => SetData(CATEGORY_SETTINGS, KEY_DEFAULT_FIELD_OF_VIEW, value);
        }

        // ─── 初始化 ─────────────────────────────────────────────────
        protected override void Initialize()
        {
            base.Initialize();
            EnsureDefaults();
        }

        private void EnsureDefaults()
        {
            if (!HasData(CATEGORY_SETTINGS, KEY_FOLLOW_SMOOTH_TIME))    FollowSmoothTime         = DEFAULT_FOLLOW_SMOOTH_TIME;
            if (!HasData(CATEGORY_SETTINGS, KEY_SHAKE_INTENSITY_MULT))  ShakeIntensityMultiplier = DEFAULT_SHAKE_INTENSITY_MULT;
            if (!HasData(CATEGORY_SETTINGS, KEY_DEFAULT_ORTHO_SIZE))    DefaultOrthoSize         = DEFAULT_ORTHO_SIZE;
            if (!HasData(CATEGORY_SETTINGS, KEY_DEFAULT_FIELD_OF_VIEW)) DefaultFieldOfView       = DEFAULT_FIELD_OF_VIEW;
        }

        /// <summary>重置所有相机偏好为默认值。</summary>
        public void ResetToDefaults()
        {
            FollowSmoothTime         = DEFAULT_FOLLOW_SMOOTH_TIME;
            ShakeIntensityMultiplier = DEFAULT_SHAKE_INTENSITY_MULT;
            DefaultOrthoSize         = DEFAULT_ORTHO_SIZE;
            DefaultFieldOfView       = DEFAULT_FIELD_OF_VIEW;
            Log("相机设置已重置为默认值", Color.yellow);
        }
    }
}
