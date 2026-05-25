using System.Collections.Generic;
using EssSystem.Core.Application.MultiManagers.FarmManager;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;
using EssSystem.Core.Base.Event;

namespace Demo.DobeCat.Game.Farm
{
    /// <summary>
    /// DobeCat 农场作物配置注册器（共 18 种作物）。
    /// 精灵图集：Assets/Demo/DobeCat/Resources/Sprites/Plants/Plants.png
    /// <list type="bullet">
    /// <item>蔬菜：胡萝卜、大头菜、西蓝花、青辣椒、萝卜、豆角、番茄、辣椒、茄子、玉米、土豆</item>
    /// <item>水果：草莓、蓝莓、葡萄、南瓜</item>
    /// <item>其他：小麦、紫甘蓝、红萝卜</item>
    /// <item>StageSpriteIds 对应各作物生长阶段，数量因作物而异</item>
    /// </list>
    /// </summary>
    public static class DobeCatCropSetup
    {
        /// <summary>CropConfigId → CropConfig 本地缓存（供 FarmWorldController 快速查精灵）。</summary>
        public static readonly Dictionary<string, CropConfig> Configs
            = new Dictionary<string, CropConfig>();

        /// <summary>SeedItemId → CropConfigId 映射（供点击种植时选择作物）。</summary>
        public static readonly Dictionary<string, string> SeedToCropId
            = new Dictionary<string, string>();

        public static bool IsRegistered { get; private set; }

        /// <summary>一次性注册所有作物配置和物品模板，幂等。</summary>
        public static void RegisterAll(EventProcessor ep)
        {
            if (IsRegistered) return;
            IsRegistered = true;

            // 声明本模块使用的精灵图集 → ResourceService 按名缓存所有子精灵
            ep.TriggerEventMethod("RegisterSpriteSheet", new List<object> { "Sprites/Plants/Plants" });

            // ── 胡萝卜 ────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_carrot",
                DisplayName  = "胡萝卜",
                SeedItemId   = "seed_carrot",
                OutputItemId = "carrot",
                OutputAmount = 2,
                StageDurations = new List<float> { 30f, 60f, 90f, 300f },
                StageSpriteIds = new List<string>
                    { "Plants_44", "Plants_46", "Plants_47", "Plants_50", "Plants_51" }
            });
            RegisterItem(ep, "seed_carrot", "胡萝卜种子", "Plants_167");
            RegisterItem(ep, "carrot",      "胡萝卜",     "Plants_50");

