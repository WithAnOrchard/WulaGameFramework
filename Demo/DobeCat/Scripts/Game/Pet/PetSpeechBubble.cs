using System.Collections.Generic;
using Demo.DobeCat.Sys;
using Demo.DobeCat.Sys.Audio;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠头顶对话气泡 —— Screen Space，通过 Camera.WorldToScreenPoint 跟随桌宠。
    /// 走 UIManager DAO，禁止直接使用 UnityEngine.UI 组件。
    /// 调用 <see cref="Show"/> 展示文字；超时自动隐藏。
    /// 可选 <see cref="ShowWithLink"/> 使气泡可点击（打开 URL）。
    /// </summary>
    public class PetSpeechBubble : MonoBehaviour
    {
        public static PetSpeechBubble Instance { get; private set; }

        [Tooltip("气泡底部锚点相对桌宠根节点的世界偏移。")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0.9f, 0f);

        [Tooltip("在屏幕像素空间叠加的额外 Y 偏移（正=上移，负=下移）。")]
        [SerializeField] private float _screenOffsetY = -45f;

        private const string BUBBLE_ID      = "pet-speech-bubble";
        private const string BUBBLE_BTN_ID  = "pet-speech-bubble-btn";
        private const string BUBBLE_TEXT_ID = "pet-speech-bubble-text";
        private const float  BW = 220f, BH = 60f;

        private UIButtonComponent _bubbleBtn;
        private UITextComponent   _msgText;
        private GameObject        _bubbleGo;
        private RectTransform     _bubbleRt;
        private Canvas            _canvas;   // UnityEngine.Canvas（非 UnityEngine.UI）
        private Transform         _petTransform;
        private string            _pendingUrl;
        private float             _timer;
        private bool              _built;
        private bool              _petVisible = true;

        /// <summary>
        /// 番茄钟专注/休息期间设为 <c>true</c>，屏蔽所有普通 <see cref="Show"/> 调用。
        /// <see cref="ShowSilent"/> 不受此标志影响，始终可以刷新倒计时文字。
        /// </summary>
        public static bool PomodoroLock { get; set; }

        // ─── 公共 API ────────────────────────────────────────────────────────

        public void SetPetTransform(Transform petTr) => _petTransform = petTr;

        public void SetPetVisible(bool visible)
        {
            _petVisible = visible;
            if (!visible) HideImmediate();
        }

        public void HideImmediate()
        {
            _pendingUrl = null;
            _timer = 0f;
            if (_bubbleBtn != null) _bubbleBtn.Interactable = false;
            if (_bubbleGo != null) _bubbleGo.SetActive(false);
        }

        /// <summary>展示气泡文字，<paramref name="duration"/> 秒后自动隐藏。番茄钟期间被屏蔽。</summary>
        public void Show(string message, float duration = 3f)
        {
            if (PomodoroLock) return;
            ShowInternal(message, null, duration);
        }

        /// <summary>展示可点击气泡。番茄钟期间被屏蔽。</summary>
        public void ShowWithLink(string message, string url, float duration = 5f)
        {
            if (PomodoroLock) return;
            ShowInternal(message, url, duration);
        }

        /// <summary>
        /// 静默刷新气泡文字（不播放 pop 音效）。
        /// 适用于倒计时场景：每隔几秒持续更新剩余时间，避免音效轰炸。
        /// </summary>
        public void ShowSilent(string message, float duration = 3f)
        {
            if (!_petVisible) return;
            if (!_built) { if (EventProcessor.HasInstance) BuildUI(); else return; }
            if (_msgText != null) _msgText.Text = message;
            _pendingUrl = null;
            if (_bubbleBtn != null) _bubbleBtn.Interactable = false;
            if (_bubbleGo != null) { _bubbleGo.SetActive(true); UpdateScreenPosition(); }
            _timer = duration;
        }

        // ─── Unity 生命周期 ───────────────────────────────────────────────────

        private void Awake() { Instance = this; }

        private void Start()
        {
            if (EventProcessor.HasInstance) BuildUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_built && EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "UnregisterUIEntity", new List<object> { BUBBLE_ID });
        }

        private void Update()
        {
            if (!_built || _bubbleGo == null || !_bubbleGo.activeSelf) return;
            UpdateScreenPosition();
            if (_timer > 0f)
            {
                _timer -= Time.unscaledDeltaTime;
                if (_timer <= 0f) _bubbleGo.SetActive(false);
            }
        }

        // ─── 内部工具 ─────────────────────────────────────────────────────────

        private void ShowInternal(string message, string url, float duration)
        {
            if (!_petVisible) return;
            if (!_built) { if (EventProcessor.HasInstance) BuildUI(); else return; }
            if (_msgText != null) _msgText.Text = message;
            _pendingUrl = url;
            if (_bubbleBtn != null) _bubbleBtn.Interactable = !string.IsNullOrEmpty(url);
            if (_bubbleGo != null)
            {
                _bubbleGo.SetActive(true);
                UpdateScreenPosition();
            }
            _timer = duration;
            PetSoundController.PlayPop();
        }

        private void UpdateScreenPosition()
        {
            var petTr = _petTransform != null ? _petTransform : transform;
            if (_bubbleRt == null || _canvas == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var screenPos = cam.WorldToScreenPoint(petTr.position + _offset);
            if (screenPos.z < 0f) return;
            var sf = _canvas.scaleFactor;
            _bubbleRt.anchoredPosition = new Vector2(screenPos.x / sf, screenPos.y / sf + _screenOffsetY);
        }

        // ─── UI 构建（走 UIManager DAO，禁止 using UnityEngine.UI）────────────

        private void BuildUI()
        {
            if (_built) return;
            _built = true;

            // 文字：2× 超采样保证清晰
            _msgText = new UITextComponent(BUBBLE_TEXT_ID, text: "")
                .SetSize(BW * 2f, BH * 2f).SetScale(0.5f, 0.5f)
                .SetPosition(BW / 2f, BH / 2f)
                .SetFontSize(28).SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleCenter);

            // 气泡按钮（背景 + 点击区域），文字为空（子 TextComponent 负责显示）
            _bubbleBtn = new UIButtonComponent(BUBBLE_BTN_ID, text: "")
                .SetSize(BW, BH).SetPosition(BW / 2f, BH / 2f)
                .SetButtonColor(new Color(0.08f, 0.08f, 0.12f, 0.93f));
            _bubbleBtn.AddChild(_msgText);
            _bubbleBtn.OnClick += _ =>
            {
                if (!string.IsNullOrEmpty(_pendingUrl)) Application.OpenURL(_pendingUrl);
                if (_bubbleGo != null) _bubbleGo.SetActive(false);
                _timer = 0f;
            };

            // 根面板（透明，仅用于定位；内含按钮）
            var rootPanel = new UIPanelComponent(BUBBLE_ID)
                .SetSize(BW, BH).SetPosition(0f, 0f)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f));
            rootPanel.AddChild(_bubbleBtn);

            var ep  = EventProcessor.Instance;
            var res = ep.TriggerEventMethod("RegisterUIEntity",
                new List<object> { BUBBLE_ID, rootPanel });
            if (!ResultCode.IsOk(res)) { _built = false; return; }

            // 缓存 GO 和 RectTransform
            var goRes = ep.TriggerEventMethod("GetUIGameObject",
                new List<object> { BUBBLE_ID });
            if (ResultCode.IsOk(goRes) && goRes.Count >= 2)
            {
                _bubbleGo = goRes[1] as GameObject;
                if (_bubbleGo != null)
                {
                    _bubbleRt = _bubbleGo.GetComponent<RectTransform>();
                    if (_bubbleRt != null)
                    {
                        // 锚定底中：气泡底部跟随世界→屏幕投影点
                        _bubbleRt.anchorMin = _bubbleRt.anchorMax = Vector2.zero;
                        _bubbleRt.pivot     = new Vector2(0.5f, 0f);
                    }
                }
            }

            // 缓存 Canvas.scaleFactor（UnityEngine.Canvas，非 UnityEngine.UI）
            var cvRes = ep.TriggerEventMethod("GetUICanvasTransform", new List<object>());
            if (ResultCode.IsOk(cvRes) && cvRes.Count >= 2)
            {
                var canvasT = cvRes[1] as Transform;
                if (canvasT != null) _canvas = canvasT.GetComponent<Canvas>();
            }

            if (_bubbleGo != null) _bubbleGo.SetActive(false);
        }
    }
}
