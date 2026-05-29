using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using EssSystem.Core.Presentation.UIManager.Entity.CommonEntity;
using EssSystem.Core.Presentation.UIManager.Theme;
using Demo.DobeCat.Game;
using Demo.DobeCat.Game.Live;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 礼物统计面板。支持"按礼物"和"按人"两种聚合视图，数据通过 <see cref="GiftQueryService"/> 拉取。
    /// </summary>
    public class DobeCatGiftStatsPanelView : MonoBehaviour
    {
        public static DobeCatGiftStatsPanelView Instance { get; private set; }

        // ─── 静态快捷方法 ─────────────────────────────────────────────────────────
        public static void Show() => EnsureInstance()._Show();
        public static void Hide() => EnsureInstance()._Hide();

        private static DobeCatGiftStatsPanelView EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("DobeCatGiftStatsPanelView");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<DobeCatGiftStatsPanelView>();
        }

        private UIEntity        _rootEntity;
        private UIScrollViewEntity _scrollEntity;
        private UITextComponent    _statusDao;
        private UIButtonComponent  _byGiftBtn;
        private UIButtonComponent  _byPersonBtn;
        private bool               _initialized;

        private List<GiftRecord>   _lastRecords;
        private bool               _viewByGift = true;   // true=按礼物, false=按人

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
            DestroyUI();
        }

        // ─── 公共 API ─────────────────────────────────────────────────────────

        private void _Show()
        {
            if (!_initialized) BuildUI();
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(true);
        }

        private void _Hide()
        {
            if (_rootEntity != null) _rootEntity.gameObject.SetActive(false);
        }

        // ─── 主题重建 ─────────────────────────────────────────────────────────

        private void RebuildUI()
        {
            if (!_initialized) return;
            var wasOpen = IsOpen;
            DestroyUI();
            if (wasOpen) _Show();
        }

        private void DestroyUI()
        {
            _initialized = false;
            // 直接通过 _rootEntity 引用销毁 GameObject —— UIService 缓存可能已被延迟 OnDestroy 清空
            if (_rootEntity != null && _rootEntity.gameObject != null)
                Destroy(_rootEntity.gameObject);
            if (UIService.HasInstance) UIService.Instance.DestroyUIEntity("gift-root");
            _rootEntity = null;
            _scrollEntity = null;
            _statusDao = null;
            _byGiftBtn = null;
            _byPersonBtn = null;
        }

        // ─── 构建 UI ──────────────────────────────────────────────────────────

        private void BuildUI()
        {
            _initialized = true;
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) return;

            var t = DefaultUITheme.Current;
            const float PW = 460f, PH = 580f;

            var root = new UIPanelComponent("gift-root")
                .SetBackgroundColor(t.Background).SetSize(PW, PH);
            var (titleBar, closeXDao) = BuildTitleBar(t, PW, PH);
            root.AddChild(titleBar);

            float y = PH - 44f;

            // ── Tab 行 ──
            y -= 4f;
            _byGiftBtn = new UIButtonComponent("gift-tab-gift", text: "按礼物")
                .SetSize(140f, 30f).SetPosition(100f, y - 15f)
                .SetButtonColor(t.Accent).SetFontSize(11);
            root.AddChild(_byGiftBtn);

            _byPersonBtn = new UIButtonComponent("gift-tab-person", text: "按人")
                .SetSize(140f, 30f).SetPosition(260f, y - 15f)
                .SetButtonColor(t.ButtonBg).SetFontSize(11);
            root.AddChild(_byPersonBtn);

            var fetchBtn = new UIButtonComponent("gift-fetch", text: "⟳ 查询")
                .SetSize(100f, 30f).SetPosition(410f, y - 15f)
                .SetButtonColor(t.AccentAlt).SetFontSize(11);
            root.AddChild(fetchBtn);
            y -= 38f;

            // ── 分割线 ──
            root.AddChild(new UIPanelComponent("gift-div")
                .SetBackgroundColor(t.Divider).SetSize(PW, 1f)
                .SetPosition(PW / 2f, y));
            y -= 4f;

            // ── ScrollView（剩余全部空间）──
            var svH = y - 36f;
            root.AddChild(new UIScrollViewComponent("gift-sv")
                .SetBackgroundColor(t.ScrollBg).SetSize(PW - 16f, svH)
                .SetPosition(PW / 2f, svH / 2f + 36f));

            // ── 状态栏 ──
            _statusDao = new UITextComponent("gift-status", text: "点击「查询」拉取最新礼物记录")
                .SetSize(PW - 16f, 28f).SetPosition(PW / 2f, 18f)
                .SetColor(t.TextSub).SetFontSize(10).SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(_statusDao);

            // ── 注册 ──
            _rootEntity = UIService.Instance.RegisterUIEntity("gift-root", root, canvasT);
            if (_rootEntity == null) return;

            // 标题栏图标
            var iconEnt = UIService.Instance.GetUIEntity("gift-icon") as UIPanelEntity;
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
            wb.TitleBarHeight = 44f; // gift-titlebar 高度

            _scrollEntity = UIService.Instance.GetUIEntity("gift-sv") as UIScrollViewEntity;

            // 面板居中显示
            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PW, PH);

            _rootEntity.gameObject.SetActive(false);

            // ── 事件绑定 ──
            closeXDao.OnClick += _ => Hide();
            _byGiftBtn.OnClick += _ =>
            {
                _viewByGift = true;
                _byGiftBtn.SetButtonColor(DefaultUITheme.Current.Accent);
                _byPersonBtn.SetButtonColor(DefaultUITheme.Current.ButtonBg);
                RefreshView();
            };
            _byPersonBtn.OnClick += _ =>
            {
                _viewByGift = false;
                _byGiftBtn.SetButtonColor(DefaultUITheme.Current.ButtonBg);
                _byPersonBtn.SetButtonColor(DefaultUITheme.Current.Accent);
                RefreshView();
            };
            fetchBtn.OnClick += _ => DoFetch();
        }

        private static (UIPanelComponent bar, UIButtonComponent closeX)
            BuildTitleBar(DobeCatThemeData t, float pw, float ph)
        {
            var bar = new UIPanelComponent("gift-titlebar")
                .SetBackgroundColor(t.Header).SetSize(pw, 44f)
                .SetPosition(pw / 2f, ph - 22f);

            bar.AddChild(new UIPanelComponent("gift-icon")
                .SetSize(28f, 28f).SetPosition(22f, 22f)
                .SetBackgroundColor(Color.white));

            bar.AddChild(new UITextComponent("gift-title", text: "礼物统计")
                .SetSize(300f, 44f).SetPosition(pw / 2f, 22f)
                .SetColor(t.TextOnHeader).SetFontSize(13)
                .SetAlignment(TextAnchor.MiddleCenter));

            var closeX = new UIButtonComponent("gift-close-x", text: "X")
                .SetSize(22f, 22f).SetPosition(pw - 19f, 22f)
                .SetButtonColor(t.Close).SetFontSize(10);
            bar.AddChild(closeX);
            return (bar, closeX);
        }

        // ─── 数据逻辑 ─────────────────────────────────────────────────────────

        private void DoFetch()
        {
            if (_statusDao != null) _statusDao.Text = "查询中...";
            _scrollEntity?.SetText("加载中，请稍候...");

            var svc = GiftQueryService.Instance;
            if (svc == null)
            {
                if (_statusDao != null) _statusDao.Text = "GiftQueryService 未初始化";
                return;
            }
            svc.FetchGifts(200,
                records =>
                {
                    _lastRecords = records;
                    if (_statusDao != null)
                        _statusDao.Text = $"共 {records.Count} 条记录  |  最近查询: {System.DateTime.Now:HH:mm:ss}";
                    RefreshView();
                },
                err =>
                {
                    if (_statusDao != null) _statusDao.Text = $"查询失败: {err}";
                    _scrollEntity?.SetText($"错误: {err}");
                });
        }

        private void RefreshView()
        {
            if (_scrollEntity == null || _lastRecords == null) return;
            _scrollEntity.SetText(_viewByGift
                ? GiftQueryService.FormatByGift(_lastRecords)
                : GiftQueryService.FormatByPerson(_lastRecords));
        }
    }
}
