using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates
{
    /// <summary>
    /// 地图生成模板（top-down / side-scroller / ...）的策略接口。
    /// <para>
    /// 每个模板封装"该地图风格特有的默认设定"：TileType 元数据、默认配置、默认 Spawn 规则。
    /// MapManager 启动时按当前 <c>TemplateId</c> 选择一个 Template 实例，调用其默认注册方法，
    /// 自身不再写死 top-down 群系常量。
    /// </para>
    /// <para>
    /// 业务侧也可以注册自定义 Template；通过 <see cref="MapTemplateRegistry.Register"/> 加入即可。
    /// </para>
    /// </summary>
    public interface IMapTemplate
    {
        /// <summary>模板唯一 ID（如 <c>"top_down_random"</c> / <c>"side_scroller_random"</c>）。</summary>
        string TemplateId { get; }

        /// <summary>显示名（Inspector / UI）。</summary>
        string DisplayName { get; }

        /// <summary>默认 ConfigId（用作 <see cref="CreateDefaultConfig"/> 与 <see cref="RegisterDefaultSpawnRules"/> 的目标）。</summary>
        string DefaultConfigId { get; }

        /// <summary>
        /// 注册该模板用到的所有 TileType 元数据（显示名 + RuleTile 资源 ID）。
        /// 实现里调用 <c>service.RegisterTileType(...)</c> 即可。
        /// </summary>
        void RegisterDefaultTileTypes(MapService service);

        /// <summary>
        /// 创建该模板的默认 <see cref="MapConfig"/> 实例（已设置 ConfigId / DisplayName 等）。
        /// 调用方负责注册到 <see cref="MapService.RegisterConfig"/>。
        /// </summary>
        MapConfig CreateDefaultConfig();

        /// <summary>
        /// 注册该模板的默认 Spawn 规则（可空，未配置 spawn 系统时直接 return）。
        /// 实现里通过 <see cref="EntitySpawnService.RegisterRuleSet"/> 写入。
        /// </summary>
        void RegisterDefaultSpawnRules(EntitySpawnService spawnService);
    }
}
