using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using Demo.DobeCat.Game;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// Settings panel view — clipboard-paste for text fields, buttons for toggles/numbers.
    /// DESIGN.md §M5 设置面板.
    /// Layout:
    ///   [天气]  API Key, City
    ///   [动态提醒] Watch UIDs
    ///   [陪伴] reminder toggles
    ///   [番茄钟] focus/break ±
    /// </summary>
    public class DobeCatSettingsPanelView : MonoBehaviour
    {
        public static DobeCatSettingsPanelView Instance { get; private set; }

        // ─── PlayerPrefs keys ────────────────────────────────────────────────
        private const string PK_BILI_UIDS    = "BiliNotifier_uids";
        private const string PK_REM_SITBREAK = "Rem_SitBreak";
        private const string PK_REM_WATER    = "Rem_Water";
        private const string PK_REM_HOURLY   = "Rem_Hourly";
        private const string PK_REM_LATENIGHT= "Rem_LateNight";
        private const string PK_POM_FOCUS    = "Pom_FocusMin";
        private const string PK_POM_BREAK    = "Pom_BreakMin";

        // ─── State ───────────────────────────────────────────────────────────
        private int   _pomFocus = 25;
        private int   _pomBreak = 5;

        // ─── UI DAO refs ─────────────────────────────────────────────────────
        private UITextComponent _weatherStatusText;
        private UITextComponent _biliUidsText;
        private UITextComponent _pomFocusText;
        private UITextComponent _pomBreakText;
        private readonly Dictionary<string, UIButtonComponent> _toggleBtns =
            new Dictionary<string, UIButtonComponent>();

        private UIEntity _rootEntity;
        private bool     _initialized;

        public bool IsOpen => _rootEntity != null && _rootEntity.gameObject.activeSelf;

        // ─── Colors ──────────────────────────────────────────────────────────
        private static readonly Color CB   = new Color(0.10f, 0.11f, 0.14f, 0.97f);
        private static readonly Color CH   = new Color(0.07f, 0.08f, 0.11f, 1.00f);
        private static readonly Color CX   = new Color(0.70f, 0.18f, 0.18f, 1.00f);
        private static readonly Color CTM  = new Color(0.94f, 0.94f, 0.96f, 1.00f);
        private static readonly Color CTS  = new Color(0.58f, 0.61f, 0.70f, 1.00f);
        private static readonly Color CDiv = new Color(0.22f, 0.23f, 0.28f, 1.00f);
        private static readonly Color CBTN = new Color(0.18f, 0.19f, 0.24f, 1.00f);
        private static readonly Color CGRN = new Color(0.18f, 0.52f, 0.30f, 1.00f);
        private static readonly Color CRED = new Color(0.50f, 0.18f, 0.18f, 1.00f);

        // ─── Unity lifecycle ─────────────────────────────────────────────────

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_initialized && UIService.HasInstance)
                UIService.Instance.DestroyUIEntity("cfg-root");
        }

        // ─── Public API ──────────────────────────────────────────────────────

        public void Show()
        {
            if (!_initialized) BuildUI();
            RefreshDisplay();
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(false);
        }

        // ─── UI Build ────────────────────────────────────────────────────────

        private void BuildUI()
        {
            _initialized = true;
            var canvasT = GetCanvasTransform();
            if (canvasT == null) { Debug.LogWarning("[SettingsPanel] UIManager Canvas 未就绪"); return; }

            _pomFocus = PlayerPrefs.GetInt(PK_POM_FOCUS, 25);
            _pomBreak = PlayerPrefs.GetInt(PK_POM_BREAK, 5);

            const float PW = 400f, PH = 560f;
            float y = PH; // running cursor from top

            var root = new UIPanelComponent("cfg-root")
                .SetBackgroundColor(CB).SetSize(PW, PH)
                .SetPosition(PW / 2f + 20f, PH / 2f + 20f);  // bottom-left area

            // ── Title bar ──
            y -= 44f;
            var titleBar = new UIPanelComponent("cfg-titlebar")
                .SetBackgroundColor(CH).SetSize(PW, 44f).SetPosition(PW / 2f, PH - 22f);
            root.AddChild(titleBar);
            titleBar.AddChild(new UITextComponent("cfg-title", text: "⚙ 设置")
                .SetSize(320f, 44f).SetPosition(172f, 22f)
                .SetColor(CTM).SetFontSize(14).SetAlignment(TextAnchor.MiddleLeft));
            var closeXBtn = new UIButtonComponent("cfg-close-x", text: "×")
                .SetSize(40f, 44f).SetPosition(380f, 22f).SetButtonColor(CX);
            titleBar.AddChild(closeXBtn);
            y = PH - 44f;

            // ── Section builder helpers (local) ──
            void SectionHeader(string id, string label)
            {
                y -= 2f;
                root.AddChild(new UIPanelComponent(id + "-div")
                    .SetBackgroundColor(CDiv).SetSize(PW, 1f).SetPosition(PW / 2f, y + 0.5f));
                y -= 22f;
                root.AddChild(new UITextComponent(id + "-hdr", text: label)
                    .SetSize(380f, 20f).SetPosition(PW / 2f, y + 10f)
                    .SetColor(CTS).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft));
                y -= 2f;
            }

            UITextComponent ClipRow(string id, string label, string pkKey,
                                    System.Action<string> onApply)
            {
                y -= 28f;
                root.AddChild(new UITextComponent(id + "-lbl", text: label)
                    .SetSize(100f, 24f).SetPosition(90f, y + 12f)
                    .SetColor(CTM).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft));
                var valText = new UITextComponent(id + "-val", text: "—")
                    .SetSize(180f, 24f).SetPosition(198f, y + 12f)
                    .SetColor(CTS).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft);
                root.AddChild(valText);
                var pasteBtn = new UIButtonComponent(id + "-paste", text: "📋 粘贴")
                    .SetSize(70f, 24f).SetPosition(333f, y + 12f).SetButtonColor(CBTN);
                root.AddChild(pasteBtn);
                pasteBtn.OnClick += _ =>
                {
                    var clip = GUIUtility.systemCopyBuffer?.Trim() ?? "";
                    if (string.IsNullOrEmpty(clip)) return;
                    PlayerPrefs.SetString(pkKey, clip);
                    onApply?.Invoke(clip);
                    valText.Text = Truncate(clip, 22);
                };
                return valText;
            }

            // ── Weather ──
            SectionHeader("cfg-sec-weather", "🌤 天气");
            y -= 28f;
            _weatherStatusText = new UITextComponent("cfg-weather-status", text: "正在获取天气...")
                .SetSize(360f, 24f).SetPosition(PW / 2f, y + 12f)
                .SetColor(CTM).SetFontSize(11).SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(_weatherStatusText);

            // ── Bili space notifier ──
            SectionHeader("cfg-sec-bili", "📺 B站动态提醒");
            _biliUidsText = ClipRow("cfg-bu", "UIDs", PK_BILI_UIDS,
                v => BiliSpaceNotifier.Instance?.SetWatchUids(v));

            // ── Companion reminders ──
            SectionHeader("cfg-sec-rem", "🔔 陪伴提醒");
            y -= 30f;
            AddToggleRow(root, "cfg-sit",   "久坐提醒",   PK_REM_SITBREAK, PW, ref y);
            AddToggleRow(root, "cfg-water", "喝水提醒",   PK_REM_WATER,    PW, ref y);
            AddToggleRow(root, "cfg-hour",  "整点报时",   PK_REM_HOURLY,   PW, ref y);
            AddToggleRow(root, "cfg-late",  "深夜劝睡",   PK_REM_LATENIGHT,PW, ref y);

            // ── Pomodoro ──
            SectionHeader("cfg-sec-pom", "🍅 番茄钟");
            y -= 30f;
            AddStepperRow(root, "cfg-pf", "专注（分钟）", ref _pomFocus, 5, 60, PW, ref y,
                () => { PlayerPrefs.SetInt(PK_POM_FOCUS, _pomFocus); RefreshPomodoro(); });
            y -= 4f;
            AddStepperRow(root, "cfg-pb", "休息（分钟）", ref _pomBreak, 1, 30, PW, ref y,
                () => { PlayerPrefs.SetInt(PK_POM_BREAK, _pomBreak); RefreshPomodoro(); });

            // ── Clear cache button ──
            y -= 14f;
            var clearBtn = new UIButtonComponent("cfg-clear-btn", text: "⚠ 清空本地缓存")
                .SetSize(PW - 24f, 32f).SetPosition(PW / 2f, y - 16f).SetButtonColor(CRED);
            root.AddChild(clearBtn);
            y -= 40f;

            // ── Close button ──
            var closeBtn = new UIButtonComponent("cfg-close-btn", text: "关闭")
                .SetSize(PW - 24f, 32f).SetPosition(PW / 2f, y - 16f).SetButtonColor(CBTN);
            root.AddChild(closeBtn);

            // ── Register ──
            _rootEntity = UIService.Instance.RegisterUIEntity("cfg-root", root, canvasT);
            if (_rootEntity == null) return;

            // Draggable title bar
            var titleEntity = UIService.Instance.GetUIEntity("cfg-titlebar");
            if (titleEntity != null)
            {
                var img = titleEntity.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                titleEntity.gameObject.AddComponent<UIDraggable>()
                    .DragTarget = _rootEntity.GetComponent<RectTransform>();
            }

            // Anchor: bottom-left
            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 20f);
            rt.sizeDelta = new Vector2(PW, PH);

            closeXBtn.OnClick  += _ => Hide();
            closeBtn.OnClick   += _ => Hide();
            clearBtn.OnClick   += _ =>
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                RefreshDisplay();
                Demo.DobeCat.Game.Pet.PetSpeechBubble.Instance?.Show("🗑 本地缓存已清空", 3f);
                Debug.Log("[SettingsPanel] PlayerPrefs 已全部清除");
            };

            _rootEntity.gameObject.SetActive(false);
        }

        private void AddToggleRow(UIPanelComponent root, string id, string label,
                                  string pk, float pw, ref float y)
        {
            var enabled = PlayerPrefs.GetInt(pk, 1) == 1;
            var btn = new UIButtonComponent(id + "-btn", text: ToggleLabel(label, enabled))
                .SetSize(pw - 24f, 26f).SetPosition(pw / 2f, y - 13f)
                .SetButtonColor(enabled ? CGRN : CRED);
            root.AddChild(btn);
            _toggleBtns[pk] = btn;
            btn.OnClick += _ =>
            {
                var cur = PlayerPrefs.GetInt(pk, 1) == 1;
                var next = !cur;
                PlayerPrefs.SetInt(pk, next ? 1 : 0);
                btn.Text = ToggleLabel(label, next);
                btn.SetButtonColor(next ? CGRN : CRED);
            };
            y -= 30f;
        }

        private void AddStepperRow(UIPanelComponent root, string id, string label,
                                   ref int valueRef, int min, int max, float pw, ref float y,
                                   System.Action onChanged)
        {
            root.AddChild(new UITextComponent(id + "-lbl", text: label)
                .SetSize(140f, 26f).SetPosition(100f, y - 13f)
                .SetColor(CTM).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft));

            var numText = new UITextComponent(id + "-num", text: valueRef.ToString())
                .SetSize(44f, 26f).SetPosition(220f, y - 13f)
                .SetColor(CTM).SetFontSize(13).SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(numText);

            var minusBtn = new UIButtonComponent(id + "-minus", text: "−")
                .SetSize(30f, 26f).SetPosition(193f, y - 13f).SetButtonColor(CBTN);
            root.AddChild(minusBtn);

            var plusBtn = new UIButtonComponent(id + "-plus", text: "+")
                .SetSize(30f, 26f).SetPosition(247f, y - 13f).SetButtonColor(CBTN);
            root.AddChild(plusBtn);

            if (id == "cfg-pf") _pomFocusText = numText;
            else                _pomBreakText = numText;

            minusBtn.OnClick += _ =>
            {
                if (id == "cfg-pf") { _pomFocus = Mathf.Max(min, _pomFocus - 5); numText.Text = _pomFocus.ToString(); }
                else                { _pomBreak = Mathf.Max(min, _pomBreak - 1); numText.Text = _pomBreak.ToString(); }
                onChanged?.Invoke();
            };
            plusBtn.OnClick += _ =>
            {
                if (id == "cfg-pf") { _pomFocus = Mathf.Min(max, _pomFocus + 5); numText.Text = _pomFocus.ToString(); }
                else                { _pomBreak = Mathf.Min(max, _pomBreak + 1); numText.Text = _pomBreak.ToString(); }
                onChanged?.Invoke();
            };

            y -= 30f;
        }

        private void RefreshDisplay()
        {
            if (_weatherStatusText != null)
            {
                var info = WeatherNotifier.LastWeatherInfo;
                var city = WeatherNotifier.DetectedCity;
                if (!string.IsNullOrEmpty(info))
                    _weatherStatusText.Text = Truncate(info, 36);
                else if (!string.IsNullOrEmpty(city))
                    _weatherStatusText.Text = $"{city} 获取中...";
                else
                    _weatherStatusText.Text = "城市检测中...";
            }
            if (_biliUidsText != null) _biliUidsText.Text = Truncate(PlayerPrefs.GetString(PK_BILI_UIDS, "（未设置）"), 22);
            if (_pomFocusText    != null) _pomFocusText.Text    = _pomFocus.ToString();
            if (_pomBreakText    != null) _pomBreakText.Text    = _pomBreak.ToString();

            foreach (var kv in _toggleBtns)
            {
                // Re-read and reset visual — buttons self-update on click, this covers initial state
            }
        }

        private static void RefreshPomodoro() { /* future: update running pomodoro if active */ }

        private static string ToggleLabel(string label, bool on) =>
            on ? $"✔ {label}" : $"  {label}（已禁用）";

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max) + "…";

        private static Transform GetCanvasTransform()
        {
            if (!EventProcessor.HasInstance) return null;
            var res = EventProcessor.Instance.TriggerEventMethod(
                UIManager.EVT_GET_CANVAS_TRANSFORM, new List<object>());
            return ResultCode.IsOk(res) && res.Count >= 2 ? res[1] as Transform : null;
        }
    }
}
