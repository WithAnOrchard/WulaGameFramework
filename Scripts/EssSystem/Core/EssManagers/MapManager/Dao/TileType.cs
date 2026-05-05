using System;

namespace EssSystem.EssManager.MapManager.Dao
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

        /// <summary>海洋（保留兼容）。</summary>
        public const string Ocean = "ocean";

        /// <summary>陆地（保留兼容）。</summary>
        public const string Land = "land";

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

        // ─── 内置默认 RuleTile 资源 ID（按 ResourceManager 约定：文件名不带扩展名 / 子目录） ───
        /// <summary>默认海洋 RuleTile 资源 ID。</summary>
        public const string DefaultOceanRuleTile = "GrasslandsWaterTiles";

        /// <summary>默认陆地 RuleTile 资源 ID。</summary>
        public const string DefaultLandRuleTile = "GrasslandsGround";

        /// <summary>默认沙漠 RuleTile 资源 ID。</summary>
        public const string DefaultDesertRuleTile = "GrasslandSand";
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
