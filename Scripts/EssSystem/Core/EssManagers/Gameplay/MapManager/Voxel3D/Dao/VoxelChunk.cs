namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 单个 3D Chunk 数据（heightmap-only）。
    /// <para>
    /// 不存体素体；仅 (X, Z) 二维数组的 height + topBlock + sideBlock。
    /// 顶面在 y = height 处一格，侧面从 y = height 自上而下渲染（仅在邻居比自己矮的方向，由 Mesher 决定）。
    /// </para>
    /// </summary>
    public class VoxelChunk
    {
        public readonly int ChunkX, ChunkZ;
        public readonly int Size;

        /// <summary>每格地表最高 y（含；该格顶面在此 y 处）。</summary>
        public readonly byte[] Heights;

        /// <summary>每格顶面方块 ID（参 <see cref="VoxelBlockTypes"/>）。</summary>
        public readonly byte[] TopBlocks;

        /// <summary>每格侧面方块 ID（cliff 露出的）。</summary>
        public readonly byte[] SideBlocks;

        /// <summary>世界坐标系下 chunk 起点（minX, minZ；y 永远从 0 起）。</summary>
        public int WorldMinX => ChunkX * Size;
        public int WorldMinZ => ChunkZ * Size;

        public VoxelChunk(int chunkX, int chunkZ, int size)
        {
            ChunkX = chunkX; ChunkZ = chunkZ; Size = size;
            Heights    = new byte[size * size];
            TopBlocks  = new byte[size * size];
            SideBlocks = new byte[size * size];
        }

        /// <summary>(lx, lz) 行主序索引：lz * Size + lx。</summary>
        public int Index(int lx, int lz) => lz * Size + lx;

        public byte GetHeight(int lx, int lz)    => Heights[Index(lx, lz)];
        public byte GetTopBlock(int lx, int lz)  => TopBlocks[Index(lx, lz)];
        public byte GetSideBlock(int lx, int lz) => SideBlocks[Index(lx, lz)];
    }
}
