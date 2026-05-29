using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using EssSystem.Core.Presentation.UIManager.Theme;
using Demo.DobeCat.Game.Pet;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 番茄钟独立设置窗口：专注分钟 / 休息分钟 步进器 + 启动 / 停止 按钮。
    /// 之前嵌在 DobeCatSettingsPanelView 内，现拆分为可独立打开 / 关闭的小窗。
    /// </summary>
    public class DobeCatPomodoroPanelView : MonoBehaviour
    {
        public static DobeCatPomodoroPanelView Instance { get; private set; }

        // ─── 静态快捷方法（替代 DobeCatPomodoroPanel 包装类）─────────────────────
        public static void Toggle() => EnsureView()._Toggle();
        public static void Open()   => EnsureView().Show();
        public static void Close()  => EnsureView().Hide();

        private static DobeCatPomodoroPanelView EnsureView()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("DobeCatPomodoroPanelView");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<DobeCatPomodoroPanelView>();
        }

        private const string PK_POM_FOCUS = "Pom_FocusMin";
        private const string PK_POM_BREAK = "Pom_BreakMin";

        private int _focus = 25;
        private int _break = 5;

        private UITextComponent _focusText;
        private UITextComponent _breakText;
        private UIButtonComponent _startBtn;

        private UIEntity _rootEntity;
        private bool _initialized;

        public bool IsOpen => _rootEntity != null && _rootEntity.gameObject.activeSelf;

        private void Awake()
        {
            Instance = this;
            DefaultUITheme.OnThemeChanged += RebuildUI;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DefaultUITheme.OnThemeChanged -= RebuildUI;
            if (_initialized && UIService.HasInstance)
                UIService.Instance.DestroyUIEntity("pom-root");
        }

        private void RebuildUI()
        {
            if (!_initialized) return;
            var wasOpen = IsOpen;
            _initialized = false;
            if (_rootEntity != null && _rootEntity.gameObject != null)
                Destroy(_rootEntity.gameObject);
            if (UIService.HasInstance) UIService.Instance.DestroyUIEntity("pom-root");
            _rootEntity = null; _focusText = null; _breakText = null; _startBtn = null;
            if (wasOpen) Show();
        }

        public void Show()
        {
            if (!_initialized) BuildUI();
            RefreshStartBtn();
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(false);
        }

        private void _Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        private void BuildUI()
        {
            _initialized = true;
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) { Debug.LogWarning("[PomodoroPanel] Canvas 未就绪"); return; }

            _focus = PlayerPrefs.GetInt(PK_POM_FOCUS, 25);
            _break = PlayerPrefs.GetInt(PK_POM_BREAK, 5);

            const float PW = 320f, PH = 220f;
            var t = DefaultUITheme.Instance.Current;

            var root = new UIPanelComponent("pom-root")
                .SetBackgroundColor(t.Background).SetSize(PW, PH)
                .SetPosition(PW / 2f + 440f, PH / 2f + 20f);

            // 标题栏
            var titleBar = new UIPanelComponent("pom-titlebar")
                .SetBackgroundColor(t.Header).SetSize(PW, 36f).SetPosition(PW / 2f, PH - 18f);
            root.AddChild(titleBar);
            titleBar.AddChild(new UITextComponent("pom-title", text: "🍅 番茄钟")
                .SetSize(260f, 36f).SetPosition(140f, 18f)
                .SetColor(t.TextOnHeader).SetFontSize(13).SetAlignment(TextAnchor.MiddleLeft));
            var closeX = new UIButtonComponent("pom-close-x", text: "×")
                .SetSize(34f, 36f).SetPosition(PW - 17f, 18f).SetButtonColor(t.Close).SetFontSize(14);
            titleBar.AddChild(closeX);

            float y = PH - 36f;

            // 专注
            y -= 16f;
            root.AddChild(new UITextComponent("pom-fl", text: "专注（分钟）")
                .SetSize(120f, 24f).SetPosition(80f, y - 12f)
                .SetColor(t.TextMain).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft));
            _focusText = new UITextComponent("pom-fn", text: _focus.ToString())
                .SetSize(44f, 24f).SetPosition(180f, y - 12f)
                .SetColor(t.TextMain).SetFontSize(13).SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(_focusText);
            var fMinus = new UIButtonComponent("pom-fm", text: "−")
                .SetSize(28f, 24f).SetPosition(152f, y - 12f).SetButtonColor(t.ButtonBg);
            var fPlus = new UIButtonComponent("pom-fp", text: "+")
                .SetSize(28f, 24f).SetPosition(208f, y - 12f).SetButtonColor(t.ButtonBg);
            root.AddChild(fMinus); root.AddChild(fPlus);
            y -= 32f;

            // 休息
            root.AddChild(new UITextComponent("pom-bl", text: "休息（分钟）")
                .SetSize(120f, 24f).SetPosition(80f, y - 12f)
                .SetColor(t.TextMain).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft));
            _breakText = new UITextComponent("pom-bn", text: _break.ToString())
                .SetSize(44f, 24f).SetPosition(180f, y - 12f)
                .SetColor(t.TextMain).SetFontSize(13).SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(_breakText);
            var bMinus = new UIButtonComponent("pom-bm", text: "−")
                .SetSize(28f, 24f).SetPosition(152f, y - 12f).SetButtonColor(t.ButtonBg);
            var bPlus = new UIButtonComponent("pom-bp", text: "+")
                .SetSize(28f, 24f).SetPosition(208f, y - 12f).SetButtonColor(t.ButtonBg);
            root.AddChild(bMinus); root.AddChild(bPlus);
            y -= 40f;

            // 启动 / 停止按钮
            _startBtn = new UIButtonComponent("pom-start", text: "🍅 启动番茄钟")
                .SetSize(PW - 32f, 34f).SetPosition(PW / 2f, y - 17f)
                .SetButtonColor(t.ButtonGreen);
            root.AddChild(_startBtn);
            y -= 42f;

            // 关闭按钮
            var closeBtn = new UIButtonComponent("pom-close", text: "关闭")
                .SetSize(PW - 32f, 28f).SetPosition(PW / 2f, y - 14f)
                .SetButtonColor(t.ButtonBg);
            root.AddChild(closeBtn);

            _rootEntity = UIService.Instance.RegisterUIEntity("pom-root", root, canvasT);
            if (_rootEntity == null) return;

            var wb = _rootEntity.gameObject.AddComponent<UIWindowBehavior>();
            wb.EnableTopBar   = true;
            wb.TitleBarHeight = 36f; // pom-titlebar 高度

            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(440f, 20f);
            rt.sizeDelta = new Vector2(PW, PH);

            // 事件
            fMinus.OnClick += _ => { _focus = Mathf.Max(5,  _focus - 5);  _focusText.Text = _focus.ToString(); PlayerPrefs.SetInt(PK_POM_FOCUS, _focus); };
            fPlus.OnClick  += _ => { _focus = Mathf.Min(60, _focus + 5);  _focusText.Text = _focus.ToString(); PlayerPrefs.SetInt(PK_POM_FOCUS, _focus); };
            bMinus.OnClick += _ => { _break = Mathf.Max(1,  _break - 1);  _breakText.Text = _break.ToString(); PlayerPrefs.SetInt(PK_POM_BREAK, _break); };
            bPlus.OnClick  += _ => { _break = Mathf.Min(30, _break + 1);  _breakText.Text = _break.ToString(); PlayerPrefs.SetInt(PK_POM_BREAK, _break); };

            _startBtn.OnClick += _ =>
            {
                var r = PetCompanionReminder.Instance;
                if (r == null) return;
                if (r.PomodoroActive) r.StopPomodoro();
                else r.StartPomodoro(_focus, _break);
                RefreshStartBtn();
            };
            closeBtn.OnClick += _ => Hide();
            closeX.OnClick   += _ => Hide();

            _rootEntity.gameObject.SetActive(false);
        }

        private void RefreshStartBtn()
        {
            if (_startBtn == null) return;
            var t = DefaultUITheme.Instance.Current;
            var active = PetCompanionReminder.Instance?.PomodoroActive ?? false;
            _startBtn.Text = active ? "⏹ 取消番茄钟" : "🍅 启动番茄钟";
            _startBtn.SetButtonColor(active ? t.ButtonRed : t.ButtonGreen);
            UIEntity.GetEntityById(_startBtn.Id)?.SyncFromDao();
        }
    }
}
