using System;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Persistence.Dao
{
    /// <summary>
    /// 单列体素覆盖记录（区块级存档差量项）—— 与 2D <c>TileOverride</c> 平行。
    /// <para>
    /// 仅记录被业务层显式改写过的 column（玩家挖/放/改高度）。生成器输出不入此列表 ——
    /// 加载时永远先跑 <c>IVoxelMapGenerator.Generate</c> 重新生成默认地形，再应用 override 列表。
    /// </para>
    /// <para>**对比 2D**：2D 只覆盖 <c>TypeId</c>；3D 体素 column 三值同时变化（顶/侧/高度），
    /// 因此这里整体存为一个 record，避免分 3 个 list 写盘。</para>
    /// </summary>
    [Serializable]
    public struct VoxelColumnOverride
    {
        /// <summary>区块内本地 X（行主序），范围 [0, ChunkSize)。</summary>
        public int LocalX;
        /// <summary>区块内本地 Z，范围 [0, ChunkSize)。</summary>
        public int LocalZ;
        /// <summary>覆盖后的顶面方块 ID（<c>VoxelBlockTypes</c> 常量）。</summary>
        public byte TopBlock;
        /// <summary>覆盖后的侧面方块 ID。</summary>
        public byte SideBlock;
        /// <summary>覆盖后的列高度（地表 y）。</summary>
        public byte Height;
    }
}
