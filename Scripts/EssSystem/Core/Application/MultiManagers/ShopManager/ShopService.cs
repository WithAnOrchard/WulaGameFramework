using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.ShopManager.Dao;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.ShopManager
{
    /// <summary>
    /// 商店业务服务 —— 注册商店/货币、钱包管理、购买事务。
    /// 钱包实现：每位玩家对应一个 "wallet_{playerId}" Inventory，货币以 InventoryItem 形式存储。
    /// </summary>
    public class ShopService : Service<ShopService>
    {
        #region 数据分类

        public const string CAT_CONFIGS    = "ShopConfigs";
        public const string CAT_CURRENCIES = "Currencies";

        #endregion

        #region GC 优化

        private static readonly List<object> _tempList1 = new List<object>(2);
        private static readonly List<object> _tempList2 = new List<object>(3);
        private static readonly List<object> _tempList3 = new List<object>(1);

        #endregion

        #region 事件名称

        public const string EVT_REGISTER_SHOP     = "ShopRegister";
        public const string EVT_REGISTER_CURRENCY = "ShopRegisterCurrency";
        public const string EVT_BUY_ITEM          = "ShopBuy";
        public const string EVT_INIT_WALLET       = "ShopInitWallet";
        public const string EVT_GET_WALLET        = "ShopGetWallet";
        public const string EVT_ADD_WALLET        = "ShopAddWallet";

        #endregion

        public const string CURRENCY_GOLD   = "gold";
        public const string CURRENCY_SILVER = "silver";

        public static string WalletId(string playerId) => $"wallet_{playerId}";

        /// <summary>向玩家钱包充值（幂等地确保 wallet Inventory 存在后 Add）。</summary>
        public string AddWalletBalance(string playerId, string currencyId, int amount)
        {
            if (!EventProcessor.HasInstance) return "EventProcessor 未就绪";
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(currencyId)) return "参数不完整";
            if (amount <= 0) return null;
            var walletId = WalletId(playerId);
            var ep = EventProcessor.Instance;
            _tempList2.Clear();
            _tempList2.Add(walletId);
            _tempList2.Add($"{playerId}钱包");
            _tempList2.Add(9999);
            ep.TriggerEventMethod("InventoryCreate", _tempList2);
            _tempList2.Clear();
            _tempList2.Add(walletId);
            _tempList2.Add(currencyId);
            _tempList2.Add(amount);
            ep.TriggerEventMethod("InventoryAdd", _tempList2);
            Log($"充值: {walletId} +{amount} {currencyId}", Color.green);
            return null;
        }

        protected override void Initialize()
        {
            base.Initialize();
            Log("ShopService 初始化完成", Color.green);
        }

        // ── 注册 ──────────────────────────────────────────────────

        public void RegisterShop(ShopConfig cfg)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.Id)) return;
            SetData(CAT_CONFIGS, cfg.Id, cfg);
            Log($"注册商店: {cfg.Id} ({cfg.DisplayName})", Color.cyan);
        }

        public ShopConfig GetShop(string id) => GetData<ShopConfig>(CAT_CONFIGS, id);

        public IEnumerable<ShopConfig> GetAllShops()
        {
            foreach (var key in GetKeys(CAT_CONFIGS))
            {
                var cfg = GetShop(key);
                if (cfg != null) yield return cfg;
            }
        }

        public void RegisterCurrency(CurrencyEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return;
            SetData(CAT_CURRENCIES, entry.Id, entry);
        }

        // ── 钱包 ──────────────────────────────────────────────────

        /// <summary>初始化钱包并赠送初始余额（幂等：已有余额则跳过）。</summary>
        public string InitWallet(string playerId, string currencyId, int initialAmount)
        {
            if (!EventProcessor.HasInstance) return "EventProcessor 未就绪";
            var ep       = EventProcessor.Instance;
            var walletId = WalletId(playerId);
            _tempList2.Clear();
            _tempList2.Add(walletId);
            _tempList2.Add($"{playerId}钱包");
            _tempList2.Add(5);
            ep.TriggerEventMethod("InventoryCreate", _tempList2);
            if (GetWalletBalance(playerId, currencyId) > 0) return null;
            _tempList2.Clear();
            _tempList2.Add(walletId);
            _tempList2.Add(currencyId);
            _tempList2.Add(initialAmount);
            ep.TriggerEventMethod("InventoryAdd", _tempList2);
            Log($"钱包初始化: {walletId} +{initialAmount} {currencyId}", Color.green);
            return null;
        }

        /// <summary>查询玩家货币余额。</summary>
        public int GetWalletBalance(string playerId, string currencyId)
        {
            if (!EventProcessor.HasInstance) return 0;
            _tempList1.Clear();
            _tempList1.Add(WalletId(playerId));
            var res = EventProcessor.Instance.TriggerEventMethod("InventoryQuery", _tempList1);
            if (!ResultCode.IsOk(res) || res.Count < 2) return 0;
            var inv = res[1] as Inventory;
            return inv?.CountOf(currencyId) ?? 0;
        }

        // ── 购买 ──────────────────────────────────────────────────

        /// <summary>购买物品事务（校验 → 扣钱 → 发货 → 失败回滚）。返回 null=成功，否则为错误描述。</summary>
        public string BuyItem(string shopId, string itemId, int amount, string playerId)
        {
            if (!EventProcessor.HasInstance) return "EventProcessor 未就绪";
            var shop = GetShop(shopId);
            if (shop == null) return $"商店不存在: {shopId}";

            ShopStock stock = null;
            foreach (var s in shop.Stock)
                if (s.ItemId == itemId) { stock = s; break; }
            if (stock == null) return $"商品不存在: {itemId}";
            if (stock.Stock >= 0 && stock.Stock < amount) return "库存不足";

            int unitPrice = stock.BasePrice > 0 ? stock.BasePrice : 10;
            int totalCost = Mathf.RoundToInt(unitPrice * shop.Policy.BuyMarkupRatio * amount);
            int balance   = GetWalletBalance(playerId, shop.CurrencyId);
            if (balance < totalCost)
                return $"金币不足（需 {totalCost}，现有 {balance}）";

            var ep = EventProcessor.Instance;
            var walletId = WalletId(playerId);

            // 步骤1：扣款
            _tempList2.Clear();
            _tempList2.Add(walletId);
            _tempList2.Add(shop.CurrencyId);
            _tempList2.Add(totalCost);
            var removeResult = ep.TriggerEventMethod("InventoryRemove", _tempList2);
            string errMsg;
            if (!ResultCode.IsOk(removeResult))
            {
                errMsg = removeResult?.Count >= 2 ? removeResult[1]?.ToString() : "未知错误";
                LogWarning($"购买失败：扣款异常 - {errMsg}");
                return $"扣款失败：{errMsg}";
            }

            // 步骤2：发货
            _tempList2.Clear();
            _tempList2.Add(playerId);
            _tempList2.Add(itemId);
            _tempList2.Add(amount);
            var addResult = ep.TriggerEventMethod("InventoryAdd", _tempList2);
            if (!ResultCode.IsOk(addResult))
            {
                // 发货失败，回滚扣款
                _tempList2.Clear();
                _tempList2.Add(walletId);
                _tempList2.Add(shop.CurrencyId);
                _tempList2.Add(totalCost);
                ep.TriggerEventMethod("InventoryAdd", _tempList2);
                errMsg = addResult?.Count >= 2 ? addResult[1]?.ToString() : "未知错误";
                LogWarning($"购买回滚：发货异常，已退款 {totalCost} {shop.CurrencyId}");
                return $"发货失败：{errMsg}，已退款";
            }

            // 步骤3：扣减库存
            if (stock.Stock > 0) stock.Stock -= amount;
            Log($"购买成功: {playerId} 买 {itemId}×{amount}，花 {totalCost} {shop.CurrencyId}", Color.green);
            return null;
        }
    }
}
