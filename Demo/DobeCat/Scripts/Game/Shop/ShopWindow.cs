using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Application.MultiManagers.ShopManager;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using Demo.DobeCat.Game.Farm;
using Demo.DobeCat.Sys.UI;

namespace Demo.DobeCat.Game.Shop
{
    /// <summary>
    /// UIManager 种子商店窗口。通过右键托盘菜单"商店"打开/关闭。
    /// 面板骨架走 UIService，动态商品行直接添加到 UIScrollViewEntity.ContentTransform。
    /// </summary>
    public class ShopWindow : MonoBehaviour
    {
        public static ShopWindow Instance { get; private set; }

        private UIPanelComponent      _rootPanel;
        private UITextComponent       _balanceDao;
        private UITextComponent       _msgDao;
        private RectTransform         _contentTransform;
        private UIButtonComponent     _tabSilverDao;
        private UIButtonComponent     _tabGoldDao;
        private readonly List<string> _rowIds = new List<string>();
        private float                 _msgTimer;
        private bool                  _initialized;
        private int                   _rowIdx;

        private const string CURRENCY_SILVER = "silver";
        private const string CURRENCY_GOLD   = "gold";

        // current active shop (default: silver store)
        private string _currentShopId     = DobeCatShopSetup.SHOP_SEED_STORE;
        private string _currentCurrencyId = CURRENCY_SILVER;

        // ─── 颜色 ───────────────────────────────────────────────────────────
        private static readonly Color CB   = new Color(0.11f, 0.12f, 0.15f, 0.97f);
        private static readonly Color CH   = new Color(0.07f, 0.08f, 0.11f, 1.00f);
        private static readonly Color CX   = new Color(0.70f, 0.18f, 0.18f, 1.00f);
        private static readonly Color CTM  = new Color(0.94f, 0.94f, 0.96f, 1.00f);
        private static readonly Color CTS  = new Color(0.58f, 0.61f, 0.70f, 1.00f);
        private static readonly Color CDiv = new Color(0.22f, 0.23f, 0.28f, 1.00f);
        private static readonly Color CSB  = new Color(0.08f, 0.09f, 0.11f, 1.00f);
        private static readonly Color CRow = new Color(0.16f, 0.17f, 0.21f, 1.00f);
        private static readonly Color CBuy = new Color(0.15f, 0.40f, 0.80f, 1.00f);
        private static readonly Color CTabOn  = new Color(0.30f, 0.32f, 0.40f, 1.00f); // 选中 Tab
        private static readonly Color CTabOff = new Color(0.13f, 0.14f, 0.18f, 1.00f); // 未选中 Tab

