using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using EssSystem.Core.Presentation.UIManager.Entity.CommonEntity;
using EssSystem.Core.Presentation.UIManager.Theme;

namespace BiliBiliDanmu.UI
{
    /// <summary>
    /// 弹幕测试面板的通用 uGUI 渲染载体。
    /// <para>数据由 <see cref="DanmuTestPanel"/> 注入；本类负责构建窗口并暴露文本属性。</para>
    /// <para>使用默认颜色方案（深色主题），不依赖业务层主题系统。</para>
    /// </summary>
    public class DanmuTestPanelView : MonoBehaviour
    {
        public static DanmuTestPanelView Instance { get; private set; }

        // DAO 引用 — 赋值时自动通过 UIManager 事件链刷新显示
        private UITextComponent _statusDao;
        private UITextComponent _liveDao;
        // ScrollView 实体 — 直接调用 SetText
        private UIScrollViewEntity _detailEntity;
        private UIScrollViewEntity _logEntity;
        // 根实体 — SetActive 控制显隐
        private UIEntity _rootEntity;
        private bool     _initialized;

        // 获取当前主题颜色
        private Color GetThemeColor(System.Func<DefaultUIThemeData, Color> selector)
        {
            return selector(DefaultUITheme.Instance.Current);
        }

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
        internal string DetailText 
        { 
            set 
            { 
                if (_detailEntity == null) 
                {
                    _detailEntity = UIService.Instance?.GetUIEntity("tp-detail-sv") as UIScrollViewEntity;
                    if (_detailEntity == null)
                    {
                        Debug.LogWarning($"[DanmuTestPanelView] DetailText setter: _detailEntity 仍为 null，无法设置文本");
                        return;
                    }
                }
                Debug.Log($"[DanmuTestPanelView] DetailText setter: 设置文本，长度={value?.Length ?? 0}");
                _detailEntity.SetText(value); 
            } 
            get => ""; 
        }
        internal string LogText    
        { 
            set 
            { 
                if (_logEntity == null) 
                {
                    _logEntity = UIService.Instance?.GetUIEntity("tp-log-sv") as UIScrollViewEntity;
                    if (_logEntity == null)
                    {
                        Debug.LogWarning($"[DanmuTestPanelView] LogText setter: _logEntity 仍为 null，无法设置文本");
                        return;
                    }
                }
                Debug.Log($"[DanmuTestPanelView] LogText setter: 设置文本，长度={value?.Length ?? 0}");
                _logEntity.SetText(value);    
            } 
            get => ""; 
        }

        public bool IsOpen => _rootEntity != null && _rootEntity.gameObject.activeSelf;

        // ─── 生命周期 ────────────────────────────────────────────────────────
        private void Awake()
        {
            Instance = this;
        }

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
            if (canvasT == null) { Debug.LogWarning("[DanmuTestPanelView] UIManager Canvas 未就绪"); return; }

            const float PW = 420f, PH = 500f;

            // ── 根面板 ──
            var root = new UIPanelComponent("tp-root")
                .SetBackgroundColor(GetThemeColor(t => t.Background)).SetSize(PW, PH)
                .SetPosition(1920f - PW / 2f - 20f, 1080f - PH / 2f - 20f);

            // ── 标题栏（可拖拽）──
            var titleBar = new UIPanelComponent("tp-titlebar")
                .SetBackgroundColor(GetThemeColor(t => t.Header)).SetSize(PW, 42f)
                .SetPosition(PW / 2f, PH - 21f);
            root.AddChild(titleBar);

            // 图标（注册后手动赋 sprite）
            titleBar.AddChild(new UIPanelComponent("tp-icon")
                .SetSize(28f, 28f).SetPosition(24f, 21f)
                .SetBackgroundColor(Color.white));

            titleBar.AddChild(new UITextComponent("tp-title", text: "弹幕测试面板")
                .SetSize(300f, 42f).SetPosition(190f, 21f)
                .SetColor(GetThemeColor(t => t.TextOnHeader)).SetFontSize(13).SetAlignment(TextAnchor.MiddleLeft));

            // 关闭按钮：红色背景，缩小到半尺寸
            var closeXDao = new UIButtonComponent("tp-close-x", text: "X")
                .SetSize(22f, 22f).SetPosition(PW - 19f, 21f).SetButtonColor(GetThemeColor(t => t.Close)).SetFontSize(10);
            titleBar.AddChild(closeXDao);

            // ── 状态行 ──
            float y = PH - 42f;  // 当前光标（从顶往下）

            y -= 10f;
            _statusDao = new UITextComponent("tp-status", text: "● 未连接")
                .SetSize(392f, 18f).SetPosition(PW / 2f, y - 9f)
                .SetColor(GetThemeColor(t => t.TextMain)).SetFontSize(12).SetAlignment(TextAnchor.MiddleLeft);
            root.AddChild(_statusDao);

            y -= 22f;
            _liveDao = new UITextComponent("tp-live", text: "开播轮询: 未启动")
                .SetSize(392f, 16f).SetPosition(PW / 2f, y - 8f)
                .SetColor(GetThemeColor(t => t.TextSub)).SetFontSize(11).SetAlignment(TextAnchor.MiddleLeft);
            root.AddChild(_liveDao);

