using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.ShopManager;
using EssSystem.Core.Application.MultiManagers.ShopManager.Dao;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;

namespace Demo.DobeCat.Game.Shop
{
    /// <summary>
    /// DobeCat 种子商店注册器：
    /// 注册金币物品模板、货币条目、种子商店商品及价格，并初始化玩家 100 金初始资金（幂等）。
    /// </summary>
    public static class DobeCatShopSetup
    {
        public const string SHOP_SEED_STORE = "shop_seed_store";

        public static bool IsRegistered { get; private set; }

        public static void RegisterAll(EventProcessor ep)
        {
            if (IsRegistered) return;
            IsRegistered = true;

            // 1. 注册金币物品模板
            ep.TriggerEventMethod("InventoryRegisterItem", new List<object>
            {
                new InventoryItem
                {
                    Id           = ShopService.CURRENCY_GOLD,
                    Name         = "金币",
                    Description  = "通用货币，用于购买种子和道具。",
                    Type         = InventoryItemType.Misc,
                    Value        = 1,
                    MaxStack     = 9999,
                    IconSpriteId = ""
                }
            });

            // 2. 注册货币条目
            ep.TriggerEventMethod(ShopManager.EVT_REGISTER_CURRENCY, new List<object>
            {
                new CurrencyEntry { Id = ShopService.CURRENCY_GOLD, DisplayName = "金币" }
            });

            // 3. 注册种子商店（BuyMarkupRatio=1.0 = 直接按 BasePrice）
            ep.TriggerEventMethod(ShopManager.EVT_REGISTER_SHOP, new List<object>
            {
                new ShopConfig
                {
                    Id          = SHOP_SEED_STORE,
                    DisplayName = "种子商店",
                    CurrencyId  = ShopService.CURRENCY_GOLD,
                    Policy      = new ShopPolicy { BuyMarkupRatio = 1.0f },
                    Stock       = new List<ShopStock>
                    {
                        // ── 基础蔬菜（5-10金）──────────────────────
                        new ShopStock { ItemId = "seed_radish",         BasePrice =  5, Stock = -1 },
                        new ShopStock { ItemId = "seed_carrot",         BasePrice =  8, Stock = -1 },
                        new ShopStock { ItemId = "seed_turnip",         BasePrice =  8, Stock = -1 },
                        new ShopStock { ItemId = "seed_green_pepper",   BasePrice =  8, Stock = -1 },
                        new ShopStock { ItemId = "seed_bean",           BasePrice =  8, Stock = -1 },
                        new ShopStock { ItemId = "seed_wheat",          BasePrice = 10, Stock = -1 },
                        new ShopStock { ItemId = "seed_broccoli",       BasePrice = 10, Stock = -1 },
                        // ── 中级蔬菜（12-20金）─────────────────────
                        new ShopStock { ItemId = "seed_tomato",         BasePrice = 12, Stock = -1 },
                        new ShopStock { ItemId = "seed_potato",         BasePrice = 15, Stock = -1 },
                        new ShopStock { ItemId = "seed_pepper",         BasePrice = 15, Stock = -1 },
                        new ShopStock { ItemId = "seed_eggplant",       BasePrice = 18, Stock = -1 },
                        new ShopStock { ItemId = "seed_purple_cabbage", BasePrice = 20, Stock = -1 },
                        new ShopStock { ItemId = "seed_corn",           BasePrice = 20, Stock = -1 },
                        // ── 高级/水果（22-35金）────────────────────
                        new ShopStock { ItemId = "seed_pumpkin",        BasePrice = 22, Stock = -1 },
                        new ShopStock { ItemId = "seed_strawberry",     BasePrice = 25, Stock = -1 },
                        new ShopStock { ItemId = "seed_blueberry",      BasePrice = 25, Stock = -1 },
                        new ShopStock { ItemId = "seed_grape",          BasePrice = 35, Stock = -1 },
                    }
                }
            });

            // 4. 初始化玩家钱包（100 金，幂等）
            ep.TriggerEventMethod(ShopManager.EVT_INIT_WALLET, new List<object>
                { "player", ShopService.CURRENCY_GOLD, 100 });
        }
    }
}
