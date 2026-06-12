namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates
{
    /// <summary>
    /// 地图生成模板（top-down / side-scroller / ...）的策略接口。
    /// <para>
    /// 模板只负责该地图风格必须绑定到运行时对象的 TileType 元数据。
    /// 默认 MapConfig 与 SpawnRuleSet 统一由 MapManager 从 FrameworkResources 配置文件注册，
    /// 避免默认数据污染模板代码。
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

        /// <summary>默认 ConfigId，用于选择配置文件中的 MapConfig 与 SpawnRuleSet。</summary>
        string DefaultConfigId { get; }

        /// <summary>
        /// 注册该模板用到的所有 TileType 元数据（显示名 + RuleTile 资源 ID）。
        /// 实现里调用 <c>service.RegisterTileType(...)</c> 即可。
        /// </summary>
        void RegisterDefaultTileTypes(MapService service);

    }
}
