using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using EssSystem.Core.Presentation.UIManager.Entity.CommonEntity;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// DobeCatTestPanel 的 uGUI 渲染载体。
    /// 数据由静态类 <see cref="DobeCatTestPanel"/> 注入；本类负责构建窗口并暴露文本属性。
    /// </summary>
    public class DobeCatTestPanelView : MonoBehaviour
    {
        public static DobeCatTestPanelView Instance { get; private set; }

        // DAO 引用 — 赋值时自动通过 UIManager 事件链刷新显示
        private UITextComponent _statusDao;
        private UITextComponent _liveDao;
        // ScrollView 实体 — 直接调用 SetText
        private UIScrollViewEntity _detailEntity;
        private UIScrollViewEntity _logEntity;
        // 根实体 — SetActive 控制显隐
        private UIEntity _rootEntity;
        private bool     _initialized;

        // ─── 颜色 ───────────────────────────────────────────────────────────
        private static readonly Color CB   = new Color(0.11f, 0.12f, 0.15f, 0.97f);
        private static readonly Color CH   = new Color(0.07f, 0.08f, 0.11f, 1.00f);
        private static readonly Color CX   = new Color(0.70f, 0.18f, 0.18f, 1.00f);
        private static readonly Color CTM  = new Color(0.94f, 0.94f, 0.96f, 1.00f);
        private static readonly Color CTS  = new Color(0.58f, 0.61f, 0.70f, 1.00f);
        private static readonly Color CDiv = new Color(0.22f, 0.23f, 0.28f, 1.00f);
        private static readonly Color CSB  = new Color(0.08f, 0.09f, 0.11f, 1.00f);

        // ─── 公共属性 ────────────────────────────────────────────────────────
        internal string StatusText
        {
            get => _statusDao?.Text ?? "";
            set { if (_statusDao != null) _statusDao.Text = value; }
        }
        internal string LiveText
        {
            get => _liveDao?.Text ?? "";
            set { if (_liveDao != null) _liveDao.Text = value; }
        }
        internal string DetailText { set => _detailEntity?.SetText(value); get => ""; }
        internal string LogText    { set => _logEntity?.SetText(value);    get => ""; }

        public bool IsOpen => _rootEntity != null && _rootEntity.gameObject.activeSelf;

        // ─── 生命周期 ────────────────────────────────────────────────────────
        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_initialized && UIService.HasInstance)
                UIService.Instance.DestroyUIEntity("tp-root");
        }

        // ─── 公共 API ────────────────────────────────────────────────────────
        public void Show()
        {
            if (!_initialized) BuildUI();
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(true);
        }
        public void Hide() { if (_rootEntity != null) _rootEntity.gameObject.SetActive(false); }

        // ─── UI 构建（走 UIManager）─────────────────────────────────────────
        private void BuildUI()
        {
            _initialized = true;
            var canvasT = GetCanvasTransform();
            if (canvasT == null) { Debug.LogWarning("[TestPanelView] UIManager Canvas 未就绪"); return; }

            // 面板在 UIManager 画布（1920×1080 参考分辨率）的右上角
            // pos(left,top,w,h) = (left+w/2, PH-top-h/2)  ← 均以面板底左为原点
            const float PW = 420f, PH = 540f;

            // ── 根面板 ──
            var root = new UIPanelComponent("tp-root")
                .SetBackgroundColor(CB).SetSize(PW, PH)
                .SetPosition(1920f - PW / 2f - 20f, 1080f - PH / 2f - 20f);

            // ── 标题栏（整行，可拖拽）──
            var titleBar = new UIPanelComponent("tp-titlebar")
                .SetBackgroundColor(CH).SetSize(PW, 44f)
                .SetPosition(PW / 2f, PH - 22f);
            root.AddChild(titleBar);

            titleBar.AddChild(new UITextComponent("tp-title", text: "弹幕测试面板")
                .SetSize(340f, 44f).SetPosition(182f, 22f)
                .SetColor(CTM).SetFontSize(14).SetAlignment(TextAnchor.MiddleLeft));

            var closeXDao = new UIButtonComponent("tp-close-x", text: "×")
                .SetSize(40f, 44f).SetPosition(400f, 22f).SetButtonColor(CX);
            titleBar.AddChild(closeXDao);

            // ── 状态 / 直播文本 ──
            _statusDao = new UITextComponent("tp-status", text: "未连接")
                .SetSize(392f, 20f).SetPosition(PW / 2f, PH - 52f - 10f)
                .SetColor(CTM).SetFontSize(12).SetAlignment(TextAnchor.MiddleLeft);
            root.AddChild(_statusDao);

            _liveDao = new UITextComponent("tp-live", text: "开播轮询: 未启动")
                .SetSize(392f, 18f).SetPosition(PW / 2f, PH - 74f - 9f)
                .SetColor(CTS).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft);
            root.AddChild(_liveDao);

            // ── 分割线 ──
            root.AddChild(new UIPanelComponent("tp-div1")
                .SetBackgroundColor(CDiv).SetSize(PW, 1f)
                .SetPosition(PW / 2f, PH - 98f - 0.5f));

            // ── 直播详情标签 + 滚动区 ──
            root.AddChild(new UITextComponent("tp-detail-lbl", text: "直播 / 房间")
                .SetSize(392f, 15f).SetPosition(PW / 2f, PH - 103f - 7.5f)
                .SetColor(CTS).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft));

            root.AddChild(new UIScrollViewComponent("tp-detail-sv")
                .SetBackgroundColor(CSB).SetSize(392f, 125f)
                .SetPosition(PW / 2f, PH - 121f - 62.5f));

            // ── 分割线 ──
            root.AddChild(new UIPanelComponent("tp-div2")
                .SetBackgroundColor(CDiv).SetSize(PW, 1f)
                .SetPosition(PW / 2f, PH - 252f - 0.5f));

            // ── 弹幕日志标签 + 滚动区 ──
            root.AddChild(new UITextComponent("tp-log-lbl", text: "弹幕日志")
                .SetSize(392f, 15f).SetPosition(PW / 2f, PH - 257f - 7.5f)
                .SetColor(CTS).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft));

            root.AddChild(new UIScrollViewComponent("tp-log-sv")
                .SetBackgroundColor(CSB).SetSize(392f, 210f)
                .SetPosition(PW / 2f, PH - 275f - 105f));

            // ── 关闭按钮 ──
            var closeBtnDao = new UIButtonComponent("tp-close-btn", text: "关闭")
                .SetSize(392f, 32f).SetPosition(PW / 2f, PH - 495f - 16f)
                .SetButtonColor(new Color(0.18f, 0.19f, 0.24f, 1f));
            root.AddChild(closeBtnDao);

            // ── 注册到 UIManager ──
            _rootEntity = UIService.Instance.RegisterUIEntity("tp-root", root, canvasT);
            if (_rootEntity == null) return;

            // 标题栏加 UIDraggable（需要 Image.raycastTarget = true）
            var titleBarEntity = UIService.Instance.GetUIEntity("tp-titlebar");
            if (titleBarEntity != null)
            {
                var img = titleBarEntity.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                titleBarEntity.gameObject.AddComponent<UIDraggable>()
                    .DragTarget = _rootEntity.GetComponent<RectTransform>();
            }

            // 按钮回调
            closeXDao.OnClick   += _ => Hide();
            closeBtnDao.OnClick += _ => Hide();

            // ScrollView 实体引用
            _detailEntity = UIService.Instance.GetUIEntity("tp-detail-sv") as UIScrollViewEntity;
            _logEntity    = UIService.Instance.GetUIEntity("tp-log-sv")    as UIScrollViewEntity;

            // 覆盖定位：让面板锚定右上角，不随 canvas 缩放漂移
            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(PW, PH);

            _rootEntity.gameObject.SetActive(false);
        }

        private static Transform GetCanvasTransform()
        {
            if (!EventProcessor.HasInstance) return null;
            var res = EventProcessor.Instance.TriggerEventMethod(
                UIManager.EVT_GET_CANVAS_TRANSFORM, new List<object>());
            return ResultCode.IsOk(res) && res.Count >= 2 ? res[1] as Transform : null;
        }
    }
}
