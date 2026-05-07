namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Dao
{
    /// <summary>
    /// 横版 2D（side-scroller）模板专用 TileType ID 常量。
    /// <para>
    /// 与俯视模板的群系名（Forest/Hill/...）正交：横版地图按"层"组织（地表 / 土层 / 石层 / 基岩 / 液体），
    /// 业务侧据此决定通行 / 破坏 / 渲染规则。
    /// </para>
    /// <para>
    /// 横版坐标约定：本框架 Chunk 始终是 2D 网格 (X,Y)；横版生成中 Y 视为"高度轴"，
    /// 数值越大越靠上（向上 = 天空）。SideScrollerMapGenerator 据此填 Tile。
    /// </para>
    /// </summary>
    public static class SideScrollerTileTypes
    {
        /// <summary>空气（玩家可通行，无碰撞）。</summary>
        public const string Sky = "ss_sky";

        /// <summary>地表草皮（土层最上一格，可踩）。</summary>
        public const string Grass = "ss_grass";

        /// <summary>土层（地表草皮以下数格，可挖）。</summary>
        public const string Dirt = "ss_dirt";

        /// <summary>石层（土层以下，更难挖）。</summary>
        public const string Stone = "ss_stone";

        /// <summary>基岩（地图最底层，不可挖，世界边界）。</summary>
        public const string Bedrock = "ss_bedrock";

        /// <summary>沙地（沙漠地表）。</summary>
        public const string Sand = "ss_sand";

        /// <summary>雪（寒带地表）。</summary>
        public const string Snow = "ss_snow";

        /// <summary>水（液体，可游泳）。</summary>
        public const string Water = "ss_water";

        /// <summary>岩浆（液体 + 伤害）。</summary>
        public const string Lava = "ss_lava";
    }
}
