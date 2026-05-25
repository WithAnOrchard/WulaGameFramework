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
        public const string SHOP_SEED_STORE    = "shop_seed_store";
        public const string SHOP_PREMIUM_STORE = "shop_premium_store";

        public const string FOOD_CAT_FOOD  = "cat_food";
        public const string FOOD_DRIED_FISH = "dried_fish";

        public static bool IsRegistered { get; private set; }

        public static void RegisterAll(EventProcessor ep)
        {
            if (IsRegistered) return;
            IsRegistered = true;

            // 1. 注册货币物品模板（作为 Inventory 中的一种道具存储）
            ep.TriggerEventMethod("InventoryRegisterItem", new List<object>
            {
                new InventoryItem
                {
                    Id           = ShopService.CURRENCY_GOLD,
                    Name         = "金币",
                    Description  = "B 站电池礼物兑换，用于购买高价值内容。",
                    Type         = InventoryItemType.Misc,
                    Value        = 1,
                    MaxStack     = 9999,
                    IconSpriteId = ""
                }
            });
            ep.TriggerEventMethod("InventoryRegisterItem", new List<object>
            {
                new InventoryItem
                {
                    Id           = ShopService.CURRENCY_SILVER,
                    Name         = "银币",
                    Description  = "陪伴时长 / 弹幕奖励货币，用于购买日常消耗品。",
                    Type         = InventoryItemType.Misc,
                    Value        = 1,
                    MaxStack     = 9999,
                    IconSpriteId = ""
                }
            });

            // ── 食物模板 ──────────────────────────────────────────────────────
            ep.TriggerEventMethod("InventoryRegisterItem", new List<object>
            {
                new InventoryItem
                {
                    Id          = FOOD_CAT_FOOD,
                    Name        = "猫粮",
                    Description = "均衡营养猫粮，让桌宠恢复饥饿。",
                    Type        = InventoryItemType.Consumable,
                    Value       = 20,
                    MaxStack    = 99,
                }
            });
            ep.TriggerEventMethod("InventoryRegisterItem", new List<object>
            {
                new InventoryItem
                {
                    Id          = FOOD_DRIED_FISH,
                    Name        = "小鱼干",
                    Description = "桌宠最爱！让桌宠快速恢复饥饿并提升心情。",
                    Type        = InventoryItemType.Consumable,
                    Value       = 30,
                    MaxStack    = 99,
                }
            });

            // ── 金币商店道具模板 ──────────────────────────────────────────────
            foreach (var (id, name, desc) in new (string, string, string)[]
            {
                ("premium_hat_festival",  "节日帽子",     "限定节日主题帽子装扮。"),
                ("premium_food_sushi",    "寿司",         "高档食物，能大幅恢复饥饿并提升好感度。"),
                ("premium_food_cake",     "生日蛋糕",     "特别食物，好感度 +10。"),
                ("premium_action_pack",   "动作扩展包",   "解锁全新 Idle 变体动作。"),
                ("premium_celebrate_fx",  "庆祝特效",     "专属庆祝粒子特效。"),
            })
            {
                ep.TriggerEventMethod("InventoryRegisterItem", new List<object>
                {
                    new InventoryItem { Id = id, Name = name, Description = desc,
                        Type = InventoryItemType.Misc, Value = 1, MaxStack = 10 }
                });
            }

            // 2. 注册货币条目
            ep.TriggerEventMethod(ShopManager.EVT_REGISTER_CURRENCY, new List<object>
            {
                new CurrencyEntry { Id = ShopService.CURRENCY_GOLD,   DisplayName = "金币" }
            });
            ep.TriggerEventMethod(ShopManager.EVT_REGISTER_CURRENCY, new List<object>
            {
                new CurrencyEntry { Id = ShopService.CURRENCY_SILVER, DisplayName = "银币" }
            });

            // 3. 注册种子商店（银币商店：日常消耗品）
            ep.TriggerEventMethod(ShopManager.EVT_REGISTER_SHOP, new List<object>
            {
                new ShopConfig
                {
                    Id          = SHOP_SEED_STORE,
                    DisplayName = "🪙 银币商店（种子/日常）",
                    CurrencyId  = ShopService.CURRENCY_SILVER,
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
                        // ── 食物（宠物饥饿恢复）────────────────────────
                        new ShopStock { ItemId = FOOD_CAT_FOOD,  BasePrice = 20, Stock = -1 },
                        new ShopStock { ItemId = FOOD_DRIED_FISH, BasePrice = 30, Stock = -1 },
                    }
                }
            });

            // 4. 注册金币商店（高价值内容，仅能通过电池礼物获得金币购买）
            ep.TriggerEventMethod(ShopManager.EVT_REGISTER_SHOP, new List<object>
            {
                new ShopConfig
                {
                    Id          = SHOP_PREMIUM_STORE,
                    DisplayName = "💎 金币商店（稀有/限定）",
                    CurrencyId  = ShopService.CURRENCY_GOLD,
                    Policy      = new ShopPolicy { BuyMarkupRatio = 1.0f },
                    Stock       = new List<ShopStock>
                    {
                        new ShopStock { ItemId = "premium_hat_festival",   BasePrice =  50, Stock = -1 },
                        new ShopStock { ItemId = "premium_food_sushi",     BasePrice =  30, Stock = -1 },
                        new ShopStock { ItemId = "premium_food_cake",      BasePrice =  60, Stock = -1 },
                        new ShopStock { ItemId = "premium_action_pack",    BasePrice = 100, Stock = -1 },
                        new ShopStock { ItemId = "premium_celebrate_fx",   BasePrice = 150, Stock = -1 },
                    }
                }
            });

            // 5. 初始化玩家钱包：50 银 / 0 金（金币必须通过电池礼物赚取，InitWallet 幂等不重置现有余额）
            ep.TriggerEventMethod(ShopManager.EVT_INIT_WALLET, new List<object>
                { "player", ShopService.CURRENCY_SILVER, 50 });
            ep.TriggerEventMethod(ShopManager.EVT_INIT_WALLET, new List<object>
                { "player", ShopService.CURRENCY_GOLD,    0  });
        }
    }
}
