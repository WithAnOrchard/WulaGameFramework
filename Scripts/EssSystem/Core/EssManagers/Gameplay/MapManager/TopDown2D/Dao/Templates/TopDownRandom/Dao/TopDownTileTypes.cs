namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Dao
{
    /// <summary>
    /// 俯视 2D（top-down）模板专用 TileType ID 常量。
    /// <para>
    /// 不再放在通用 <see cref="EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.TileTypes"/>，
    /// 避免横版/其它模板被迫见到俯视特有的群系名称。
    /// </para>
    /// <para>
    /// 通用 ID（None / Ocean / Land / 默认 RuleTile 资源 ID）仍在 <c>Dao.TileTypes</c>。
    /// </para>
    /// </summary>
    public static class TopDownTileTypes
    {
        // ─── 海洋分层（按海拔深度） ───────────────────────────────
        public const string DeepOcean = "deep_ocean";
        public const string ShallowOcean = "shallow_ocean";

        /// <summary>河流（陆地上的流水线）。</summary>
        public const string River = "river";

        /// <summary>湖泊（陆地内静水体）。</summary>
        public const string Lake = "lake";

        // ─── 陆地：地形高度类 ────────────────────────────────────
        public const string Beach = "beach";
        public const string Hill = "hill";
        public const string Mountain = "mountain";
        public const string SnowPeak = "snow_peak";

        // ─── 陆地：生物群系（低海拔由温度湿度决定） ───────────────
        public const string Tundra = "tundra";
        public const string Taiga = "taiga";
        public const string Grassland = "grassland";
        public const string Forest = "forest";
        public const string Swamp = "swamp";
        public const string Desert = "desert";
        public const string Savanna = "savanna";
        public const string Rainforest = "rainforest";
    }
}
