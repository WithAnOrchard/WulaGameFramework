using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity.CommonEntity;
using EssSystem.Core.Application.MultiManagers.ShopManager;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using Demo.DobeCat.Game.Farm;
using Demo.DobeCat.Sys.UI;
using EssSystem.Core.Presentation.UIManager.Entity;

namespace Demo.DobeCat.Game.Shop
{
    /// <summary>
    /// UIManager 种子商店窗口。通过右键托盘菜单"商店"打开/关闭。
    /// 面板骨架走 UIService，动态商品行直接添加到 UIScrollViewEntity.ContentTransform。
    /// </summary>
    public class ShopWindow : MonoBehaviour
    {
        public static ShopWindow Instance { get; private set; }

        private UIEntity           _rootEntity;
        private UITextComponent    _balanceDao;
        private UITextComponent    _msgDao;
        private UIScrollViewEntity _itemsEntity;
        private UIButtonComponent  _tabSilverDao;
        private UIButtonComponent  _tabGoldDao;
        private float              _msgTimer;
        private bool               _initialized;

        // 当前选中的商店（默认银币店）
        private string _currentShopId     = DobeCatShopSetup.SHOP_SEED_STORE;
        private string _currentCurrencyId = ShopService.CURRENCY_SILVER;

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
            if (_initialized && UIService.HasInstance)
                UIService.Instance.DestroyUIEntity("shop-root");
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
            if (_rootEntity == null) return;
            var next = !_rootEntity.gameObject.activeSelf;
            _rootEntity.gameObject.SetActive(next);
            if (next) Rebuild();
        }

        // ─── UI 构建（走 UIManager）─────────────────────────────────────────
        private void BuildUI()
        {
            _initialized = true;
            var canvasT = GetCanvasTransform();
            if (canvasT == null) { Debug.LogWarning("[ShopWindow] UIManager Canvas 未就绪"); return; }

            // pos(left,top,w,h) = (left+w/2, PH-top-h/2)
            const float PW = 360f, PH = 480f;

            // ── 根面板 ──
            var root = new UIPanelComponent("shop-root")
                .SetBackgroundColor(CB).SetSize(PW, PH)
                .SetPosition(PW / 2f + 60f, 1080f - PH / 2f - 40f);

            // ── 标题栏：[银币Tab][金币Tab][余额][×] ──
            var titleBar = new UIPanelComponent("shop-titlebar")
                .SetBackgroundColor(CH).SetSize(PW, 44f)
                .SetPosition(PW / 2f, PH - 22f);
            root.AddChild(titleBar);

            // Tab 1：银币商店（10..118，宽 110）
            _tabSilverDao = new UIButtonComponent("shop-tab-silver", text: "🪙 银币商店")
                .SetSize(110f, 36f).SetPosition(65f, 22f).SetButtonColor(CTabOn);
            titleBar.AddChild(_tabSilverDao);

            // Tab 2：金币商店（124..234，宽 110）
            _tabGoldDao = new UIButtonComponent("shop-tab-gold", text: "💎 金币商店")
                .SetSize(110f, 36f).SetPosition(179f, 22f).SetButtonColor(CTabOff);
            titleBar.AddChild(_tabGoldDao);

            // 余额（240..318，宽 78）
            _balanceDao = new UITextComponent("shop-balance", text: "银币: --")
                .SetSize(78f, 44f).SetPosition(279f, 22f)
                .SetColor(new Color(1f, 0.85f, 0.2f)).SetFontSize(12)
                .SetAlignment(TextAnchor.MiddleRight);
            titleBar.AddChild(_balanceDao);

            // × 关闭（322..360，宽 38）
            var closeXDao = new UIButtonComponent("shop-close-x", text: "×")
                .SetSize(38f, 44f).SetPosition(341f, 22f).SetButtonColor(CX);
            titleBar.AddChild(closeXDao);

            // ── 分割线 ──
            root.AddChild(new UIPanelComponent("shop-div")
                .SetBackgroundColor(CDiv).SetSize(PW, 1f)
                .SetPosition(PW / 2f, PH - 47f - 0.5f));

            // ── 商品滚动区 ──
            root.AddChild(new UIScrollViewComponent("shop-items-sv")
                .SetBackgroundColor(CSB).SetSize(PW - 28f, 340f)
                .SetPosition(PW / 2f, PH - 51f - 170f));

            // ── 消息文本 ──
            _msgDao = new UITextComponent("shop-msg", text: "")
                .SetSize(PW - 28f, 20f).SetPosition(PW / 2f, PH - 397f - 10f)
                .SetColor(new Color(0.3f, 0.9f, 0.4f)).SetFontSize(12)
                .SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(_msgDao);

            // ── 关闭按钮 ──
            var closeBtnDao = new UIButtonComponent("shop-close-btn", text: "关闭")
                .SetSize(PW - 28f, 32f).SetPosition(PW / 2f, PH - 423f - 16f)
                .SetButtonColor(new Color(0.18f, 0.19f, 0.24f, 1f));
            root.AddChild(closeBtnDao);

            // ── 注册 ──
            _rootEntity = UIService.Instance.RegisterUIEntity("shop-root", root, canvasT);
            if (_rootEntity == null) return;

            // 标题栏拖拽
            var titleBarEntity = UIService.Instance.GetUIEntity("shop-titlebar");
            if (titleBarEntity != null)
            {
                var img = titleBarEntity.GetComponent<Image>();
                if (img != null) img.raycastTarget = true;
                titleBarEntity.gameObject.AddComponent<UIDraggable>()
                    .DragTarget = _rootEntity.GetComponent<RectTransform>();
            }

            // 回调
            closeXDao.OnClick   += _ => _rootEntity.gameObject.SetActive(false);
            closeBtnDao.OnClick += _ => _rootEntity.gameObject.SetActive(false);
            _tabSilverDao.OnClick += _ => SwitchTab(DobeCatShopSetup.SHOP_SEED_STORE,    ShopService.CURRENCY_SILVER);
            _tabGoldDao.OnClick   += _ => SwitchTab(DobeCatShopSetup.SHOP_PREMIUM_STORE, ShopService.CURRENCY_GOLD);

            // ScrollView 实体
            _itemsEntity = UIService.Instance.GetUIEntity("shop-items-sv") as UIScrollViewEntity;

            // 覆盖定位：左上角
            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(60f, -40f);
            rt.sizeDelta = new Vector2(PW, PH);

            _rootEntity.gameObject.SetActive(false);
        }

