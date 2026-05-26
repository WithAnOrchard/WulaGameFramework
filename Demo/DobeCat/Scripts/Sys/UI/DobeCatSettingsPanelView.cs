using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 设置面板：陪伴提醒开关 + 界面主题选择 + 清空缓存。
    /// 番茄钟 → 托盘「生活助手 → 番茄钟设置」独立窗口。
    /// 天气 / 闹钟 → 托盘「生活助手」子菜单。
    /// 礼物统计 / 弹幕面板 → 托盘「工具」子菜单。
    /// DESIGN.md §M5 设置面板.
    /// </summary>
    public class DobeCatSettingsPanelView : MonoBehaviour
    {
        public static DobeCatSettingsPanelView Instance { get; private set; }

        // ─── PlayerPrefs keys ────────────────────────────────────────────────
        private const string PK_REM_SITBREAK = "Rem_SitBreak";
        private const string PK_REM_WATER    = "Rem_Water";
        private const string PK_REM_HOURLY   = "Rem_Hourly";
        private const string PK_REM_LATENIGHT= "Rem_LateNight";

        // ─── UI DAO refs ─────────────────────────────────────────────────────
        private readonly Dictionary<string, UIButtonComponent> _toggleBtns =
            new Dictionary<string, UIButtonComponent>();

        private UIEntity _rootEntity;
        private bool     _initialized;

        public bool IsOpen => _rootEntity != null && _rootEntity.gameObject.activeSelf;

        // 颜色由 DobeCatTheme 提供，不再使用静态常量。

        // ─── Unity lifecycle ─────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            DobeCatTheme.OnThemeChanged += RebuildUI;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DobeCatTheme.OnThemeChanged -= RebuildUI;
            if (_initialized && UIService.HasInstance)
                UIService.Instance.DestroyUIEntity("cfg-root");
        }

        private void RebuildUI()
        {
            if (!_initialized) return;
            var wasOpen = IsOpen;
            _initialized = false;
            // 直接通过 _rootEntity 引用销毁 GameObject —— UIService 缓存可能因延迟 OnDestroy 已被清空，DestroyUIEntity 会静默 no-op
            if (_rootEntity != null && _rootEntity.gameObject != null)
                Destroy(_rootEntity.gameObject);
            if (UIService.HasInstance) UIService.Instance.DestroyUIEntity("cfg-root");
            _rootEntity = null; _toggleBtns.Clear();
            if (wasOpen) Show();
        }

        // ─── Public API ──────────────────────────────────────────────────────

        public void Show()
        {
            if (!_initialized) BuildUI();
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

            const float PW = 400f, PH = 500f;
            float y = PH; // running cursor from top

            var t    = DobeCatTheme.Current;
            var CB   = t.Background; var CH = t.Header; var CX  = t.Close;
            var CTM  = t.TextMain;   var CTS = t.TextSub; var CDiv = t.Divider;
            var CBTN = t.ButtonBg;   var CGRN = t.ButtonGreen; var CRED = t.ButtonRed;

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
                .SetColor(t.TextOnHeader).SetFontSize(14).SetAlignment(TextAnchor.MiddleLeft));
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

            // ── Companion reminders ──
            SectionHeader("cfg-sec-rem", "🔔 陪伴提醒");
            y -= 30f;
            AddToggleRow(root, "cfg-sit",   "久坐提醒",   PK_REM_SITBREAK, PW, ref y);
            AddToggleRow(root, "cfg-water", "喝水提醒",   PK_REM_WATER,    PW, ref y);
            AddToggleRow(root, "cfg-hour",  "整点报时",   PK_REM_HOURLY,   PW, ref y);
            AddToggleRow(root, "cfg-late",  "深夜劝睡",   PK_REM_LATENIGHT,PW, ref y);

            // ── Theme selector (2 rows x 4) ──
            SectionHeader("cfg-sec-theme", "🎨 界面主题");
            y -= 6f;
            const int COLS = 4;
            float themeW = (PW - 24f) / COLS;
            int totalThemes = DobeCatTheme.Presets.Length;
            int rows = Mathf.CeilToInt(totalThemes / (float)COLS);
            for (int i = 0; i < totalThemes; i++)
            {
                int idx = i;
                int row = idx / COLS;
                int col = idx % COLS;
                var isActive = DobeCatTheme.CurrentIndex == idx;
                var tbtn = new UIButtonComponent($"cfg-theme-{idx}", text: DobeCatTheme.Presets[idx].Name)
                    .SetSize(themeW - 4f, 28f)
                    .SetPosition(12f + themeW * col + themeW / 2f - 2f, y - 14f - row * 32f)
                    .SetButtonColor(isActive ? t.Accent : t.ButtonBg)
                    .SetFontSize(11);
                root.AddChild(tbtn);
                tbtn.OnClick += _ => DobeCatTheme.Apply(idx);
            }
            y -= rows * 32f + 4f;

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
            rt.sizeDelta  = new Vector2(PW, PH);

            closeXBtn.OnClick += _ => Hide();
            closeBtn.OnClick  += _ => Hide();
            clearBtn.OnClick  += _ =>
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Demo.DobeCat.Game.Pet.PetSpeechBubble.Instance?.Show("🗑 本地缓存已清空", 3f);
                Debug.Log("[SettingsPanel] PlayerPrefs 已全部清除");
            };

            _rootEntity.gameObject.SetActive(false);
        }

        private void AddToggleRow(UIPanelComponent root, string id, string label,
                                  string pk, float pw, ref float y)
        {
            var enabled = PlayerPrefs.GetInt(pk, 1) == 1;
            var tg = DobeCatTheme.Current;
            var btn = new UIButtonComponent(id + "-btn", text: ToggleLabel(label, enabled))
                .SetSize(pw - 24f, 26f).SetPosition(pw / 2f, y - 13f)
                .SetButtonColor(enabled ? tg.ButtonGreen : tg.ButtonRed);
            root.AddChild(btn);
            _toggleBtns[pk] = btn;
            btn.OnClick += _ =>
            {
                var cur = PlayerPrefs.GetInt(pk, 1) == 1;
                var next = !cur;
                PlayerPrefs.SetInt(pk, next ? 1 : 0);
                // 先更新 DAO 属性（驱动事件链）
                btn.Text = ToggleLabel(label, next);
                btn.SetButtonColor(next ? DobeCatTheme.Current.ButtonGreen : DobeCatTheme.Current.ButtonRed);
                // 再直接强制同步 Entity，确保视觉一定刷新（兜底）
                EssSystem.Core.Presentation.UIManager.Entity.UIEntity
                    .GetEntityById(btn.Id)?.SyncFromDao();
            };
            y -= 30f;
        }

        private static string ToggleLabel(string label, bool on) =>
            on ? $"✔ {label}" : $"  {label}（已禁用）";

        private static Transform GetCanvasTransform() => DobeCatCanvasProvider.GetOrCreate();
    }
}
