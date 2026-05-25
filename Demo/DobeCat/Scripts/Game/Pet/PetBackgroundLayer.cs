using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠背景层 —— 位于桌宠下方（sortingOrder = -100），可整体开关。
    /// <para>以程序生成的纯色精灵覆盖摄像机可视区域，运行时自动跟随 Camera 正交尺寸缩放。</para>
    /// <para>外部通过 <see cref="Instance"/> 调用 <see cref="SetVisible"/> / <see cref="SetColor"/>。</para>
    /// </summary>
    public class PetBackgroundLayer : MonoBehaviour
    {
        public static PetBackgroundLayer Instance { get; private set; }

        [Tooltip("默认背景颜色。")]
        [SerializeField] private Color _defaultColor = new Color(0.15f, 0.16f, 0.20f, 0.90f);

        private SpriteRenderer _renderer;
        private Camera _cam;
        private float _lastOrthoSize;
        private float _lastAspect;

        // ─── 公共 API ────────────────────────────────────────────────────────

        public bool Visible
        {
            get => _renderer != null && _renderer.enabled;
            set { if (_renderer != null) _renderer.enabled = value; }
        }

        public void SetVisible(bool visible) => Visible = visible;

        public void SetColor(Color c) { if (_renderer != null) _renderer.color = c; }

        // ─── Unity 生命周期 ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;

            var child = new GameObject("Background");
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0f, 0f, 5f); // 正Z轴 = 摄像机后方

            _renderer = child.AddComponent<SpriteRenderer>();
            _renderer.sprite   = MakeWhiteSprite();
            _renderer.color    = _defaultColor;
            _renderer.sortingOrder = -100; // 所有层的最底层
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            _cam = Camera.main;
            FitToCamera();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            var ortho  = _cam.orthographicSize;
            var aspect = _cam.aspect;
            if (Mathf.Approximately(ortho, _lastOrthoSize) &&
                Mathf.Approximately(aspect, _lastAspect)) return;
            FitToCamera();
        }

        // ─── 内部工具 ─────────────────────────────────────────────────────────

        private void FitToCamera()
        {
            if (_cam == null || _renderer == null) return;
            _lastOrthoSize = _cam.orthographicSize;
            _lastAspect    = _cam.aspect;

            var h = _lastOrthoSize * 2f;
            var w = h * _lastAspect;
            // 精灵是 1×1 像素（1 ppu），scale = 世界单位数
            _renderer.transform.localScale = new Vector3(w, h, 1f);
        }

        private static Sprite MakeWhiteSprite()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
