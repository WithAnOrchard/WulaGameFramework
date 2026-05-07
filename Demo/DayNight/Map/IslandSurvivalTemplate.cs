using UnityEngine;
using EssSystem.EssManager.MapManager;
using EssSystem.EssManager.MapManager.Dao;
using EssSystem.EssManager.MapManager.Dao.Config;
using EssSystem.EssManager.MapManager.Dao.Templates;
using EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Dao;
using EssSystem.EssManager.MapManager.Spawn;
using Demo.DayNight.Map.Config;

namespace Demo.DayNight.Map
{
    /// <summary>
    /// 昼夜求生 - 海岛模板。把 <see cref="IslandSurvivalMapConfig"/> + <see cref="IslandSurvivalGenerator"/>
    /// 注册成 <see cref="MapTemplateRegistry"/> 中的一项，可在 <c>MapManager</c> Inspector 的
    /// <c>_templateId</c> 填 <see cref="Id"/>（"day_night_island"）启用。
    /// <para>**自动注册**：通过 <see cref="RuntimeInitializeOnLoadMethodAttribute"/> 在 Unity 启动早期就把模板挂进 Registry，
    /// 无需业务侧手动调用。</para>
    /// </summary>
    public sealed class IslandSurvivalTemplate : IMapTemplate
    {
        public const string Id = "day_night_island";

        public string TemplateId => Id;
        public string DisplayName => "昼夜求生 - 有界海岛";
        public string DefaultConfigId => "DayNightIsland";

        public MapConfig CreateDefaultConfig() =>
            new IslandSurvivalMapConfig(DefaultConfigId, "昼夜求生海岛 (20×20)");

        /// <summary>仅注册本模板生成器实际会用到的 TileType 子集（与 TopDownRandomTemplate 同 ID，幂等覆盖）。</summary>
        public void RegisterDefaultTileTypes(MapService service)
        {
            if (service == null) return;
            var ocean = TileTypes.DefaultOceanRuleTile;
            var land = TileTypes.DefaultLandRuleTile;
            var desert = TileTypes.DefaultDesertRuleTile;

            // 兼容通用 ID
            service.RegisterTileType(new TileTypeDef(TileTypes.Ocean, "海洋", ocean));
            service.RegisterTileType(new TileTypeDef(TileTypes.Land, "陆地", land));

            // 海洋 / 海岸
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.DeepOcean, "深海", ocean));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.ShallowOcean, "浅海", ocean));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Beach, "海滩", desert));

            // 山地
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Mountain, "山地", land));

            // 陆地群系
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Tundra, "苔原", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Taiga, "针叶林", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Grassland, "草原", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Forest, "温带森林", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Swamp, "沼泽", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Desert, "沙漠", desert));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Savanna, "稀树草原", land));
            service.RegisterTileType(new TileTypeDef(TopDownTileTypes.Rainforest, "热带雨林", land));
        }

        /// <summary>本模板不内建 spawn 规则；业务可在 <c>WaveSpawnService</c> 自行注册波次怪物，
        /// 或调用 <c>TopDownRandomTemplate</c> 注册 forest/grass spawn 规则后复用。</summary>
        public void RegisterDefaultSpawnRules(EntitySpawnService spawnService) { }

        /// <summary>
        /// Unity 启动早期自动登记模板，业务侧无需写注册代码。
        /// <para>
        /// **不能用 <c>SubsystemRegistration</c>**：框架的 <c>PlayModeResetGuard</c> 在那个阶段会清空
        /// <c>MapTemplateRegistry</c>，且同阶段内多个 <c>RuntimeInitializeOnLoadMethod</c> 之间执行顺序未定义，
        /// 一旦 Reset 后跑，本注册就被擦除。<c>BeforeSceneLoad</c> 严格晚于 <c>SubsystemRegistration</c>，
        /// 既能保证模板可用，又赶在所有 <c>Awake()</c> 之前。
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoRegister()
        {
            if (!MapTemplateRegistry.Contains(Id))
                MapTemplateRegistry.Register(new IslandSurvivalTemplate());
        }
    }
}
