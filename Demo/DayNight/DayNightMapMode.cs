using EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Templates.TopDownRandom;
using Demo.DayNight.Map;

namespace Demo.DayNight
{
    /// <summary>枚举 → MapConfig ID / Map Template ID 解析助手。</summary>
    public static class DayNightMapModeExtensions
    {
        /// <summary>派发出该模式应当使用的 MapConfig ID（持久化主键）。</summary>
        public static string ToConfigId(this DayNightMapMode mode, string customConfigId = null) => mode switch
        {
            DayNightMapMode.Island       => "DayNightIsland",
            DayNightMapMode.PerlinIsland => "PerlinIsland",
            DayNightMapMode.Custom       => customConfigId,
            _                            => "DayNightIsland",
        };

        /// <summary>派发出该模式对应的 MapTemplate ID（写到 <c>MapManager.SetTemplateId</c>）。</summary>
        public static string ToTemplateId(this DayNightMapMode mode) => mode switch
        {
            DayNightMapMode.Island       => IslandSurvivalTemplate.Id,
            DayNightMapMode.PerlinIsland => TopDownRandomTemplate.Id,
            DayNightMapMode.Custom       => null,            // Custom 不强制改 TemplateId
            _                            => IslandSurvivalTemplate.Id,
        };
    }

    /// <summary>
    /// 昼夜求生模式可选地图模板。Inspector 用 dropdown 选择，避免手填 ConfigId。
    /// <para>
    /// 与 <c>MapTemplateRegistry</c> 注册的 Template 一一对应；新增模板时：
    /// <list type="number">
    ///   <item>注册新 <c>IMapTemplate</c>（默认 <c>RuntimeInitializeOnLoadMethod</c> 自动登记）</item>
    ///   <item>在本枚举追加新值</item>
    ///   <item>在 <see cref="DayNightGameManager.MapConfigId"/> 的 switch 中映射 ConfigId</item>
    /// </list>
    /// 仍想用任意配置 ID 时选 <see cref="Custom"/> 并填 <c>_customMapConfigId</c>。
    /// </para>
    /// </summary>
    public enum DayNightMapMode
    {
        /// <summary>有界海岛（默认） — IslandSurvivalTemplate / ConfigId="DayNightIsland"。</summary>
        Island = 0,

        /// <summary>俯视 Perlin 大世界 — TopDownRandomTemplate / ConfigId="PerlinIsland"。</summary>
        PerlinIsland = 1,

        /// <summary>自定义 ConfigId（手填 <c>_customMapConfigId</c>）。</summary>
        Custom = 99,
    }
}