        // ─── 动态商品列表 ─────────────────────────────────────────────────────
        private void Rebuild()
        {
            if (_itemsEntity == null)
            {
                Debug.LogWarning("[ShopWindow] _itemsEntity 为 null，请检查 BuildUI 是否完成");
                return;
            }
            _itemsEntity.ClearContent();

            // 刷新余额（跟随当前货币）
            if (EventProcessor.HasInstance && _balanceDao != null)
            {
                var balRes = EventProcessor.Instance.TriggerEventMethod(
                    ShopManager.EVT_GET_WALLET, new List<object> { "player", _currentCurrencyId });
                int bal = ResultCode.IsOk(balRes) && balRes.Count >= 2
                    ? System.Convert.ToInt32(balRes[1]) : 0;
                var label = _currentCurrencyId == ShopService.CURRENCY_SILVER ? "银币" : "金币";
                _balanceDao.Text = $"{label}: {bal}";
            }

            var shopSvc = ShopService.Instance;
            if (shopSvc == null) { AddInfoRow("商店未初始化"); return; }
            var shop = shopSvc.GetShop(_currentShopId);
            if (shop == null) { AddInfoRow("商店未注册"); return; }

            var invSvc = InventoryService.Instance;
            foreach (var stock in shop.Stock)
            {
                string dn = stock.ItemId;
                if (DobeCatCropSetup.SeedToCropId.TryGetValue(stock.ItemId, out var cid)
                    && DobeCatCropSetup.Configs.TryGetValue(cid, out var ccfg))
                    dn = ccfg.DisplayName + "种子";
                else if (invSvc != null)
                {
                    var tpl = invSvc.GetTemplate(stock.ItemId);
                    if (tpl != null) dn = tpl.Name;
                }
                AddItemRow(dn, stock.ItemId, stock.BasePrice);
            }
            ForceLayoutRebuild();
        }