            // ── 西蓝花 ────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_broccoli",
                DisplayName  = "西蓝花",
                SeedItemId   = "seed_broccoli",
                OutputItemId = "broccoli",
                OutputAmount = 1,
                StageDurations = new List<float> { 35f, 70f, 105f, 350f },
                StageSpriteIds = new List<string>
                    { "Plants_16", "Plants_17", "Plants_18", "Plants_19", "Plants_20" }
            });
            RegisterItem(ep, "seed_broccoli", "西蓝花种子", "Plants_163");
            RegisterItem(ep, "broccoli",      "西蓝花",     "Plants_21");

            // ── 青辣椒 ────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_green_pepper",
                DisplayName  = "青辣椒",
                SeedItemId   = "seed_green_pepper",
                OutputItemId = "green_pepper",
                OutputAmount = 2,
                StageDurations = new List<float> { 20f, 40f, 60f, 90f, 180f },
                StageSpriteIds = new List<string>
                    { "Plants_5", "Plants_6", "Plants_7", "Plants_8", "Plants_9", "Plants_10" }
            });
            RegisterItem(ep, "seed_green_pepper", "青辣椒种子", "Plants_162");
            RegisterItem(ep, "green_pepper",      "青辣椒",     "Plants_15");

            // ── 萝卜 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_radish",
                DisplayName  = "萝卜",
                SeedItemId   = "seed_radish",
                OutputItemId = "radish",
                OutputAmount = 1,
                StageDurations = new List<float> { 30f, 60f, 90f, 300f },
                StageSpriteIds = new List<string>
                    { "Plants_32", "Plants_22", "Plants_23", "Plants_24", "Plants_25" }
            });
            RegisterItem(ep, "seed_radish", "萝卜种子", "Plants_176");
            RegisterItem(ep, "radish",      "萝卜",     "Plants_2");



            // ── 豆角 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_bean",
                DisplayName  = "豆角",
                SeedItemId   = "seed_bean",
                OutputItemId = "bean",
                OutputAmount = 3,
                StageDurations = new List<float> { 22f, 44f, 66f, 220f },
                StageSpriteIds = new List<string>
                    { "Plants_53", "Plants_54", "Plants_55", "Plants_57", "Plants_58" }
            });
            RegisterItem(ep, "seed_bean", "豆角种子", "Plants_162");
            RegisterItem(ep, "bean",      "豆角",     "Plants_57");

            // ── 番茄 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_tomato",
                DisplayName  = "番茄",
                SeedItemId   = "seed_tomato",
                OutputItemId = "tomato",
                OutputAmount = 2,
                StageDurations = new List<float> { 35f, 70f, 105f, 350f },
                StageSpriteIds = new List<string>
                    { "Plants_72", "Plants_60", "Plants_61", "Plants_62", "Plants_63" }
            });
            RegisterItem(ep, "seed_tomato", "番茄种子", "Plants_172");
            RegisterItem(ep, "tomato",      "番茄",     "Plants_62");

            // ── 辣椒 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_pepper",
                DisplayName  = "辣椒",
                SeedItemId   = "seed_pepper",
                OutputItemId = "pepper",
                OutputAmount = 2,
                StageDurations = new List<float> { 30f, 60f, 90f, 300f },
                StageSpriteIds = new List<string>
                    { "Plants_65", "Plants_66", "Plants_67", "Plants_69", "Plants_70" }
            });
            RegisterItem(ep, "seed_pepper", "辣椒种子", "Plants_174");
            RegisterItem(ep, "pepper",      "辣椒",     "Plants_69");

            // ── 茄子 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_eggplant",
                DisplayName  = "茄子",
                SeedItemId   = "seed_eggplant",
                OutputItemId = "eggplant",
                OutputAmount = 1,
                StageDurations = new List<float> { 40f, 80f, 120f, 400f },
                StageSpriteIds = new List<string>
                    { "Plants_93", "Plants_94", "Plants_95", "Plants_97", "Plants_96" }
            });
            RegisterItem(ep, "seed_eggplant", "茄子种子", "Plants_184");
            RegisterItem(ep, "eggplant",      "茄子",     "Plants_97");

            // ── 玉米 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_corn",
                DisplayName  = "玉米",
                SeedItemId   = "seed_corn",
                OutputItemId = "corn",
                OutputAmount = 2,
                StageDurations = new List<float> { 45f, 90f, 135f, 450f },
                StageSpriteIds = new List<string>
                    { "Plants_109", "Plants_110", "Plants_99", "Plants_101", "Plants_102" }
            });
            RegisterItem(ep, "seed_corn", "玉米种子", "Plants_179");
            RegisterItem(ep, "corn",      "玉米",     "Plants_160");

            // ── 草莓 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_strawberry",
                DisplayName  = "草莓",
                SeedItemId   = "seed_strawberry",
                OutputItemId = "strawberry",
                OutputAmount = 3,
                StageDurations = new List<float> { 30f, 60f, 90f, 300f },
                StageSpriteIds = new List<string>
                    { "Plants_111", "Plants_112", "Plants_113", "Plants_115", "Plants_114" }
            });
            RegisterItem(ep, "seed_strawberry", "草莓种子", "Plants_190");
            RegisterItem(ep, "strawberry",       "草莓",     "Plants_115");

            // ── 蓝莓 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_blueberry",
                DisplayName  = "蓝莓",
                SeedItemId   = "seed_blueberry",
                OutputItemId = "blueberry",
                OutputAmount = 4,
                StageDurations = new List<float> { 25f, 50f, 75f, 250f },
                StageSpriteIds = new List<string>
                    { "Plants_73", "Plants_74", "Plants_75", "Plants_77", "Plants_76" }
            });
            RegisterItem(ep, "seed_blueberry", "蓝莓种子", "Plants_173");
            RegisterItem(ep, "blueberry",       "蓝莓",     "Plants_77");

            // ── 葡萄 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_grape",
                DisplayName  = "葡萄",
                SeedItemId   = "seed_grape",
                OutputItemId = "grape",
                OutputAmount = 5,
                StageDurations = new List<float> { 50f, 100f, 150f, 500f },
                StageSpriteIds = new List<string>
                    { "Plants_122", "Plants_123", "Plants_116", "Plants_78", "Plants_212" }
            });
            RegisterItem(ep, "seed_grape", "葡萄种子", "Plants_197");
            RegisterItem(ep, "grape",       "葡萄",     "Plants_78");

            // ── 南瓜 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_pumpkin",
                DisplayName  = "南瓜",
                SeedItemId   = "seed_pumpkin",
                OutputItemId = "pumpkin",
                OutputAmount = 1,
                StageDurations = new List<float> { 60f, 120f, 180f, 600f },
                StageSpriteIds = new List<string>
                    { "Plants_83", "Plants_3", "Plants_4", "Plants_13", "Plants_202" }
            });
            RegisterItem(ep, "seed_pumpkin", "南瓜种子", "Plants_186");
            RegisterItem(ep, "pumpkin",       "南瓜",     "Plants_202");

            // ── 小麦 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_wheat",
                DisplayName  = "小麦",
                SeedItemId   = "seed_wheat",
                OutputItemId = "wheat",
                OutputAmount = 3,
                StageDurations = new List<float> { 30f, 60f, 90f, 300f },
                StageSpriteIds = new List<string>
                    { "Plants_8", "Plants_3", "Plants_4", "Plants_13", "Plants_79" }
            });
            RegisterItem(ep, "seed_wheat", "小麦种子", "Plants_175");
            RegisterItem(ep, "wheat",      "小麦",     "Plants_79");

            // ── 紫甘蓝 ────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_purple_cabbage",
                DisplayName  = "紫甘蓝",
                SeedItemId   = "seed_purple_cabbage",
                OutputItemId = "purple_cabbage",
                OutputAmount = 1,
                StageDurations = new List<float> { 28f, 56f, 84f, 280f },
                StageSpriteIds = new List<string>
                    { "Plants_90", "Plants_91", "Plants_92", "Plants_88", "Plants_14" }
            });
            RegisterItem(ep, "seed_purple_cabbage", "紫甘蓝种子", "Plants_177");
            RegisterItem(ep, "purple_cabbage",       "紫甘蓝",     "Plants_89");

            // ── 土豆 ──────────────────────────────────────────────
            RegisterCrop(ep, new CropConfig
            {
                Id           = "crop_potato",
                DisplayName  = "土豆",
                SeedItemId   = "seed_potato",
                OutputItemId = "potato",
                OutputAmount = 2,
                StageDurations = new List<float> { 30f, 60f, 90f, 300f },
                StageSpriteIds = new List<string>
                    { "Plants_26", "Plants_27", "Plants_28", "Plants_29", "Plants_30" }
            });
            RegisterItem(ep, "seed_potato", "土豆种子", "Plants_164");
            RegisterItem(ep, "potato",      "土豆",     "Plants_31");
        }

        private static void RegisterCrop(EventProcessor ep, CropConfig cfg)
        {
            ep.TriggerEventMethod(FarmManager.EVT_REGISTER_CROP_CONFIG,
                new List<object> { cfg });
            Configs[cfg.Id]        = cfg;
            SeedToCropId[cfg.SeedItemId] = cfg.Id;
        }

        private static void RegisterItem(EventProcessor ep, string id, string displayName, string spriteId = null)
        {
            var item = new InventoryItem(id, displayName)
                .WithType(InventoryItemType.Material)
                .WithMaxStack(99);
            if (!string.IsNullOrEmpty(spriteId))
                item.WithIcon(spriteId);
            ep.TriggerEventMethod("InventoryRegisterItem", new List<object> { item });
        }
    }
}
