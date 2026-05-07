using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 体素地图生成器策略接口（与 2D 的 <c>IMapGenerator</c> 平行）。
    /// <para>实现需保证：同 (Seed, ChunkCoord) 永远生成同一份 <see cref="VoxelChunk"/>，
    /// 不依赖全局随机 / 帧时间 / 静态可变状态 —— 这是流式重生成 + 持久化覆盖的前提。</para>
    /// </summary>
    public interface IVoxelMapGenerator
    {
        /// <summary>把 (chunkX, chunkZ) 烘成完整 <see cref="VoxelChunk"/>（heightmap + 顶/侧 BlockId）。</summary>
        VoxelChunk Generate(int chunkX, int chunkZ);

        /// <summary>给定世界 (wx, wz) 直接出地表高度（不构建 chunk）。
        /// 用于 spawn 落地点查询 / 流式半径检查等场景。</summary>
        int SampleHeight(int wx, int wz);
    }
}
