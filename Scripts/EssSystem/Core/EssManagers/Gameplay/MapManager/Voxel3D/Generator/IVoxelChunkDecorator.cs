using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 体素区块装饰器 —— 在 <see cref="IVoxelMapGenerator.Generate"/> 完成 heightmap 填充后，
    /// 对同一 <see cref="VoxelChunk"/> 做二次加工（生成树木 / 怪物 / 结构等）。
    /// <para>注册方式：<c>Voxel3DMapService.Instance.RegisterDecorator(new MyDecorator())</c>。
    /// 注册后所有后续创建的 <see cref="VoxelMap"/> 的新区块按 <see cref="Priority"/> 升序依次执行
    /// <see cref="Decorate"/>，全部跑完才广播 <c>Voxel3DMapService.ChunkGenerated</c> 给业务层。</para>
    /// <para>**确定性约定**：实现类不应使用全局随机或帧时间。需要随机时基于 (mapId, chunkCoord, tag)
    /// 派生独立 <c>System.Random</c>，保证同一区块每次生成的分布一致、支持卸载/重载。</para>
    /// </summary>
    public interface IVoxelChunkDecorator
    {
        /// <summary>唯一 ID。用于 <c>UnregisterDecorator</c> 注销与日志定位。</summary>
        string Id { get; }

        /// <summary>执行顺序，越小越先（地形装饰 100 → 植被 200 → 生物 300 → 结构 400）。</summary>
        int Priority { get; }

        /// <summary>装饰回调。<paramref name="chunk"/> 的 heightmap / TopBlocks / SideBlocks 已填好。</summary>
        void Decorate(VoxelMap map, VoxelChunk chunk);
    }
}
