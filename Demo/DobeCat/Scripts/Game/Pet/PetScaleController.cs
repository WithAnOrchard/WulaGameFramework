using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠外观大小控制器 —— 通过调整主摄像机正交 Size 改变"视野宽窄"，
    /// 从观感上等同于放大/缩小桌宠。
    /// <list type="bullet">
    /// <item>scaleFactor = 1.0 → 100%（默认大小，正交 Size = 基准值）</item>
    /// <item>scaleFactor = 2.0 → 200%（桌宠看起来更大，正交 Size 缩小一半）</item>
    /// <item>scaleFactor = 0.5 → 50%（桌宠看起来更小，正交 Size 翻倍）</item>
    /// </list>
    /// 缩放值持久化到 <see cref="PlayerPrefs"/>，键名 <c>DobeCat_PetScale</c>。
    /// </summary>
    public class PetScaleController : MonoBehaviour
    {
        private const string PrefKey = "DobeCat_PetScale";
        public const float ScaleMin = 0.5f;
        public const float ScaleMax = 3.0f;

        public static PetScaleController Instance { get; private set; }

        [Tooltip("基准正交 Size（与 DobeCatGameManager._cameraOrthoSize 保持一致）。")]
        [SerializeField] private float _baseOrthoSize = 5f;

        private float _scaleFactor = 1f;

        // ─── 公共 API ────────────────────────────────────────────────────────

        public float ScaleFactor => _scaleFactor;

        /// <summary>设置桌宠大小倍率（0.5 ~ 3.0），立即生效并持久化。</summary>
        public void SetScale(float factor)
        {
            _scaleFactor = Mathf.Clamp(factor, ScaleMin, ScaleMax);
            ApplyToCamera();
            PlayerPrefs.SetFloat(PrefKey, _scaleFactor);
            PlayerPrefs.Save();
            Debug.Log($"[PetScaleController] 桌宠大小 = {_scaleFactor * 100f:F0}%，OrthoSize = {_baseOrthoSize / _scaleFactor:F2}");
        }

        // ─── Unity 生命周期 ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _scaleFactor = PlayerPrefs.GetFloat(PrefKey, 1f);
            _scaleFactor = Mathf.Clamp(_scaleFactor, ScaleMin, ScaleMax);
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null && cam.orthographic)
                _baseOrthoSize = cam.orthographicSize;
            ApplyToCamera();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── 内部工具 ─────────────────────────────────────────────────────────

        private void ApplyToCamera()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;
            cam.orthographicSize = _baseOrthoSize / _scaleFactor;
        }
    }
}
