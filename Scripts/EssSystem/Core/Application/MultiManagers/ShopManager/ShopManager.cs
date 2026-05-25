using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.ShopManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.ShopManager
{
    /// <summary>
    /// 商店门面 —— 事件路由到 ShopService 的业务逻辑。
    /// </summary>
    [Manager(19)]
    public class ShopManager : Manager<ShopManager>
    {
        public const string EVT_REGISTER_SHOP     = ShopService.EVT_REGISTER_SHOP;
        public const string EVT_REGISTER_CURRENCY = ShopService.EVT_REGISTER_CURRENCY;
        public const string EVT_BUY_ITEM          = ShopService.EVT_BUY_ITEM;
        public const string EVT_INIT_WALLET       = ShopService.EVT_INIT_WALLET;
        public const string EVT_GET_WALLET        = ShopService.EVT_GET_WALLET;
        public const string EVT_ADD_WALLET        = ShopService.EVT_ADD_WALLET;

        public ShopService Service => ShopService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("ShopManager 初始化完成", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        [Event(EVT_REGISTER_SHOP)]
        public List<object> HandleRegisterShop(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("ShopService 未初始化");
            if (data == null || data.Count < 1 || !(data[0] is ShopConfig cfg))
                return ResultCode.Fail("参数错误：需要 [ShopConfig]");
            Service.RegisterShop(cfg);
            return ResultCode.Ok(cfg.Id);
        }

        [Event(EVT_REGISTER_CURRENCY)]
        public List<object> HandleRegisterCurrency(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("ShopService 未初始化");
            if (data == null || data.Count < 1 || !(data[0] is CurrencyEntry entry))
                return ResultCode.Fail("参数错误：需要 [CurrencyEntry]");
            Service.RegisterCurrency(entry);
            return ResultCode.Ok(entry.Id);
        }

        [Event(EVT_BUY_ITEM)]
        public List<object> HandleBuyItem(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("ShopService 未初始化");
            if (data == null || data.Count < 2) return ResultCode.Fail("参数错误：需要 [shopId, itemId, amount?, playerId?]");
            if (!(data[0] is string shopId)) return ResultCode.Fail("data[0] 需为 shopId");
            if (!(data[1] is string itemId)) return ResultCode.Fail("data[1] 需为 itemId");
            var amount   = data.Count > 2 ? System.Convert.ToInt32(data[2]) : 1;
            var playerId = data.Count > 3 && data[3] is string pid ? pid : "player";
            var err = Service.BuyItem(shopId, itemId, amount, playerId);
            return err == null ? ResultCode.Ok("购买成功") : ResultCode.Fail(err);
        }

        [Event(EVT_INIT_WALLET)]
        public List<object> HandleInitWallet(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("ShopService 未初始化");
            if (data == null || data.Count < 3) return ResultCode.Fail("参数错误：需要 [playerId, currencyId, amount]");
            var playerId   = data[0] as string;
            var currencyId = data[1] as string;
            var amount     = System.Convert.ToInt32(data[2]);
            var err = Service.InitWallet(playerId, currencyId, amount);
            return err == null ? ResultCode.Ok() : ResultCode.Fail(err);
        }

        [Event(EVT_GET_WALLET)]
        public List<object> HandleGetWallet(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("ShopService 未初始化");
            if (data == null || data.Count < 2) return ResultCode.Fail("参数错误：需要 [playerId, currencyId]");
            var balance = Service.GetWalletBalance(data[0] as string, data[1] as string);
            return ResultCode.Ok(balance);
        }

        [Event(EVT_ADD_WALLET)]
        public List<object> HandleAddWallet(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("ShopService 未初始化");
            if (data == null || data.Count < 3) return ResultCode.Fail("参数错误：需要 [playerId, currencyId, amount]");
            var playerId   = data[0] as string;
            var currencyId = data[1] as string;
            var amount     = System.Convert.ToInt32(data[2]);
            var err = Service.AddWalletBalance(playerId, currencyId, amount);
            return err == null ? ResultCode.Ok() : ResultCode.Fail(err);
        }
    }
}
