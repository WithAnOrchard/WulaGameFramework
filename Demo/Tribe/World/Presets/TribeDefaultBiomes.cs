using System.Collections.Generic;
using UnityEngine;
using Demo.Tribe.Entities;
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

        /// <summary>构造默认 biome（当前阶段：起源草地 + 沼泽史莱姆区）。</summary>
        public static List<TribeBiomeConfig> Build()
        {
            return new List<TribeBiomeConfig>
            {
                BuildMeadow(0f, 40f),
                BuildSwamp(40f, 80f),
                // 拓展群系（forest / town / rocky / snow / ruins）—— 待后续阶段恢复
            };
        }

        // ─── 起源草地（meadow）─────────────────────────────
        private static TribeBiomeConfig BuildMeadow(float x0, float x1)
        {
            var b = new TribeBiomeConfig("meadow", "起源草地", x0, x1, MEADOW_TINT);
            // 出生点临时小营地（X≈4，玩家在 X=0 朝右出生即可看到）：
            //   营火 + 左右帐篷占位 + 引路 NPC（用 CharacterManager 默认 Mage 配置，
            //   与玩家 Warrior 同套身体部位系统，只是不同 sprite sheet）
            b.Add(new CampFeature(x0 + 4f, npcInstanceId: "TribeNpc_Alice",
                npcDisplayName: "向导艾丽丝", npcCharacterConfigId: "Mage"));
            // 史莱姆刷新点 —— 放在 meadow 右边缘附近（x≈x0+35），从地图边缘进军营地。
            // 进军目标设为营地右侧 ~3 单位（x0+7）：抵达后切回随机巡游，会在营地外徘徊。
            var slimeCfg = Slime.Preset();
            slimeCfg.MarchTargetX = x0 + 7f;
            slimeCfg.MarchArrivalThreshold = 1.5f;
            // 进军期间活动圈不应限制行进 → 拉大 ActivityRadius，到达后局部巡游半径仍然 6 单位的体感（_anchor 会重置为到达点）
            slimeCfg.ActivityRadius = 60f;
            // 每只史莱姆 30% 概率出生 2~5 秒后巨大化 12 秒（体型 ×2、HP ×3、减伤 50%、跳跃 ×1.5）
            slimeCfg.GiantChance = 0.3f;
            b.Add(new CreatureSpawnerFeature(
                worldX: x0 + 35f,
                displayName: "史莱姆巢",
                config: slimeCfg,
                spawnNamePrefix: "史莱姆_",
                maxAlive: 3,
                interval: 10f,
                initialDelay: 5f) { HorizontalJitter = 3f });
            // 既有素材：胡萝卜 / 向日葵 / 红蘑菇 / 浆果（推到营地东侧 X≥15 开始铺）
            b.Add(new GatherableFeature(x0 + 15f, "向日葵", "Tribe/Common/Objects/Crops (sunflower)", "tribe_sunflower_pickable"));
            b.Add(new GatherableFeature(x0 + 22f, "红蘑菇", "Tribe/Common/Objects/Mushroom_2",        "tribe_red_mushroom_pickable"));
            b.Add(new GatherableFeature(x0 + 30f, "浆果丛", "Tribe/Common/Objects/Crops (berries)",   "tribe_berries_pickable",   2f, 3));
            b.Add(new GatherableFeature(x0 + 40f, "胡萝卜", "Tribe/Common/Objects/Crops (carrot)",    "tribe_carrot_pickable"));
            b.Add(new GatherableFeature(x0 + 50f, "向日葵2","Tribe/Common/Objects/Crops (sunflower)", "tribe_sunflower_pickable"));
            // 占位：浅水洼 + 传送门入口（通往 #3 静谧采集林）—— 暂时隐藏，待素材到位后恢复
            // b.Add(new PlaceholderFeature(x0 + 20f, "水洼",   WATER_C,  new Vector2(2f, 0.4f),  yOffset: -0.2f));
            // b.Add(new PlaceholderFeature(x0 + 70f, "🌀传送门\n静谧采集林", PORTAL_C, new Vector2(1.5f, 3f), yOffset: 0.5f));
            // 暂时取消其他生物生成 —— 仅保留采集物 + 营地。沼泽生物在 BuildSwamp。
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
            b.Add(new GatherableFeature(x0 + 20f, "浆果丛", "Tribe/Common/Objects/Crops (berries)",  "tribe_berries_pickable", 2f, 3));
            b.Add(new GatherableFeature(x0 + 50f, "红蘑菇", "Tribe/Common/Objects/Mushroom_2",       "tribe_red_mushroom_pickable"));
            b.Add(new GatherableFeature(x0 + 70f, "红蘑菇", "Tribe/Common/Objects/Mushroom_2",       "tribe_red_mushroom_pickable"));
            // 怪物：蘑菇怪 + 蝙蝠（现有 preset）
            b.Add(new CreatureFeature(x0 + 60f,  "蘑菇怪", Mushy.Mushy01()));
            b.Add(new CreatureFeature(x0 + 100f, "蝙蝠",   Bat.Preset(), yOffset: 2f));
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

            // 环境占位：芦苇 + 水洼 + 泥潭（紧凑版 40 单位）
            var reedColor = new Color(0.55f, 0.7f, 0.3f);
            var mudColor  = new Color(0.40f, 0.30f, 0.18f);
            b.Add(new PlaceholderFeature(x0 + 5f,  "🌾芦苇",  reedColor, new Vector2(0.6f, 2f), yOffset: 0.5f));
            b.Add(new PlaceholderFeature(x0 + 15f, "🌊水洼",  WATER_C,   new Vector2(6f, 0.5f), yOffset: -0.25f));
            b.Add(new PlaceholderFeature(x0 + 25f, "💩泥潭",  mudColor,  new Vector2(3f, 0.3f), yOffset: -0.2f));
            b.Add(new PlaceholderFeature(x0 + 33f, "🌾芦苇",  reedColor, new Vector2(0.6f, 2f), yOffset: 0.5f));

            // 史莱姆群 —— 4 只，间隔 ~10 单位，匹配 ActivityRadius=6 不会互相重叠太多
            b.Add(new CreatureFeature(x0 + 8f,  "史莱姆_α", Slime.Preset()));
            b.Add(new CreatureFeature(x0 + 18f, "史莱姆_β", Slime.Preset()));
            b.Add(new CreatureFeature(x0 + 28f, "史莱姆_γ", Slime.Preset()));
            b.Add(new CreatureFeature(x0 + 36f, "史莱姆_δ", Slime.Preset()));
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
            b.Add(new CreatureFeature(x0 + 60f, "骷髅",  Skeleton.Preset()));
            b.Add(new CreatureFeature(x0 + 100f,"食人魔", Ogre.Preset(), yOffset: 1f));
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
            b.Add(new CreatureFeature(x0 + 50f, "恶狼",   Wolf.Preset()));
            b.Add(new CreatureFeature(x0 + 90f, "冰蘑菇", Mushy.Mushy04()));
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
            b.Add(new CreatureFeature(x0 + 30f, "精英骷髅", Skeleton.Preset()));
            b.Add(new CreatureFeature(x0 + 70f, "蝙蝠群",   Bat.Preset(), yOffset: 2f));
            b.Add(new CreatureFeature(x0 + 110f,"边境食人魔", Ogre.Preset(), yOffset: 1f));
            return b;
        }
    }
}
