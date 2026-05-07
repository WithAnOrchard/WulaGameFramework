using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Config;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom
{
    /// <summary>
    /// 横版 2D 随机大世界模板（骨架）。
    /// <para>
    /// 当前实现仅注册基础 TileType 元数据 + 提供默认 Config；spawn 规则待业务侧补充。
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

        public MapConfig CreateDefaultConfig()
        {
            return new SideScrollerMapConfig(DefaultConfigId, "横版随机世界");
        }

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

        /// <summary>
        /// 横版默认 spawn 规则待补（敌人/装饰物/战利品）。当前留空让业务侧自行注册。
        /// </summary>
        public void RegisterDefaultSpawnRules(EntitySpawnService spawnService)
        {
            // No-op：横版生成需要的 spawn 规则形式（按层带 / 按地表 / 按洞穴）
            // 与俯视的"按群系 (T,M)"语义不同，强制使用同一套 EntitySpawnRule 不一定合适。
            // 后续可在此挂"地表敌人"、"地下矿"等示例。
        }
    }
}
