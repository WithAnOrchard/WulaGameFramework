using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using Demo.DobeCat.Game;
using Demo.DobeCat.Sys.UI;
using EssSystem.Core.Presentation.UIManager.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 宠物 HUD：
    /// <list type="bullet">
    /// <item>鼠标悬停宠物或停在按钮栏上时，在宠物下方显示 ICON 快捷按钮（AI 切换 / 背包）。</item>
    /// <item>手动操作模式（AI 关闭）或游戏上下文激活时，Hotbar 始终显示在宠物头顶。</item>
    /// <item>背包打开时 Hotbar 移到屏幕底部居中。</item>
    /// </list>
    /// </summary>
    public class PetHUD : MonoBehaviour
    {
        public static PetHUD Instance { get; private set; }

        private PetView         _view;
        private PetAiController _ai;

        // ── QuickBar ───────────────────────────────────────────────────
        private const string QB_ROOT    = "pet-quickbar";
        private const string QB_BTN_AI  = "pet-qb-ai";
        private const string QB_BTN_BAG = "pet-qb-bag";
        private const float  QB_SIZE    = 40f;   // 正方形图标按钮边长
        private const float  QB_GAP     = 6f;
        private const float  QB_BELOW   = 24f;   // 按钮中心距宠物底部像素
        private const float  HIDE_DELAY = 0.5f;

        private RectTransform     _qbRt;
        private UIButtonComponent _btnAi;
        private UIButtonComponent _btnBag;
        private bool              _qbBuilt;
        private float             _showTimer;

        // ── Hotbar ────────────────────────────────────────────────────
        private const float HB_NATIVE_W = 780f;
        private const float HB_NATIVE_H = 100f;
        private const float HB_GAP      = -55f;  // Hotbar 位置微调

        private RectTransform _hotbarRt;
        private bool          _hotbarShown;   // 当前 Hotbar 是否可见（由本脚本控制）
        private bool          _backpackOpen;
        private bool          _prevAiEnabled; // 检测 AI 状态变化

        private const float   HB_TRIGGER_DURATION = 2f; // 手动模式 hotbar 持续显示秒数
        private float         _hotbarTriggerTimer;      // > 0 时在手动模式下显示 Hotbar
        private int           _prevNumKeyMask;          // 上帧 1-9 按下状态（bit i = 键 i+1）
        public  static float  GlobalScrollDelta;        // 由 GlobalWheelHook 写入，PetHUD 轮询
        /// <summary>当前是否处于手动模式（AI 自主行为关闭，WASD 由 Brain PlayerControl 处理）。</summary>
        public bool IsManualMode => _ai != null && _ai.ManualMode;

        /// <summary>背包按钮触发时回调，由 DobeCatGameManager 订阅完成实际开关。</summary>
        public System.Action OnBackpackToggleRequested;

        // ── 生命周期 ──────────────────────────────────────────────────

        private void Awake()
        {
            Instance      = this;
            _view         = GetComponent<PetView>();
            _ai           = GetComponent<PetAiController>();
            _prevAiEnabled = true; // 默认 AI 自主模式（ManualMode=false）
        }

        private void Start()
        {
            DobeCatGameContext.OnContextChanged += OnContextChanged;
            DefaultUITheme.Instance.OnThemeChanged         += RebuildQuickBar;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DobeCatGameContext.OnContextChanged -= OnContextChanged;
            DefaultUITheme.Instance.OnThemeChanged         -= RebuildQuickBar;
            DestroyQuickBar();
        }

        private void OnGUI()
        {
            if (!IsManualMode) return;
            var e = Event.current;
            if (e != null && e.type == EventType.ScrollWheel)
                GlobalScrollDelta += e.delta.y;
        }

        private void OnContextChanged(bool active)
        {
            if (!active)
            {
                // 上下文关闭时，背包状态重置；Hotbar 由 Update 根据 AI 状态决定
                _backpackOpen = false;
                RefreshBagLabel();
            }
        }

        // ── 公开 API ───────────────────────────────────────────────────

        /// <summary>GameManager 在 OpenInventoryUI("hotbar") 后调用；若 PetHUD 已持有 RT 则跳过。</summary>
        public void OnHotbarOpened() => EnsureHotbarRt();

        /// <summary>背包开关状态由外部同步过来。</summary>
        public void SetBackpackOpen(bool open)
        {
            _backpackOpen = open;
            if (open && !_hotbarShown)  OpenHotbar();   // 立即打开，不等下一帧
            else if (!open && _hotbarShown && !IsManualMode && _hotbarTriggerTimer <= 0f)
                CloseHotbar();                           // 关背包且不在手动触发期内则隐藏
            RepositionHotbar();
            RefreshBagLabel();
        }

        // ── Update ────────────────────────────────────────────────────

        private void Update()
        {
            // ① 手动模式：检查 1–9 / 滚轮触发计时器
            if (IsManualMode) TryTriggerHotbarTimer();
            else _hotbarTriggerTimer = 0f; // 切回 AI 模式时清空计时器

            // ② Hotbar 可见性
            //   手动模式按键/滚轮触发 2s，或背包开着时常显（显示在屏幕底部）
            var shouldShow = (IsManualMode && _hotbarTriggerTimer > 0f) || _backpackOpen;
            if (shouldShow && !_hotbarShown)       OpenHotbar();
            else if (!shouldShow && _hotbarShown)  CloseHotbar();
            else if (shouldShow && _hotbarRt != null) RepositionHotbar();

            // ③ 手动/AI 模式切换 → 语音气泡（仅边沿触发）
            var aiNow = !IsManualMode;
            if (aiNow != _prevAiEnabled)
            {
                _prevAiEnabled = aiNow;
                RebuildQuickBar();
                var msg = aiNow ? "AI 自动巡逻已启动" : "手动模式：WASD 移动，B 开背包";
                PetSpeechBubble.Instance?.Show(msg, 3f);
            }

            // ④ QuickBar hover
            UpdateHoverVisibility();
            if (_qbRt != null && _qbRt.gameObject.activeSelf)
                UpdateQuickBarPos();
        }

        // ── Hotbar 手动模式触发 ──────────────────────────────────────

        private void TryTriggerHotbarTimer()
        {
            if (_hotbarTriggerTimer > 0f) _hotbarTriggerTimer -= Time.unscaledDeltaTime;

            var triggered = false;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // 数字键 1-9：用 bit15 (当前按下) + 边沿检测，避免 bit0 丢失问题
            var currMask = 0;
            for (var i = 0; i < 9; i++)
            {
                var main = EssSystem.Core.Platform.Windows.Win32Native.GetAsyncKeyState(0x31 + i);
                var num  = EssSystem.Core.Platform.Windows.Win32Native.GetAsyncKeyState(0x61 + i);
                if (((main | num) & unchecked((short)0x8000)) != 0)
                    currMask |= 1 << i;
            }
            // 新增按下的键（上帧未按、本帧按下）
            if ((currMask & ~_prevNumKeyMask) != 0) triggered = true;
            _prevNumKeyMask = currMask;
            // 滚轮：由 OnGUI 写入 GlobalScrollDelta
            if (!triggered && Mathf.Abs(GlobalScrollDelta) > 0.01f) triggered = true;
            GlobalScrollDelta = 0f;
#else
            for (var i = 0; i < 9 && !triggered; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)) ||
                    Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad1 + i)))
                    triggered = true;
            if (!triggered && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f) triggered = true;
