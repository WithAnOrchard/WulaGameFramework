using System;

namespace EssSystem.EssManager.MapManager.Dao
{
    /// <summary>
    /// 区块 —— <see cref="Size"/> × <see cref="Size"/> 个 <see cref="Tile"/> 的扁平数组容器。
    /// <para>
    /// 区块坐标 <see cref="ChunkX"/> / <see cref="ChunkY"/> 表示该区块在世界中的整数偏移
    /// （世界 Tile 坐标 = ChunkCoord * Size + LocalCoord）。
    /// </para>
    /// <para>
    /// 数组按行主序展平：<c>Tiles[ly * Size + lx]</c>。
    /// </para>
    /// </summary>
    [Serializable]
    public class Chunk
    {
        public int ChunkX;
        public int ChunkY;
        public int Size;
        public Tile[] Tiles;

        public Chunk() { }

        public Chunk(int chunkX, int chunkY, int size)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            Size = size;
            Tiles = new Tile[size * size];
        }

        /// <summary>按本地坐标取 Tile（不做边界检查，调用方保证 0 ≤ lx,ly &lt; Size）。</summary>
        public Tile GetTile(int lx, int ly) => Tiles[ly * Size + lx];

        /// <summary>按本地坐标设 Tile（不做边界检查）。</summary>
        public void SetTile(int lx, int ly, Tile tile) => Tiles[ly * Size + lx] = tile;

        /// <summary>世界 Tile 坐标 → 该区块的左下角世界 X。</summary>
        public int WorldOriginX => ChunkX * Size;

        /// <summary>世界 Tile 坐标 → 该区块的左下角世界 Y。</summary>
        public int WorldOriginY => ChunkY * Size;

        public override string ToString() => $"Chunk({ChunkX},{ChunkY}) {Size}x{Size}";
    }
}