        private void Awake() { Instance = this; }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_initialized && EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "UnregisterUIEntity", new List<object> { "shop-root" });
        }

        private void Update()
        {
            if (_msgTimer > 0)
            {
                _msgTimer -= Time.deltaTime;
                if (_msgTimer <= 0 && _msgDao != null) _msgDao.Text = "";
            }
        }

        // ─── 公共 API ────────────────────────────────────────────────────────
        public void Toggle()
        {
            if (!_initialized) BuildUI();
            if (_rootPanel == null) return;
            var next = !_rootPanel.Visible;
            _rootPanel.Visible = next;
            if (next) Rebuild();
        }

        // ─── UI 构建（走 UIManager DAO，禁止 using UnityEngine.UI）────────────
        private void BuildUI()
        {
            _initialized = true;
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;

            var cvRes = ep.TriggerEventMethod("GetUICanvasTransform", new List<object>());
            if (!ResultCode.IsOk(cvRes) || cvRes.Count < 2 || cvRes[1] == null)
            {
                Debug.LogWarning("[ShopWindow] UIManager Canvas not ready");
                return;
            }

            const float PW = 360f, PH = 480f;

            // ── root panel ──
            _rootPanel = new UIPanelComponent("shop-root")
                .SetBackgroundColor(CB).SetSize(PW, PH)
                .SetPosition(PW / 2f + 60f, 1080f - PH / 2f - 40f);

            // ── title bar as Button (provides raycastTarget for UIDraggable) ──
            var titleBarBtn = new UIButtonComponent("shop-titlebar", text: "")
                .SetSize(PW, 44f).SetPosition(PW / 2f, PH - 22f)
                .SetButtonColor(CH);
            _rootPanel.AddChild(titleBarBtn);

            _tabSilverDao = new UIButtonComponent("shop-tab-silver", text: "🪙 银币商店")
                .SetSize(118f, 36f).SetPosition(65f, 22f).SetButtonColor(CTabOn);
            titleBarBtn.AddChild(_tabSilverDao);

            _tabGoldDao = new UIButtonComponent("shop-tab-gold", text: "💎 金币商店")
                .SetSize(118f, 36f).SetPosition(187f, 22f).SetButtonColor(CTabOff);
            titleBarBtn.AddChild(_tabGoldDao);

            _balanceDao = new UITextComponent("shop-balance", text: "Silver: --")
                .SetSize(78f, 44f).SetPosition(279f, 22f)
                .SetColor(new Color(1f, 0.85f, 0.2f)).SetFontSize(12)
                .SetAlignment(TextAnchor.MiddleRight);
            titleBarBtn.AddChild(_balanceDao);

            var closeXDao = new UIButtonComponent("shop-close-x", text: "×")
                .SetSize(38f, 44f).SetPosition(341f, 22f).SetButtonColor(CX);
            titleBarBtn.AddChild(closeXDao);

            // ── divider ──
            _rootPanel.AddChild(new UIPanelComponent("shop-div")
                .SetBackgroundColor(CDiv).SetSize(PW, 1f)
                .SetPosition(PW / 2f, PH - 47f - 0.5f));

            // ── scroll view ──
            _rootPanel.AddChild(new UIScrollViewComponent("shop-items-sv")
                .SetBackgroundColor(CSB).SetSize(PW - 28f, 340f)
                .SetPosition(PW / 2f, PH - 51f - 170f));

            // ── status message ──
            _msgDao = new UITextComponent("shop-msg", text: "")
                .SetSize(PW - 28f, 20f).SetPosition(PW / 2f, PH - 397f - 10f)
                .SetColor(new Color(0.3f, 0.9f, 0.4f)).SetFontSize(12)
                .SetAlignment(TextAnchor.MiddleCenter);
            _rootPanel.AddChild(_msgDao);

            // ── close button ──
            var closeBtnDao = new UIButtonComponent("shop-close-btn", text: "Close")
                .SetSize(PW - 28f, 32f).SetPosition(PW / 2f, PH - 423f - 16f)
                .SetButtonColor(new Color(0.18f, 0.19f, 0.24f, 1f));
            _rootPanel.AddChild(closeBtnDao);

            // ── register ──
            var res = ep.TriggerEventMethod("RegisterUIEntity",
                new List<object> { "shop-root", _rootPanel });
            if (!ResultCode.IsOk(res)) return;

            // title bar drag (UIButtonComponent has raycastTarget=true by default)
            var tbGoRes   = ep.TriggerEventMethod("GetUIGameObject", new List<object> { "shop-titlebar" });
            var rootGoRes = ep.TriggerEventMethod("GetUIGameObject", new List<object> { "shop-root" });
            if (ResultCode.IsOk(tbGoRes)   && tbGoRes[1]   is GameObject tbGo &&
                ResultCode.IsOk(rootGoRes) && rootGoRes[1] is GameObject rootGo)
            {
                tbGo.AddComponent<UIDraggable>().DragTarget = rootGo.GetComponent<RectTransform>();
                // override anchor to top-left
                var rt = rootGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(60f, -40f);
                rt.sizeDelta = new Vector2(PW, PH);
            }

            // callbacks
            closeXDao.OnClick   += _ => { _rootPanel.Visible = false; };
            closeBtnDao.OnClick += _ => { _rootPanel.Visible = false; };
            _tabSilverDao.OnClick += _ => SwitchTab(DobeCatShopSetup.SHOP_SEED_STORE,    CURRENCY_SILVER);
            _tabGoldDao.OnClick   += _ => SwitchTab(DobeCatShopSetup.SHOP_PREMIUM_STORE, CURRENCY_GOLD);

            // cache ContentTransform for dynamic rows
            var svGoRes = ep.TriggerEventMethod("GetUIGameObject", new List<object> { "shop-items-sv" });
            if (ResultCode.IsOk(svGoRes) && svGoRes[1] is GameObject svGo)
                _contentTransform = svGo.transform.Find("Viewport/Content") as RectTransform;

            _rootPanel.Visible = false;
        }

        // ─── 动态商品列表 ─────────────────────────────────────────────────────
        private void Rebuild()
        {
            if (_contentTransform == null)
            {
                Debug.LogWarning("[ShopWindow] ContentTransform null, check BuildUI");
                return;
            }
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;

            // clear old rows
            foreach (var id in _rowIds)
                ep.TriggerEventMethod("UnregisterUIEntity", new List<object> { id });
            _rowIds.Clear();
            _rowIdx = 0;

            // refresh balance
            if (_balanceDao != null)
            {
                var balRes = ep.TriggerEventMethod(
                    "ShopGetWallet", new List<object> { "player", _currentCurrencyId });
                int bal = ResultCode.IsOk(balRes) && balRes.Count >= 2
                    ? System.Convert.ToInt32(balRes[1]) : 0;
                var label = _currentCurrencyId == CURRENCY_SILVER ? "🪙" : "💎";
                _balanceDao.Text = $"{label} {bal}";
            }

            var shopSvc = ShopService.Instance;
            if (shopSvc == null) { AddInfoRow("Shop not initialized"); return; }
            var shop = shopSvc.GetShop(_currentShopId);
            if (shop == null) { AddInfoRow("Shop not registered"); return; }

            var invSvc = InventoryService.Instance;
            foreach (var stock in shop.Stock)
            {
                string dn = stock.ItemId;
                if (DobeCatCropSetup.SeedToCropId.TryGetValue(stock.ItemId, out var cid)
                    && DobeCatCropSetup.Configs.TryGetValue(cid, out var ccfg))
                    dn = ccfg.DisplayName + " 种子";
                else if (invSvc != null)
                {
                    var tpl = invSvc.GetTemplate(stock.ItemId);
                    if (tpl != null) dn = tpl.Name;
                }
                var owned = invSvc?.GetInventory("player")?.CountOf(stock.ItemId) ?? 0;
                AddItemRow(dn, stock.ItemId, stock.BasePrice, owned);
            }
        }

        private void AddItemRow(string displayName, string itemId, int price, int owned = 0)
        {
            if (_contentTransform == null || !EventProcessor.HasInstance) return;
            const float rowH = 36f;
            var rowId = $"shop-row-{_rowIdx++}";

            var capturedId   = itemId;
            var capturedName = displayName;

            var nameText = new UITextComponent($"{rowId}-name", text: displayName)
                .SetSize(148f, 28f).SetPosition(78f, 18f)
                .SetColor(CTM).SetFontSize(12).SetAlignment(TextAnchor.MiddleLeft);

            var ownedText = new UITextComponent($"{rowId}-owned", text: $"持有:{owned}")
                .SetSize(56f, 28f).SetPosition(184f, 18f)
                .SetColor(CTS).SetFontSize(11).SetAlignment(TextAnchor.MiddleCenter);

            var priceText = new UITextComponent($"{rowId}-price", text: $"{price}")
                .SetSize(52f, 28f).SetPosition(234f, 18f)
                .SetColor(new Color(1f, 0.85f, 0.2f)).SetFontSize(12)
                .SetAlignment(TextAnchor.MiddleCenter);

            var buyBtn = new UIButtonComponent($"{rowId}-buy", text: "购买")
                .SetSize(60f, 28f).SetPosition(292f, 18f)
                .SetButtonColor(CBuy);
            buyBtn.OnClick += _ => BuyItem(capturedId, capturedName);

            var row = new UIPanelComponent(rowId)
                .SetSize(332f, rowH).SetBackgroundColor(CRow);
            row.AddChild(nameText);
            row.AddChild(ownedText);
            row.AddChild(priceText);
            row.AddChild(buyBtn);

            var ep  = EventProcessor.Instance;
            var res = ep.TriggerEventMethod("RegisterUIEntity", new List<object> { rowId, row });
            if (!ResultCode.IsOk(res)) return;
            _rowIds.Add(rowId);

            var goRes = ep.TriggerEventMethod("GetUIGameObject", new List<object> { rowId });
            if (!ResultCode.IsOk(goRes) || goRes.Count < 2 || !(goRes[1] is GameObject rowGo)) return;

            rowGo.transform.SetParent(_contentTransform, false);
            var rt = rowGo.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowH);
        }

        private void AddInfoRow(string msg)
        {
            if (_contentTransform == null || !EventProcessor.HasInstance) return;
            const float rowH = 36f;
            var rowId = $"shop-row-{_rowIdx++}";

            var infoText = new UITextComponent(rowId, text: msg)
                .SetSize(300f, rowH).SetPosition(150f, 18f)
                .SetColor(CTS).SetFontSize(12).SetAlignment(TextAnchor.MiddleCenter);

            var ep  = EventProcessor.Instance;
            var res = ep.TriggerEventMethod("RegisterUIEntity", new List<object> { rowId, infoText });
            if (!ResultCode.IsOk(res)) return;
            _rowIds.Add(rowId);

            var goRes = ep.TriggerEventMethod("GetUIGameObject", new List<object> { rowId });
            if (!ResultCode.IsOk(goRes) || goRes.Count < 2 || !(goRes[1] is GameObject infoGo)) return;

            infoGo.transform.SetParent(_contentTransform, false);
            var rt = infoGo.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(rt.sizeDelta.x, rowH);
        }

        private void SwitchTab(string shopId, string currencyId)
        {
            if (_currentShopId == shopId) return;
            _currentShopId     = shopId;
            _currentCurrencyId = currencyId;
            if (_tabSilverDao != null)
                _tabSilverDao.ButtonColor = currencyId == CURRENCY_SILVER ? CTabOn : CTabOff;
            if (_tabGoldDao != null)
                _tabGoldDao.ButtonColor = currencyId == CURRENCY_GOLD ? CTabOn : CTabOff;
            Rebuild();
        }

        private void BuyItem(string itemId, string displayName)
        {
            if (!EventProcessor.HasInstance) return;
            var res = EventProcessor.Instance.TriggerEventMethod("ShopBuy",
                new List<object> { _currentShopId, itemId, 1, "player" });
            var ok = ResultCode.IsOk(res);
            if (_msgDao != null)
            {
                _msgDao.Text = ok
                    ? $"已购买：{displayName}"
                    : (res?.Count >= 2 ? res[1]?.ToString() : "购买失败");
                _msgDao.Color = ok ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            }
            _msgTimer = 2.5f;
            if (ok) Rebuild();
        }
    }
}
