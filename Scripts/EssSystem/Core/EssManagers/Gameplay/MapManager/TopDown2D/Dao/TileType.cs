using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao
{
    /// <summary>
    /// 内置 Tile 类型 ID 常量集合。
    /// <para>
    /// 不强制使用 enum，便于不同模板自由扩展（如 <c>"forest"</c> / <c>"mountain"</c> / <c>"sand"</c>）。
    /// 元数据（显示名 / RuleTile 资源 ID 等）由 <see cref="TileTypeDef"/> 描述，
    /// 通过 <c>MapService.RegisterTileType</c> 注册到内存注册表。
    /// </para>
    /// </summary>
    public static class TileTypes
    {
        /// <summary>未指定（占位）。</summary>
        public const string None = "";

        /// <summary>海洋（兼容旧 ID；具体海洋分层由各模板自行定义）。</summary>
        public const string Ocean = "ocean";

        /// <summary>陆地（兼容旧 ID）。</summary>
        public const string Land = "land";

        // ─── 内置默认 RuleTile 资源 ID（按 ResourceManager 约定：文件名不带扩展名 / 子目录） ───
        /// <summary>默认海洋 RuleTile 资源 ID。</summary>
        public const string DefaultOceanRuleTile = "GrasslandsWaterTiles";

        /// <summary>默认陆地 RuleTile 资源 ID。</summary>
        public const string DefaultLandRuleTile = "GrasslandsGround";

        /// <summary>默认沙漠 RuleTile 资源 ID。</summary>
        public const string DefaultDesertRuleTile = "GrasslandSand";

        // ─── 注意 ────────────────────────────────────────────────
        // 模板（top-down / side-scroller / ...）专用群系/方块常量请放到各自模板的
        // Dao 子目录下，例如：
        //   Dao/Templates/TopDownRandom/Dao/TopDownTileTypes.cs   (Beach/Hill/Forest/...)
        //   Dao/Templates/SideScrollerRandom/Dao/SideScrollerTileTypes.cs (Sky/Dirt/Stone/...)
        // 这里不再把模板特有 ID 放到通用层，避免跨模板见到不属于自己的群系。
    }

    /// <summary>
    /// Tile 类型元数据（显示名、对应 RuleTile 资源 ID 等）。
    /// <para>
    /// 渲染层据此把 <see cref="Tile.TypeId"/> 映射到具体外观 / 规则瓦片。
    /// 后续可扩展通行性、伤害、声音等字段。
    /// </para>
    /// </summary>
    [Serializable]
    public class TileTypeDef
    {
        /// <summary>Tile 类型 ID，与 <see cref="Tile.TypeId"/> 对齐。</summary>
        public string TypeId;

        /// <summary>显示名（Editor / UI 用）。</summary>
        public string DisplayName;

        /// <summary>对应 RuleTile 在 ResourceManager 中的资源 ID（即 <c>Resources/Tiles/</c> 下的文件名不带扩展）。</summary>
        public string RuleTileResourceId;

        public TileTypeDef() { }

        public TileTypeDef(string typeId, string displayName, string ruleTileResourceId)
        {
            TypeId = typeId;
            DisplayName = displayName;
            RuleTileResourceId = ruleTileResourceId;
        }

        public override string ToString() => $"TileTypeDef({TypeId} \"{DisplayName}\" → {RuleTileResourceId})";
    }
}
