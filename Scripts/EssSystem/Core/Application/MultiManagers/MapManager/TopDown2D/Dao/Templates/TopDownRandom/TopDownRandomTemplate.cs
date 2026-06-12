using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Dao;

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

    }
}
