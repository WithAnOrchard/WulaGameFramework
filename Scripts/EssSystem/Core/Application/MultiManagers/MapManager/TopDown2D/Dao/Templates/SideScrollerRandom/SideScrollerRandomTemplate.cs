using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom
{
    /// <summary>
    /// 横版 2D 随机大世界模板（骨架）。
    /// <para>
    /// 当前实现仅注册基础 TileType 元数据；默认配置从 FrameworkResources 读取。
    /// 装饰器（洞穴 / 矿脉 / 群系横向带）请实现 <c>IChunkDecorator</c> 后通过
    /// <c>MapService.RegisterDecorator</c> 加入管线。
    /// </para>
    /// </summary>
    public sealed class SideScrollerRandomTemplate : IMapTemplate
    {
        public const string Id = "side_scroller_random";

        public string TemplateId => Id;
        public string DisplayName => "横版 2D 随机大世界";
        public string DefaultConfigId => "SideScrollerWorld";

        public void RegisterDefaultTileTypes(MapService service)
        {
            if (service == null) return;
            var ocean = TileTypes.DefaultOceanRuleTile;
            var land = TileTypes.DefaultLandRuleTile;
            var desert = TileTypes.DefaultDesertRuleTile;

            // 兼容旧 ID（与俯视模板共用 Land 关键字）
            service.RegisterTileType(new TileTypeDef(TileTypes.Land, "陆地", land));

            // 横版基础方块
            // Sky 不渲染贴图，但仍登记元数据避免 unknown 警告
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Sky, "天空", null));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Grass, "草皮", land));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Dirt, "泥土", land));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Stone, "石头", land));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Bedrock, "基岩", land));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Sand, "沙地", desert));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Snow, "雪", desert));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Water, "水", ocean));
            service.RegisterTileType(new TileTypeDef(SideScrollerTileTypes.Lava, "岩浆", ocean));
        }

    }
}