            // ── 分割线 ──
            y -= 20f;
            root.AddChild(new UIPanelComponent("tp-div1")
                .SetBackgroundColor(GetThemeColor(t => t.Divider)).SetSize(PW, 1f)
                .SetPosition(PW / 2f, y));

            // ── 直播 / 房间区 ──
            y -= 6f;
            root.AddChild(new UITextComponent("tp-detail-lbl", text: "直播 / 房间")
                .SetSize(392f, 14f).SetPosition(PW / 2f, y - 7f)
                .SetColor(GetThemeColor(t => t.TextSub)).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft));
            y -= 18f;
            const float detailH = 110f;
            root.AddChild(new UIScrollViewComponent("tp-detail-sv")
                .SetBackgroundColor(GetThemeColor(t => t.ScrollBg)).SetSize(392f, detailH)
                .SetPosition(PW / 2f, y - detailH / 2f));
            y -= detailH + 6f;

            // ── 分割线 ──
            root.AddChild(new UIPanelComponent("tp-div2")
                .SetBackgroundColor(GetThemeColor(t => t.Divider)).SetSize(PW, 1f)
                .SetPosition(PW / 2f, y));

            // ── 弹幕日志区（剩余空间全给它）──
            y -= 6f;
            root.AddChild(new UITextComponent("tp-log-lbl", text: "弹幕日志")
                .SetSize(392f, 14f).SetPosition(PW / 2f, y - 7f)
                .SetColor(GetThemeColor(t => t.TextSub)).SetFontSize(10).SetAlignment(TextAnchor.MiddleLeft));
            y -= 18f;
            var logH = y - 8f;   // 剩余到底部留 8px 边距
            root.AddChild(new UIScrollViewComponent("tp-log-sv")
                .SetBackgroundColor(GetThemeColor(t => t.ScrollBg)).SetSize(392f, logH)
                .SetPosition(PW / 2f, logH / 2f + 8f));

            // ── 注册到 UIManager ──
            _rootEntity = UIService.Instance.RegisterUIEntity("tp-root", root, canvasT);
            if (_rootEntity == null) 
            { 
                Debug.LogError("[DanmuTestPanelView] Failed to register tp-root entity");
                return;
            }

            // 标题栏图标
            var iconEnt = UIService.Instance.GetUIEntity("tp-icon") as UIPanelEntity;
            if (iconEnt != null)
            {
                var iconImg = iconEnt.GetImage();
                if (iconImg != null)
                {
                    var spr = Resources.Load<Sprite>("UI/icon");
                    if (spr == null) { var all = Resources.LoadAll<Sprite>("UI/icon"); spr = all != null && all.Length > 0 ? all[0] : null; }
                    if (spr != null) { iconImg.sprite = spr; iconImg.color = Color.white; iconImg.preserveAspect = true; }
                    iconImg.raycastTarget = false;
                }
            }

            var wb = _rootEntity.gameObject.AddComponent<UIWindowBehavior>();
            wb.EnableTopBar   = true;
            wb.TitleBarHeight = 42f; // tp-titlebar 高度

            // 关闭按钮：只剩右上角 X
            closeXDao.OnClick += _ => Hide();

            // 锚定右上角
            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(PW, PH);

            // ScrollView 实体引用（延迟一帧获取，确保所有子实体都已初始化）
            Debug.Log("[DanmuTestPanelView] 启动 GetScrollViewEntitiesDelayed 协程");
            StartCoroutine(GetScrollViewEntitiesDelayed());
        }

        /// <summary>获取 Canvas Transform。子类可重写以使用自定义 Canvas。</summary>
        protected virtual Transform GetCanvasTransform()
        {
            // 使用 OverlayCanvasProvider（ConstantPixelSize，避免字体模糊）
            return EssSystem.Core.Presentation.UIManager.OverlayCanvasProvider.GetOrCreate("DanmuTestCanvas", 50);
        }

        /// <summary>延迟一帧获取 ScrollView 实体，确保所有子实体都已初始化并注册到缓存。</summary>
        private System.Collections.IEnumerator GetScrollViewEntitiesDelayed()
        {
            yield return null; // 等待一帧
            
            _detailEntity = UIService.Instance.GetUIEntity("tp-detail-sv") as UIScrollViewEntity;
            _logEntity    = UIService.Instance.GetUIEntity("tp-log-sv")    as UIScrollViewEntity;
            
            if (_detailEntity == null) Debug.LogWarning("[DanmuTestPanelView] Failed to get tp-detail-sv entity after delay");
            if (_logEntity == null) Debug.LogWarning("[DanmuTestPanelView] Failed to get tp-log-sv entity after delay");
            
            // 如果仍然获取失败，尝试通过 Find 方式查找
            if (_logEntity == null)
            {
                var logGo = _rootEntity?.transform.Find("tp-log-sv");
                if (logGo != null)
                {
                    _logEntity = logGo.GetComponent<UIScrollViewEntity>();
                    if (_logEntity != null) Debug.Log("[DanmuTestPanelView] Found tp-log-sv via Transform.Find");
                }
            }
            
            if (_detailEntity == null)
            {
                var detailGo = _rootEntity?.transform.Find("tp-detail-sv");
                if (detailGo != null)
                {
                    _detailEntity = detailGo.GetComponent<UIScrollViewEntity>();
                    if (_detailEntity != null) Debug.Log("[DanmuTestPanelView] Found tp-detail-sv via Transform.Find");
                }
            }
        }
    }
}
