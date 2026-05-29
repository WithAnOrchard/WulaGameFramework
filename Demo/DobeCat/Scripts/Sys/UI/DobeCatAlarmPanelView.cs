using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using Demo.DobeCat.Game.Pet;
using UIInput = EssSystem.Core.Presentation.UIManager.Dao.CommonComponents.UIInputComponent;
using UIInputEntity = EssSystem.Core.Presentation.UIManager.Entity.CommonEntity.UIInputEntity;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 闹钟管理面板：时 / 分步进器设置时间，预设标签切换，显示全部已设闹钟，逐条删除。
    /// </summary>
    public class DobeCatAlarmPanelView : MonoBehaviour
    {
        public static DobeCatAlarmPanelView Instance { get; private set; }

        // ─── 编辑区状态（跨重建保持）─────────────────────────────────────────
        private int    _editHour   = 8;
        private int    _editMinute = 0;
        private string _editLabel  = "起床";

        // ─── UI ──────────────────────────────────────────────────────────────
        private UIEntity    _rootEntity;
        private UIInput     _hourInput;
        private UIInput     _minuteInput;
        private UIInput     _labelInput;
        private Vector2     _savedPos = new Vector2(780f, 20f);

        public bool IsOpen => _rootEntity != null && _rootEntity.gameObject.activeSelf;

        private const float PW = 420f;

        // ─── 生命周期 ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            DobeCatTheme.OnThemeChanged += OnThemeChanged;
            UIInputEntity.OnAnyInputSelected += OnInputFocused;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DobeCatTheme.OnThemeChanged -= OnThemeChanged;
            UIInputEntity.OnAnyInputSelected -= OnInputFocused;
            DestroyPanel();
        }

        private static void OnInputFocused()
        {
            Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.BringToForeground();
        }

        private void OnThemeChanged()
        {
            var wasOpen = IsOpen;
            DestroyPanel();
            if (wasOpen) BuildAndShow();
        }

        // ─── 公共 API ─────────────────────────────────────────────────────────

        public void Show()
        {
            DestroyPanel();
            BuildAndShow();
            Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.BringToForeground();
        }
        public void Hide()  { if (_rootEntity != null) _rootEntity.gameObject.SetActive(false); }
        public void Toggle(){ if (IsOpen) Hide(); else Show(); }

        // ─── 构建 ─────────────────────────────────────────────────────────────

        private void BuildAndShow()
        {
            BuildUI();
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(true);
        }

        private void DestroyPanel()
        {
            if (_rootEntity != null)
            {
                if (_rootEntity.gameObject != null)
                {
                    // 记住位置以便重建后复原
                    var rt = _rootEntity.GetComponent<UnityEngine.RectTransform>();
                    if (rt != null) _savedPos = rt.anchoredPosition;
                    Destroy(_rootEntity.gameObject);
                }
                if (UIService.HasInstance) UIService.Instance.DestroyUIEntity("alm-root");
                _rootEntity = null;
            }
            _hourInput = null; _minuteInput = null; _labelInput = null;
        }

        private void BuildUI()
        {
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) { Debug.LogWarning("[AlarmPanel] Canvas 未就绪"); return; }

            var reminder = PetCompanionReminder.Instance;
            int count    = reminder?.AlarmCount ?? 0;
            var t        = DobeCatTheme.Current;

            // ── 动态高度 ──
            const float TITLE_H = 36f;
            const float ADD_H   = 272f; // 小节标题 + 3行（时/分/标签）+ 添加按钮
            const float ROW_H   = 32f;
            const float PAD     = 10f;
            float listH  = count > 0 ? count * ROW_H : ROW_H;
            float clearH = count > 0 ? PAD + 26f : 0f;  // 「清除全部」按钮高度
            float PH = TITLE_H + ADD_H + 1f + PAD + listH + PAD + clearH + PAD;

            var root = new UIPanelComponent("alm-root")
                .SetBackgroundColor(t.Background).SetSize(PW, PH);

            // ── 标题栏 ──
            var titleBar = new UIPanelComponent("alm-titlebar")
                .SetBackgroundColor(t.Header).SetSize(PW, TITLE_H)
                .SetPosition(PW / 2f, PH - TITLE_H / 2f);
            root.AddChild(titleBar);
            titleBar.AddChild(new UITextComponent("alm-title", text: "⏰ 闹钟管理")
                .SetSize(340f, TITLE_H).SetPosition(180f, TITLE_H / 2f)
                .SetColor(t.TextOnHeader).SetFontSize(13).SetAlignment(TextAnchor.MiddleLeft));
            var closeX = new UIButtonComponent("alm-close-x", text: "×")
                .SetSize(34f, TITLE_H).SetPosition(PW - 17f, TITLE_H / 2f)
                .SetButtonColor(t.Close).SetFontSize(14);
            titleBar.AddChild(closeX);

            float y = PH - TITLE_H - PAD;

            // ── 编辑区小标题 ──
            root.AddChild(new UITextComponent("alm-add-hdr", text: "添加新闹钟")
                .SetSize(PW - 24f, 18f).SetPosition(PW / 2f, y - 9f)
                .SetColor(t.TextSub).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft));
            y -= 28f;

            // ── 行参数（固定宽度，整体居中）──
            const float ABW  = 48f;   // 箭头按钮宽
            const float IH   = 38f;   // 行高
            const float IW   = 96f;   // 输入框宽
            const float LBLW = 36f;   // 左侧标签宽
            const float GAP  = 6f;    // 标签与控件的间距
            // 控件组总宽：ABW + IW + ABW = 192，加标签 = 228，两端留白居中
            const float CTRL = ABW + IW + ABW;   // 192
            float rowLeft = (PW - LBLW - GAP - CTRL) / 2f; // 整行起始 x

            var rowBg = new UnityEngine.Color(t.ButtonBg.r, t.ButtonBg.g, t.ButtonBg.b, 0.5f);

            // ─── 辅助：构建单行 ───────────────────────────────────────────────
            void AddRow(string lblId, string lblTxt,
                        string btnLId, string inputId, string btnRId,
                        ref UIInput inputDao, string ph, string initText,
                        UIInputComponent.InputType ct, int charLim,
                        out UIButtonComponent btnL, out UIButtonComponent btnR)
            {
                float lx = rowLeft;
                root.AddChild(new UITextComponent(lblId, text: lblTxt)
                    .SetSize(LBLW, IH).SetPosition(lx + LBLW/2f, y - IH/2f)
                    .SetColor(t.TextSub).SetFontSize(11).SetAlignment(TextAnchor.MiddleRight));
                lx += LBLW + GAP;

                btnL = new UIButtonComponent(btnLId, text: "◀")
                    .SetSize(ABW, IH).SetPosition(lx + ABW/2f, y - IH/2f)
                    .SetButtonColor(t.ButtonBg).SetFontSize(14);
                lx += ABW;

                inputDao = new UIInput(inputId, ph)
                    .SetSize(IW, IH).SetPosition(lx + IW/2f, y - IH/2f)
                    .SetBgColor(rowBg).SetTextColor(t.Accent).SetFontSize(18)
                    .SetCharLimit(charLim).SetContentType(ct).SetText(initText);
                lx += IW;

                btnR = new UIButtonComponent(btnRId, text: "▶")
                    .SetSize(ABW, IH).SetPosition(lx + ABW/2f, y - IH/2f)
                    .SetButtonColor(t.ButtonBg).SetFontSize(14);

                root.AddChild(btnL); root.AddChild(inputDao); root.AddChild(btnR);
                y -= IH + 10f;
            }

            AddRow("alm-lh",  "小时",
                   "alm-hm", "alm-hv", "alm-hp",
                   ref _hourInput, "时", $"{_editHour}",
                   UIInputComponent.InputType.Integer, 2,
                   out var hMinus, out var hPlus);

            AddRow("alm-lm",  "分钟",
                   "alm-mm1", "alm-mv", "alm-mp1",
                   ref _minuteInput, "分", $"{_editMinute}",
                   UIInputComponent.InputType.Integer, 2,
                   out var mMinus1, out var mPlus1);

            // 标签行输入框颜色用 TextMain
            UIInput dummy = null;
            AddRow("alm-lbl-tag", "标签",
                   "alm-tp", "alm-tv", "alm-tn",
                   ref dummy, "输入标签…", _editLabel,
                   UIInputComponent.InputType.Standard, 10,
                   out var tagPrev, out var tagNext);
            _labelInput = dummy;
            if (_labelInput != null)
                _labelInput.SetTextColor(t.TextMain).SetFontSize(15);
            y -= 2f; // 标签行后多留 2px

            // ── 添加按钮（宽/高与输入框+箭头组一致）──
            float addBtnCX = rowLeft + LBLW + GAP + CTRL / 2f; // 与控件组水平对齐
            var addBtn = new UIButtonComponent("alm-add", text: "➕ 添加")
                .SetSize(CTRL, IH).SetPosition(addBtnCX, y - IH / 2f).SetButtonColor(t.ButtonGreen).SetFontSize(13);
            root.AddChild(addBtn);
            y -= IH + 8f;

            // ── 分隔线 ──
            root.AddChild(new UIPanelComponent("alm-div1")
                .SetBackgroundColor(t.Divider).SetSize(PW, 1f).SetPosition(PW / 2f, y));
            y -= PAD;

            // ── 闹钟列表标题 ──
            root.AddChild(new UITextComponent("alm-list-hdr",
                    text: count == 0 ? "暂无闹钟" : $"已设闹钟（{count} 条）")
                .SetSize(PW - 24f, 18f).SetPosition(PW / 2f, y - 9f)
                .SetColor(t.TextSub).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft));
            y -= 22f;

            // ── 闹钟列表 ──
            if (count == 0)
            {
                root.AddChild(new UITextComponent("alm-empty", text: "点击上方添加第一个闹钟")
                    .SetSize(PW - 24f, ROW_H).SetPosition(PW / 2f, y - ROW_H / 2f)
                    .SetColor(t.TextSub).SetFontSize(11).SetAlignment(TextAnchor.MiddleCenter));
                y -= ROW_H;
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    var (h, m, lbl) = reminder!.GetAlarm(i);
                    int ci = i;
                    string ts = $"{h:D2}:{m:D2}";
                    string dl = string.IsNullOrEmpty(lbl) ? "闹钟提醒" : lbl;

                    if (i % 2 == 0)
                        root.AddChild(new UIPanelComponent($"alm-rbg{i}")
                            .SetBackgroundColor(new Color(t.ButtonBg.r, t.ButtonBg.g, t.ButtonBg.b, 0.3f))
                            .SetSize(PW - 12f, ROW_H - 2f).SetPosition(PW / 2f, y - ROW_H / 2f + 1f));

                    root.AddChild(new UITextComponent($"alm-rt{i}", text: ts)
                        .SetSize(60f, ROW_H).SetPosition(40f, y - ROW_H / 2f)
                        .SetColor(t.Accent).SetFontSize(13).SetAlignment(TextAnchor.MiddleCenter));

                    root.AddChild(new UITextComponent($"alm-rl{i}", text: dl)
                        .SetSize(PW - 130f, ROW_H).SetPosition(PW / 2f, y - ROW_H / 2f)
                        .SetColor(t.TextMain).SetFontSize(12).SetAlignment(TextAnchor.MiddleLeft));

                    var delBtn = new UIButtonComponent($"alm-del{i}", text: "✕")
                        .SetSize(28f, 26f).SetPosition(PW - 24f, y - ROW_H / 2f)
                        .SetButtonColor(t.ButtonRed).SetFontSize(10);
                    root.AddChild(delBtn);
                    delBtn.OnClick += _ =>
                    {
                        PetCompanionReminder.Instance?.RemoveAlarmAt(ci);
                        PetSpeechBubble.Instance?.Show($"已删除 {ts}", 2f);
                        Show();
                    };
                    y -= ROW_H;
                }
            }

            // ── 底部：仅当有闹钟时显示「清除全部」 ──
            if (count > 0)
            {
                y -= PAD;
                var clearBtn = new UIButtonComponent("alm-clear", text: "✕ 清除全部")
                    .SetSize(PW - 24f, 26f).SetPosition(PW / 2f, y - 13f).SetButtonColor(t.ButtonRed).SetFontSize(11);
                root.AddChild(clearBtn);
                clearBtn.OnClick += _ =>
                {
                    PetCompanionReminder.Instance?.ClearAlarms();
                    PetSpeechBubble.Instance?.Show("所有闹钟已清除", 2f);
                    Show();
                };
            }

            // ── 注册 ──
            _rootEntity = UIService.Instance.RegisterUIEntity("alm-root", root, canvasT);
            if (_rootEntity == null) return;

            var wb = _rootEntity.gameObject.AddComponent<UIWindowBehavior>();
            wb.EnableTopBar   = true;
            wb.TitleBarHeight = TITLE_H; // 36px — alm-titlebar 高度

            var rt = _rootEntity.GetComponent<UnityEngine.RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = _savedPos;
            rt.sizeDelta = new Vector2(PW, PH);

            // ── 事件绑定 ──
            closeX.OnClick += _ => Hide();

            // ── 步进按钮事件 ──
            hMinus.OnClick  += _ => { _editHour   = (_editHour   - 1 + 24) % 24; SyncInput(_hourInput,   $"{_editHour}"); };
            hPlus.OnClick   += _ => { _editHour   = (_editHour   + 1)       % 24; SyncInput(_hourInput,   $"{_editHour}"); };
            mMinus1.OnClick += _ => { _editMinute = (_editMinute - 1 + 60) % 60; SyncInput(_minuteInput, $"{_editMinute}"); };
            mPlus1.OnClick  += _ => { _editMinute = (_editMinute + 1)       % 60; SyncInput(_minuteInput, $"{_editMinute}"); };

            // 标签 ◀▶ 循环预设
            string[] presets = { "起床","早餐","午休","下班","吃药","运动","喝水","睡前" };
            int presetIdx = System.Array.IndexOf(presets, _editLabel);
            if (presetIdx < 0) presetIdx = 0;
            tagPrev.OnClick += _ => { presetIdx = (presetIdx - 1 + presets.Length) % presets.Length; _editLabel = presets[presetIdx]; SyncInput(_labelInput, _editLabel); };
            tagNext.OnClick += _ => { presetIdx = (presetIdx + 1)                  % presets.Length; _editLabel = presets[presetIdx]; SyncInput(_labelInput, _editLabel); };

            // 输入框失焦时校验并保存
            _hourInput.OnEndEdit   += v => { if (int.TryParse(v, out int h)) { _editHour   = Mathf.Clamp(h, 0, 23); SyncInput(_hourInput,   $"{_editHour}"); } };
            _minuteInput.OnEndEdit += v => { if (int.TryParse(v, out int m)) { _editMinute = Mathf.Clamp(m, 0, 59); SyncInput(_minuteInput, $"{_editMinute}"); } };
            _labelInput.OnEndEdit  += v => { if (!string.IsNullOrWhiteSpace(v)) _editLabel = v.Trim(); };

            addBtn.OnClick += _ =>
            {
                // 读取当前输入框值（可能已手动修改）
                if (int.TryParse(_hourInput?.Text, out int fh)) _editHour   = Mathf.Clamp(fh, 0, 23);
                if (int.TryParse(_minuteInput?.Text, out int fm)) _editMinute = Mathf.Clamp(fm, 0, 59);
                if (!string.IsNullOrWhiteSpace(_labelInput?.Text)) _editLabel = _labelInput.Text.Trim();

                var r = PetCompanionReminder.Instance;
                if (r == null) return;
                r.AddAlarm(_editHour, _editMinute, _editLabel);
                PetSpeechBubble.Instance?.Show($"⏰ 已添加 {_editHour:D2}:{_editMinute:D2} {_editLabel}", 3f);
                Show();
            };
        }

        private static void SyncInput(UIInput dao, string text)
        {
            if (dao == null) return;
            dao.Text = text;
            UIEntity.GetEntityById(dao.Id)?.SyncFromDao();
        }
    }
}
