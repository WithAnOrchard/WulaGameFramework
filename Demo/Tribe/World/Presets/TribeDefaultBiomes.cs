using System.Collections.Generic;
using UnityEngine;
using Demo.Tribe.Enemy;
using Demo.Tribe.World.Features;

namespace Demo.Tribe.World.Presets
{
    /// <summary>
    /// Tribe 默认 7 段带状世界 —— 设计参考 <c>Demo/Tribe/ToDo.md</c> 条目 #1。
    /// <para>
    /// 当前为占位实现：缺素材的 Feature（湖泊 / 石头堆 / 房屋 / 传送门 / NPC 等）
    /// 一律用 <see cref="PlaceholderFeature"/> 色块 + 中文标签代替；后续按
    /// <c>Demo/Tribe/need.md</c> 列出的素材路径替换为具体 *Feature 类型。
    /// </para>
    /// </summary>
    public static class TribeDefaultBiomes
    {
        // ═════════════════════════════════════════════════════
        //  色调参考（biome.GroundTint）
        // ═════════════════════════════════════════════════════
        private static readonly Color MEADOW_TINT = new Color(0.69f, 0.85f, 0.47f);   // 翠绿
        private static readonly Color FOREST_TINT = new Color(0.29f, 0.44f, 0.22f);   // 深绿
        private static readonly Color TOWN_TINT   = new Color(0.85f, 0.66f, 0.47f);   // 暖黄
        private static readonly Color SWAMP_TINT  = new Color(0.35f, 0.41f, 0.22f);   // 暗绿
        private static readonly Color ROCKY_TINT  = new Color(0.53f, 0.49f, 0.44f);   // 灰褐
        private static readonly Color SNOW_TINT   = new Color(0.88f, 0.91f, 0.94f);   // 冷白
        private static readonly Color RUINS_TINT  = new Color(0.41f, 0.34f, 0.47f);   // 暗紫

        // 占位色块
        private static readonly Color WATER_C   = new Color(0.40f, 0.65f, 0.95f);
        private static readonly Color STONE_C   = new Color(0.55f, 0.55f, 0.58f);
        private static readonly Color HOUSE_C   = new Color(0.78f, 0.55f, 0.32f);
        private static readonly Color PORTAL_C  = new Color(0.45f, 0.30f, 0.85f);
        private static readonly Color NPC_C     = new Color(0.95f, 0.85f, 0.40f);
        private static readonly Color ICE_C     = new Color(0.78f, 0.92f, 0.95f);
        private static readonly Color RUIN_C    = new Color(0.50f, 0.40f, 0.55f);

        /// <summary>构造 7 段默认 biome（从 X=0 开始，全长约 760 单位）。</summary>
        public static List<TribeBiomeConfig> Build()
        {
            return new List<TribeBiomeConfig>
            {
                BuildMeadow(0f, 80f),
                BuildForest(80f, 200f),
                BuildTown(200f, 280f),
                BuildSwamp(280f, 400f),
                BuildRocky(400f, 520f),
                BuildSnow(520f, 640f),
                BuildRuins(640f, 760f),
            };
        }

        // ─── 起源草地（meadow）─────────────────────────────
        private static TribeBiomeConfig BuildMeadow(float x0, float x1)
        {
            var b = new TribeBiomeConfig("meadow", "起源草地", x0, x1, MEADOW_TINT);
            // 现有素材：胡萝卜 / 向日葵 / 红蘑菇 / 浆果
            b.Add(new GatherableFeature(x0 + 5f,  "向日葵", "Tribe/Objects/Crops (sunflower)", "tribe_sunflower_pickable"));
            b.Add(new GatherableFeature(x0 + 12f, "红蘑菇", "Tribe/Objects/Mushroom_2",        "tribe_red_mushroom_pickable"));
            b.Add(new GatherableFeature(x0 + 20f, "浆果丛", "Tribe/Objects/Crops (berries)",   "tribe_berries_pickable",   2f, 3));
            b.Add(new GatherableFeature(x0 + 30f, "胡萝卜", "Tribe/Objects/Crops (carrot)",    "tribe_carrot_pickable"));
            b.Add(new GatherableFeature(x0 + 45f, "向日葵2","Tribe/Objects/Crops (sunflower)", "tribe_sunflower_pickable"));
            // 占位：浅水洼 + 传送门入口（通往 #3 静谧采集林）
            b.Add(new PlaceholderFeature(x0 + 15f, "水洼",   WATER_C,  new Vector2(2f, 0.4f),  yOffset: -0.2f));
            b.Add(new PlaceholderFeature(x0 + 40f, "🌀传送门\n静谧采集林", PORTAL_C, new Vector2(1.5f, 3f), yOffset: 0.5f));
            // 友好动物（用现有 Cow / Hen 预设）
            b.Add(new CreatureFeature(x0 + 25f, "奶牛", TribeCreaturePresets.Cow()));
            b.Add(new CreatureFeature(x0 + 55f, "母鸡", TribeCreaturePresets.Hen()));
            return b;
        }

