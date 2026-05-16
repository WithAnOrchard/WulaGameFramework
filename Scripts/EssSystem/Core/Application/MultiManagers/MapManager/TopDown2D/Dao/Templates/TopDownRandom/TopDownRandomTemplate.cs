using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn.Dao;
using PerlinMapConfig = EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config.PerlinMapConfig;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom
{
    /// <summary>
    /// 俯视 2D 随机大世界模板（Perlin 海陆 + 群系 + 河流 + 群系 spawn）。
    /// </summary>
    public sealed class TopDownRandomTemplate : IMapTemplate
    {
        public const string Id = "top_down_random";

        public string TemplateId => Id;
        public string DisplayName => "俯视 2D 随机大世界";
        public string DefaultConfigId => "PerlinIsland";

        public MapConfig CreateDefaultConfig()
        {
            return new PerlinMapConfig(DefaultConfigId, "Perlin 海陆地图");
        }

        /// <summary>
        /// 注册 top-down 模板的所有 TileType 元数据：海洋分层 / 河流 / 湖泊 / 海滩 / 高度类（丘陵/山地/雪峰）/ 8 大群系。
        /// </summary>
        public void RegisterDefaultTileTypes(MapService service)
        {
            if (service == null) return;
            var ocean = TileTypes.DefaultOceanRuleTile;
            var land = TileTypes.DefaultLandRuleTile;
            var desert = TileTypes.DefaultDesertRuleTile;

            // 兼容旧 ID
            service.RegisterTileType(new TileTypeDef(TileTypes.Ocean, "海洋", ocean));
            service.RegisterTileType(new TileTypeDef(TileTypes.Land, "陆地", land));

            // 海洋分层
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.DeepOcean, "深海", ocean));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.ShallowOcean, "浅海", ocean));

            // 河流 / 湖泊：用水 RuleTile 先顶，后续替换为专用贴图
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.River, "河流", ocean));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Lake, "湖泊", ocean));

            // 陆地：地形高度类
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Beach, "海滩", desert));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Hill, "丘陵", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Mountain, "山地", land));
            // 雪峰用沙地基底（debug 模式下 tint 出冰白）
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.SnowPeak, "雪峰", desert));

            // 陆地：生物群系
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Tundra, "苔原", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Taiga, "针叶林", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Grassland, "草原", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Forest, "温带森林", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Swamp, "沼泽", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Desert, "沙漠", desert));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Savanna, "稀树草原", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Rainforest, "热带雨林", land));
        }

        /// <summary>
        /// 默认 spawn 规则集到 <see cref="DefaultConfigId"/>：按生物群系分别配置 SmallTree / MediumTree
        /// 的密度、湿度门槛、单 chunk 上限、聚簇参数。
        /// 同名规则集 <c>"default"</c> 已存在则跳过。
        /// </summary>
        public void RegisterDefaultSpawnRules(EntitySpawnService spawnService)
        {
            if (spawnService == null) return;

            // 已存在同名规则集则跳过
            var existing = spawnService.GetRuleSets(DefaultConfigId);
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] != null && existing[i].Id == "default") return;
            }

            const string SmallTreeId  = "SmallTreeEntity";
            const string MediumTreeId = "MediumTreeEntity";

            var set = new EntitySpawnRuleSet("default")
                // ─── 热带雨林：最密 ────────────────────────────────────
                .WithRule(new EntitySpawnRule("rainforest_medium", MediumTreeId)
                    .WithTileTypes(TopDownTileTypes.Rainforest)
                    .WithMoistureRange(0.55f, 1.0f)
                    .WithDensity(0.10f).WithMaxPerChunk(28).WithMinSpacing(1)
                    .WithPriority(290).WithRngTag("def_rainforest_med"))
                .WithRule(new EntitySpawnRule("rainforest_small", SmallTreeId)
                    .WithTileTypes(TopDownTileTypes.Rainforest)
                    .WithMoistureRange(0.5f, 1.0f)
                    .WithDensity(0.06f).WithMaxPerChunk(20)
                    .WithPriority(295).WithRngTag("def_rainforest_small"))

                // ─── 温带森林：高密度，主干为中型树 ─────────────────
                .WithRule(new EntitySpawnRule("forest_medium", MediumTreeId)
                    .WithTileTypes(TopDownTileTypes.Forest)
                    .WithMoistureRange(0.4f, 1.0f)
                    .WithDensity(0.06f).WithMaxPerChunk(18).WithMinSpacing(1)
                    .WithPriority(300).WithRngTag("def_forest_med"))
                .WithRule(new EntitySpawnRule("forest_small", SmallTreeId)
                    .WithTileTypes(TopDownTileTypes.Forest)
                    .WithMoistureRange(0.35f, 1.0f)
                    .WithDensity(0.05f).WithMaxPerChunk(16)
                    .WithPriority(305).WithRngTag("def_forest_small"))

                // ─── 针叶林：冷温带，聚簇 ─────────────────────────────
                .WithRule(new EntitySpawnRule("taiga_medium", MediumTreeId)
                    .WithTileTypes(TopDownTileTypes.Taiga)
                    .WithTemperatureRange(0.0f, 0.45f).WithMoistureRange(0.3f, 1.0f)
                    .WithDensity(0.05f).WithCluster(2, 3, 2)
                    .WithMaxPerChunk(14).WithMinSpacing(1)
                    .WithPriority(310).WithRngTag("def_taiga_med"))
                .WithRule(new EntitySpawnRule("taiga_small", SmallTreeId)
                    .WithTileTypes(TopDownTileTypes.Taiga)
                    .WithTemperatureRange(0.0f, 0.5f).WithMoistureRange(0.25f, 1.0f)
                    .WithDensity(0.04f).WithMaxPerChunk(12)
                    .WithPriority(315).WithRngTag("def_taiga_small"))

                // ─── 沼泽：仅中型，散落 ────────────────────────────────
                .WithRule(new EntitySpawnRule("swamp_medium", MediumTreeId)
                    .WithTileTypes(TopDownTileTypes.Swamp)
                    .WithMoistureRange(0.6f, 1.0f)
                    .WithDensity(0.025f).WithMaxPerChunk(8).WithMinSpacing(2)
                    .WithPriority(320).WithRngTag("def_swamp_med"))

                // ─── 草原：仅小树，稀疏点缀 ────────────────────────────
                .WithRule(new EntitySpawnRule("grassland_small", SmallTreeId)
                    .WithTileTypes(TopDownTileTypes.Grassland)
                    .WithMoistureRange(0.3f, 0.85f)
                    .WithDensity(0.012f).WithMaxPerChunk(5).WithMinSpacing(2)
                    .WithPriority(330).WithRngTag("def_grass_small"))

                // ─── 稀树草原：极稀疏，单棵孤立 ────────────────────
                .WithRule(new EntitySpawnRule("savanna_small", SmallTreeId)
                    .WithTileTypes(TopDownTileTypes.Savanna)
                    .WithMoistureRange(0.2f, 0.6f)
                    .WithDensity(0.006f).WithMaxPerChunk(3).WithMinSpacing(3)
                    .WithPriority(340).WithRngTag("def_savanna_small"));

            spawnService.RegisterRuleSet(DefaultConfigId, set);
        }
    }
}