        private void AddItemRow(string displayName, string itemId, int price)
        {
            var ct = _itemsEntity.ContentTransform;
            if (ct == null) return;

            var row = new GameObject("Row");
            row.transform.SetParent(ct, false);
            var rowRt = row.AddComponent<RectTransform>(); rowRt.localScale = Vector3.one;
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 36f; le.minHeight = 36f;
            row.AddComponent<Image>().color = CRow;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlHeight = true;  hlg.childControlWidth = true;
            hlg.childForceExpandHeight = true; hlg.childForceExpandWidth = false;
            hlg.spacing = 4f; hlg.padding = new RectOffset(8, 8, 4, 4);

            MakeRowCell(row.transform, displayName, 160f, 1f, CTM,     TextAnchor.MiddleLeft);
            MakeRowCell(row.transform, $"{price} G", 64f, 0f, new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter);

            var btnGo = new GameObject("BuyBtn"); btnGo.transform.SetParent(row.transform, false);
            btnGo.AddComponent<LayoutElement>().preferredWidth = 68f;
            var btnRt = btnGo.AddComponent<RectTransform>(); btnRt.localScale = Vector3.one;
            btnGo.AddComponent<Image>().color = CBuy;
            var btn = btnGo.AddComponent<Button>(); btn.targetGraphic = btnGo.GetComponent<Image>();
            var dn = displayName; var id = itemId;
            btn.onClick.AddListener(() => BuyItem(id, dn));

            var btnTxtGo = new GameObject("Text"); btnTxtGo.transform.SetParent(btnGo.transform, false);
            var btnTxtRt = btnTxtGo.AddComponent<RectTransform>();
            btnTxtRt.anchorMin = Vector2.zero; btnTxtRt.anchorMax = Vector2.one;
            btnTxtRt.offsetMin = btnTxtRt.offsetMax = Vector2.zero; btnTxtRt.localScale = Vector3.one;
            var btnTxt = btnTxtGo.AddComponent<Text>();
            btnTxt.text = "购买×1"; btnTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnTxt.fontSize = 11; btnTxt.color = CTM; btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.raycastTarget = false;
        }

        private static void MakeRowCell(Transform parent, string text, float preferW,
            float flexW, Color c, TextAnchor align)
        {
            var go = new GameObject("Cell"); go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = preferW; le.flexibleWidth = flexW;
            var rt = go.AddComponent<RectTransform>(); rt.localScale = Vector3.one;
            var t = go.AddComponent<Text>();
            t.text = text; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12; t.color = c; t.alignment = align; t.raycastTarget = false;
        }

        private void AddInfoRow(string msg)
        {
            var ct = _itemsEntity?.ContentTransform;
            if (ct == null) return;
            var go = new GameObject("Info"); go.transform.SetParent(ct, false);
            go.AddComponent<LayoutElement>().preferredHeight = 36f;
            var rt = go.AddComponent<RectTransform>(); rt.localScale = Vector3.one;
            var t = go.AddComponent<Text>();
            t.text = msg; t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 12; t.color = CTS; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        }

        private void SwitchTab(string shopId, string currencyId)
        {
            if (_currentShopId == shopId) return;
            _currentShopId     = shopId;
            _currentCurrencyId = currencyId;
            // 高亮选中 Tab
            var img1 = UIService.Instance.GetUIEntity("shop-tab-silver")?.GetComponent<Image>();
            var img2 = UIService.Instance.GetUIEntity("shop-tab-gold")?.GetComponent<Image>();
            if (img1 != null) img1.color = currencyId == ShopService.CURRENCY_SILVER ? CTabOn : CTabOff;
            if (img2 != null) img2.color = currencyId == ShopService.CURRENCY_GOLD   ? CTabOn : CTabOff;
            Rebuild();
        }

        private void BuyItem(string itemId, string displayName)
        {
            if (!EventProcessor.HasInstance) return;
            var res = EventProcessor.Instance.TriggerEventMethod(ShopManager.EVT_BUY_ITEM,
                new List<object> { _currentShopId, itemId, 1, "player" });
            var ok = ResultCode.IsOk(res);
            if (_msgDao != null)
            {
                _msgDao.Text = ok
                    ? $"已购买 {displayName}"
                    : (res?.Count >= 2 ? res[1]?.ToString() : "购买失败");
                _msgDao.Color = ok ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.4f, 0.4f);
            }
            _msgTimer = 2.5f;
            if (ok) Rebuild();
        }

        private void ForceLayoutRebuild()
        {
            if (_itemsEntity?.ContentTransform != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(
                    _itemsEntity.ContentTransform);
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