        // ─── 森林（forest）──────────────────────────────
        private static TribeBiomeConfig BuildForest(float x0, float x1)
        {
            var b = new TribeBiomeConfig("forest", "森林", x0, x1, FOREST_TINT);
            // 占位：高树 x3
            b.Add(new PlaceholderFeature(x0 + 10f, "🌲高树", FOREST_TINT, new Vector2(2f, 5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 35f, "🌲高树", FOREST_TINT, new Vector2(2f, 5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 80f, "🌲高树", FOREST_TINT, new Vector2(2f, 5f), yOffset: 0.5f));
            // 浆果 + 蘑菇圈（现有素材）
            b.Add(new GatherableFeature(x0 + 20f, "浆果丛", "Tribe/Objects/Crops (berries)",  "tribe_berries_pickable", 2f, 3));
            b.Add(new GatherableFeature(x0 + 50f, "红蘑菇", "Tribe/Objects/Mushroom_2",       "tribe_red_mushroom_pickable"));
            b.Add(new GatherableFeature(x0 + 70f, "红蘑菇", "Tribe/Objects/Mushroom_2",       "tribe_red_mushroom_pickable"));
            // 怪物：蘑菇怪 + 蝙蝠（现有 preset）
            b.Add(new CreatureFeature(x0 + 60f,  "蘑菇怪", TribeCreaturePresets.Mushy01()));
            b.Add(new CreatureFeature(x0 + 100f, "蝙蝠",   TribeCreaturePresets.Bat(), yOffset: 2f));
            return b;
        }

        // ─── 小镇（town）────────────────────────────────
        private static TribeBiomeConfig BuildTown(float x0, float x1)
        {
            var b = new TribeBiomeConfig("town", "小镇", x0, x1, TOWN_TINT);
            // 房屋 x3 + 井 + 营火 + NPC
            b.Add(new PlaceholderFeature(x0 + 10f, "🏠房屋A", HOUSE_C, new Vector2(4f, 4f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 28f, "🏠房屋B", HOUSE_C, new Vector2(4f, 4f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 50f, "🏠房屋C", HOUSE_C, new Vector2(4f, 4f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 18f, "💧井",    new Color(0.3f,0.5f,0.7f), new Vector2(1.5f, 2f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 38f, "🔥营火",  new Color(0.95f,0.5f,0.2f), new Vector2(1.2f, 1.2f), yOffset: 0.5f));
            // NPC 占位（艾丽丝商人 + 守卫）
            b.Add(new PlaceholderFeature(x0 + 22f, "👤艾丽丝\n商人",  NPC_C, new Vector2(0.8f, 1.6f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 60f, "👤守卫",         NPC_C, new Vector2(0.8f, 1.6f), yOffset: 0.5f));
            return b;
        }

        // ─── 沼泽湿地（swamp）──────────────────────────
        private static TribeBiomeConfig BuildSwamp(float x0, float x1)
        {
            var b = new TribeBiomeConfig("swamp", "沼泽湿地", x0, x1, SWAMP_TINT);
            // 大型湖泊 + 芦苇 + 泥潭
            b.Add(new PlaceholderFeature(x0 + 30f, "🌊大型湖泊",   WATER_C,                      new Vector2(12f, 0.6f), yOffset: -0.3f));
            b.Add(new PlaceholderFeature(x0 + 25f, "🌾芦苇",       new Color(0.55f,0.7f,0.3f),    new Vector2(0.6f, 2f),  yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 50f, "🌾芦苇",       new Color(0.55f,0.7f,0.3f),    new Vector2(0.6f, 2f),  yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 75f, "💩泥潭",       new Color(0.40f,0.30f,0.18f), new Vector2(2f, 0.3f),  yOffset: -0.2f));
            // 怪物：史莱姆 + 毒蘑菇
            b.Add(new CreatureFeature(x0 + 70f, "史莱姆", TribeCreaturePresets.Slime()));
            b.Add(new CreatureFeature(x0 + 95f, "毒蘑菇", TribeCreaturePresets.Mushy02()));
            return b;
        }

        // ─── 岩石荒原（rocky）──────────────────────────
        private static TribeBiomeConfig BuildRocky(float x0, float x1)
        {
            var b = new TribeBiomeConfig("rocky", "岩石荒原", x0, x1, ROCKY_TINT);
            // 现有 stone 素材
            b.Add(new PlaceholderFeature(x0 + 10f, "🪨石堆",  STONE_C, new Vector2(2f, 1.5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 30f, "🪨石堆",  STONE_C, new Vector2(3f, 2f),   yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 50f, "⛏️铁矿脉", new Color(0.65f, 0.55f, 0.35f), new Vector2(1.5f, 1.5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 80f, "🗻断崖",  STONE_C, new Vector2(4f, 4f),   yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 95f, "⛏️煤矿脉", new Color(0.20f, 0.20f, 0.20f), new Vector2(1.5f, 1.5f), yOffset: 0.5f));
            // 怪物：骷髅 + 食人魔
            b.Add(new CreatureFeature(x0 + 60f, "骷髅",  TribeCreaturePresets.Skeleton()));
            b.Add(new CreatureFeature(x0 + 100f,"食人魔", TribeCreaturePresets.Ogre(), yOffset: 1f));
            return b;
        }

        // ─── 雪原（snow）──────────────────────────────
        private static TribeBiomeConfig BuildSnow(float x0, float x1)
        {
            var b = new TribeBiomeConfig("snow", "雪原", x0, x1, SNOW_TINT);
            // 雪松 + 冻结水洼 + 冰柱
            b.Add(new PlaceholderFeature(x0 + 15f, "🌲雪松", new Color(0.3f, 0.5f, 0.4f),  new Vector2(2f, 5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 40f, "🌲雪松", new Color(0.3f, 0.5f, 0.4f),  new Vector2(2f, 5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 60f, "❄️冰湖", ICE_C,                       new Vector2(8f, 0.4f), yOffset: -0.2f));
            b.Add(new PlaceholderFeature(x0 + 80f, "❄️冰柱", ICE_C,                       new Vector2(0.5f, 3f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 95f, "❄️冰柱", ICE_C,                       new Vector2(0.5f, 3f), yOffset: 0.5f));
            // 怪物：恶狼 + 冰蘑菇
            b.Add(new CreatureFeature(x0 + 50f, "恶狼",   TribeCreaturePresets.Wolf()));
            b.Add(new CreatureFeature(x0 + 90f, "冰蘑菇", TribeCreaturePresets.Mushy04()));
            return b;
        }

        // ─── 遗迹边境（ruins）──────────────────────────
        private static TribeBiomeConfig BuildRuins(float x0, float x1)
        {
            var b = new TribeBiomeConfig("ruins", "遗迹边境", x0, x1, RUINS_TINT);
            // 残柱 + 骨堆 + 宝箱
            b.Add(new PlaceholderFeature(x0 + 15f, "🏛️残柱",   RUIN_C,                     new Vector2(1f, 5f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 40f, "🏛️残柱",   RUIN_C,                     new Vector2(1f, 4f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 60f, "💀骨堆",   new Color(0.85f, 0.82f, 0.75f), new Vector2(1.5f, 1f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 80f, "📦宝箱",   new Color(0.6f,  0.45f, 0.2f),  new Vector2(1.2f, 1.2f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 100f,"🌀传送门\n副本入口", PORTAL_C, new Vector2(1.5f, 3f), yOffset: 0.5f));
            // 精英怪：骷髅 + 蝙蝠 + Boss 候选 (Ogre)
            b.Add(new CreatureFeature(x0 + 30f, "精英骷髅", TribeCreaturePresets.Skeleton()));
            b.Add(new CreatureFeature(x0 + 70f, "蝙蝠群",   TribeCreaturePresets.Bat(), yOffset: 2f));
            b.Add(new CreatureFeature(x0 + 110f,"边境食人魔", TribeCreaturePresets.Ogre(), yOffset: 1f));
            return b;
        }
    }
}
