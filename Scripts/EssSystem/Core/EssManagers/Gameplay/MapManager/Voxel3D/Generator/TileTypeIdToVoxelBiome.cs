using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 把 2D <c>BiomeClassifier</c> 输出的 string TypeId 映射到 3D 体素的 byte BiomeId。
    /// <para>
    /// 桥梁：让 3D 体素地图与 2D 地图在同种子下使用同一套 biome 分类管线，
    /// 确保 2D 地图的草原 → 3D 地图同位置也是 Plains。
    /// </para>
    /// <para>
    /// 2D 比 3D 多出来的 biome（Swamp / Rainforest）回落到最相近的 3D biome
    /// （都映射到 Forest）—— 等 3D 增加对应贴图后再细分。
    /// </para>
    /// </summary>
    public static class TileTypeIdToVoxelBiome
    {
        /// <summary>把 2D TileType 字符串 ID 映射到 3D <see cref="VoxelBiomeIds"/>。
        /// 未知 / 空字符串 → Plains（默认草地，安全 fallback）。</summary>
        public static byte Map(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return VoxelBiomeIds.Plains;

            switch (typeId)
            {
                // ─── 水体（含河流 / 湖泊）──────────────────────────
                case TileTypes.Ocean:
                case TopDownTileTypes.DeepOcean:
                case TopDownTileTypes.ShallowOcean:
                case TopDownTileTypes.River:
                case TopDownTileTypes.Lake:
                    return VoxelBiomeIds.Ocean;

                // ─── 海岸 ───────────────────────────────────────
                case TopDownTileTypes.Beach:
                    return VoxelBiomeIds.Beach;

                // ─── 山系 ───────────────────────────────────────
                case TopDownTileTypes.Hill:     return VoxelBiomeIds.Hills;
                case TopDownTileTypes.Mountain: return VoxelBiomeIds.Mountain;
                case TopDownTileTypes.SnowPeak: return VoxelBiomeIds.SnowPeak;

                // ─── 寒带 ───────────────────────────────────────
                case TopDownTileTypes.Tundra: return VoxelBiomeIds.Tundra;
                case TopDownTileTypes.Taiga:  return VoxelBiomeIds.Taiga;

                // ─── 温带 ───────────────────────────────────────
                case TopDownTileTypes.Grassland: return VoxelBiomeIds.Plains;
                case TopDownTileTypes.Forest:    return VoxelBiomeIds.Forest;
                case TopDownTileTypes.Swamp:     return VoxelBiomeIds.Forest; // 暂归 Forest

                // ─── 热带 ───────────────────────────────────────
                case TopDownTileTypes.Desert:     return VoxelBiomeIds.Desert;
                case TopDownTileTypes.Savanna:    return VoxelBiomeIds.Savanna;
                case TopDownTileTypes.Rainforest: return VoxelBiomeIds.Forest; // 暂归 Forest

                // ─── 默认 ───────────────────────────────────────
                default: return VoxelBiomeIds.Plains;
            }
        }
    }
}