#endif

            if (triggered) _hotbarTriggerTimer = HB_TRIGGER_DURATION;
        }

        // ── Hotbar 开关 ────────────────────────────────────────────────

        private void OpenHotbar()
        {
            _hotbarShown = true;
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("OpenInventoryUI",
                    new List<object> { InventoryManager.ID_HOTBAR, "Hotbar" });
            EnsureHotbarRt();
            RepositionHotbar();
        }

        private void CloseHotbar()
        {
            _hotbarShown = false;
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("CloseInventoryUI",
                    new List<object> { InventoryManager.ID_HOTBAR });
        }

        private void EnsureHotbarRt()
        {
            if (_hotbarRt != null) return;
            if (!UIService.HasInstance) return;
            var ent = UIService.Instance.GetUIEntity(InventoryManager.ID_HOTBAR);
            _hotbarRt = ent?.GetComponent<RectTransform>();
        }

        // ── Hover 检测 ────────────────────────────────────────────────

        private void UpdateHoverVisibility()
        {
            // 悬停宠物 OR 悬停 QuickBar 本身
            if (IsHoveringPet() || IsMouseOverQuickBar())
            {
                _showTimer = HIDE_DELAY;
                if (!_qbBuilt) BuildQuickBar();
                if (_qbRt != null && !_qbRt.gameObject.activeSelf)
                    _qbRt.gameObject.SetActive(true);
            }
            else if (_showTimer > 0f)
            {
                _showTimer -= Time.unscaledDeltaTime;
                if (_showTimer <= 0f && _qbRt != null)
                    _qbRt.gameObject.SetActive(false);
            }
        }

        private bool IsHoveringPet()
        {
            if (_view == null) return false;
            var cam = Camera.main;
            if (cam == null) return false;
            var sp  = GetCursorScreenPos();
            var z   = Mathf.Abs(cam.transform.position.z);
            var wld = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, z));
            var b   = _view.WorldBounds;
            // 只横向微扩展，不向下扩展，避免与 QuickBar 区域重叠产生自循环
            var padX = cam.orthographic ? cam.orthographicSize * 2f / Screen.height * 10f : 0.06f;
            b.Expand(new Vector3(padX, 0f, 0f));
            return b.Contains(new Vector3(wld.x, wld.y, b.center.z));
        }

        /// <summary>用 RectTransformUtility 精确检测鼠标是否在 QuickBar 矩形内（ScreenSpaceOverlay，camera=null）。</summary>
        private bool IsMouseOverQuickBar()
        {
            if (_qbRt == null || !_qbRt.gameObject.activeSelf) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(_qbRt, GetCursorScreenPos(), null);
        }

        // ── QuickBar 构建 ─────────────────────────────────────────────

        private void BuildQuickBar()
        {
            if (!UIService.HasInstance) return;
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) return;

            DestroyQuickBar();

            var t      = DefaultUITheme.Instance.Current;
            var totalW = QB_SIZE * 2 + QB_GAP;

            var root = new UIPanelComponent(QB_ROOT)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetSize(totalW, QB_SIZE)
                .SetPosition(-9999f, -9999f);

                        // AI 按钮：AI模式用 Accent 高亮，手动模式用普通背景
            var aiLabel = (_ai != null && !_ai.AiEnabled) ? "手" : "AI";
            var aiColor = (_ai == null || _ai.AiEnabled) ? t.Accent : t.ButtonBg;
            _btnAi = new UIButtonComponent(QB_BTN_AI, text: aiLabel)
                .SetSize(QB_SIZE, QB_SIZE)
                .SetPosition(QB_SIZE / 2f, QB_SIZE / 2f)
                .SetButtonColor(aiColor).SetFontSize(16);
            _btnAi.OnClick += _ => OnClickAI();

            // 背包按钮
            var bagLabel = _backpackOpen ? "关" : "包";
            _btnBag = new UIButtonComponent(QB_BTN_BAG, text: bagLabel)
                .SetSize(QB_SIZE, QB_SIZE)
                .SetPosition(QB_SIZE + QB_GAP + QB_SIZE / 2f, QB_SIZE / 2f)
                .SetButtonColor(t.ButtonBg).SetFontSize(16);
            _btnBag.OnClick += _ => OnClickBag();

            root.AddChild(_btnAi);
            root.AddChild(_btnBag);

            var entity = UIService.Instance.RegisterUIEntity(QB_ROOT, root, canvasT);
            if (entity == null) return;
            _qbRt    = entity.GetComponent<RectTransform>();
            _qbBuilt = true;
            entity.gameObject.SetActive(false);
        }

        private void DestroyQuickBar()
        {
            if (UIService.HasInstance) UIService.Instance.DestroyUIEntity(QB_ROOT);
            _qbRt = null; _btnAi = null; _btnBag = null; _qbBuilt = false;
        }

        private void RebuildQuickBar()
        {
            if (!_qbBuilt) return;
            var wasActive = _qbRt != null && _qbRt.gameObject.activeSelf;
            BuildQuickBar();
            if (wasActive && _qbRt != null) { _qbRt.gameObject.SetActive(true); _showTimer = HIDE_DELAY; }
        }

        private void UpdateQuickBarPos()
        {
            var bot = PetScreenPos(bottom: true);
            if (!bot.HasValue) return;
            _qbRt.position = new Vector3(bot.Value.x, bot.Value.y - QB_BELOW, 0f);
        }

        // ── 按钮回调 ──────────────────────────────────────────────────

        private void OnClickAI()
        {
            if (_ai == null) return;
            _ai.SetManualMode(!_ai.ManualMode); // Brain 保持运行，仅切换自主行为开关
            // 状态变化由 Update 的边沿检测统一处理（语音气泡 + 刷新）
        }

        private void OnClickBag() => OnBackpackToggleRequested?.Invoke();

        private void RefreshBagLabel()
        {
            if (_btnBag == null) return;
            _btnBag.Text = _backpackOpen ? "关" : "包";
        }

        // ── Hotbar 定位 ────────────────────────────────────────────────

        private void RepositionHotbar()
        {
            if (_hotbarRt == null) return;
            if (_backpackOpen)
            {
                _hotbarRt.localScale = Vector3.one;
                _hotbarRt.position   = new Vector3(Screen.width * 0.5f, HB_NATIVE_H * 0.5f + 12f, 0f);
            }
            else
            {
                var top = PetScreenPos(bottom: false);
                if (!top.HasValue) return;
                var scale = CalcHotbarScale();
                _hotbarRt.localScale = new Vector3(scale, scale, 1f);
                _hotbarRt.position   = new Vector3(
                    top.Value.x,
                    top.Value.y + HB_NATIVE_H * scale * 0.5f + HB_GAP,
                    0f);
            }
        }

        private float CalcHotbarScale()
        {
            if (_view == null) return 0.5f;
            var cam = Camera.main;
            if (cam == null) return 0.5f;
            var b = _view.WorldBounds;
            var l = cam.WorldToScreenPoint(new Vector3(b.min.x, b.center.y, b.center.z));
            var r = cam.WorldToScreenPoint(new Vector3(b.max.x, b.center.y, b.center.z));
            return Mathf.Clamp(Mathf.Abs(r.x - l.x) / HB_NATIVE_W, 0.2f, 1.2f);
        }

        // ── 工具 ──────────────────────────────────────────────────────

        private Vector2? PetScreenPos(bool bottom)
        {
            if (_view == null) return null;
            var cam = Camera.main;
            if (cam == null) return null;
            var b  = _view.WorldBounds;
            var sp = cam.WorldToScreenPoint(new Vector3(b.center.x, bottom ? b.min.y : b.max.y, b.center.z));
            return new Vector2(sp.x, sp.y);
        }

        private static Vector2 GetCursorScreenPos()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return EssSystem.Core.Platform.Windows.DesktopOverlay.GetGlobalCursorScreenPos();
#else
            return Input.mousePosition;
#endif
        }
    }
}
