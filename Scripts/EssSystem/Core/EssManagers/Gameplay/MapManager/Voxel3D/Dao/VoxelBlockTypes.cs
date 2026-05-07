using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 内置方块常量 + 默认调色板（MC 风纯色）。后续接素材时改 <see cref="DefaultPalette"/> 即可。
    /// <para>ID 约定：0=Air，1..N 为实心。</para>
    /// </summary>
    public static class VoxelBlockTypes
    {
        public const byte Air    = 0;
        public const byte Grass  = 1;
        public const byte Dirt   = 2;
        public const byte Stone  = 3;
        public const byte Sand   = 4;
        public const byte Snow   = 5;
        public const byte Water  = 6;

        /// <summary>默认调色板（顶面 / 侧面）。索引 = Block ID。</summary>
        public static VoxelBlockType[] DefaultPalette => new[]
        {
            new VoxelBlockType(Air,   "Air",   new Color32(  0,  0,  0,  0), new Color32(  0,  0,  0,  0), solid: false),
            new VoxelBlockType(Grass, "Grass", new Color32( 90,170, 70,255), new Color32(134, 96, 67,255)),
            new VoxelBlockType(Dirt,  "Dirt",  new Color32(134, 96, 67,255), new Color32(134, 96, 67,255)),
            new VoxelBlockType(Stone, "Stone", new Color32(125,125,130,255), new Color32(125,125,130,255)),
            new VoxelBlockType(Sand,  "Sand",  new Color32(225,210,150,255), new Color32(225,210,150,255)),
            new VoxelBlockType(Snow,  "Snow",  new Color32(245,245,250,255), new Color32(220,225,230,255)),
            new VoxelBlockType(Water, "Water", new Color32( 60,110,200,200), new Color32( 60,110,200,200), solid: false),
        };
    }
}
