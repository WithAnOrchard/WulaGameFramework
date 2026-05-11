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

    /// <summary>
    /// Atlas 槽位索引（与 <see cref="Runtime.VoxelTextureAtlas"/> 的 SlotBindings 一一对应）。
    /// <para>当前 8×4 = 32 slot 布局：dirt / stone 各留一段连续区间存 OptiFine 风格 random variants，
    /// 其它方块单 slot。<see cref="SlotForTop"/> / <see cref="SlotForSide"/> 在 (wx, wz) 上做整数哈希
    /// 决定该列拿哪一变体 —— 同坐标永远同变体（确定性）。</para>
    /// </summary>
    public static class VoxelAtlasSlots
    {
        public const int GrassTop          = 0;
        public const int GrassSide         = 1;
        public const int GrassSideSnowed   = 2;

        /// <summary>Dirt 变体起始 slot（连续 <see cref="DirtVariantCount"/> 个）。</summary>
        public const int DirtBase          = 3;
        public const int DirtVariantCount  = 13;   // dirt.png + dirt1..dirt12

        /// <summary>Stone 变体起始 slot（连续 <see cref="StoneVariantCount"/> 个）。</summary>
        public const int StoneBase         = 16;
        public const int StoneVariantCount = 9;    // stone.png + stone1..stone8

        public const int Sand              = 25;
        public const int Snow              = 26;
        public const int WaterStill        = 27;

        /// <summary>总 slot 数（atlas 分配 _slotUVs 长度；含 padding 至 8×4）。</summary>
        public const int Count = 32;

        // ── 旧名兼容：单 slot 引用（指向变体段首个 = 基础贴图，无变体）──
        public const int Dirt  = DirtBase;
        public const int Stone = StoneBase;

        /// <summary>把 BlockId 映射到顶面 atlas slot；dirt/stone 在 (wx, wz) 上哈希取变体。</summary>
        public static int SlotForTop(byte blockId, int wx, int wz)
        {
            switch (blockId)
            {
                case VoxelBlockTypes.Grass: return GrassTop;
                case VoxelBlockTypes.Dirt:  return DirtBase  + (int)(Hash2D(wx, wz)        % (uint)DirtVariantCount);
                case VoxelBlockTypes.Stone: return StoneBase + (int)(Hash2D(wx, wz)        % (uint)StoneVariantCount);
                case VoxelBlockTypes.Sand:  return Sand;
                case VoxelBlockTypes.Snow:  return Snow;
                case VoxelBlockTypes.Water: return WaterStill;
                default: return StoneBase;
            }
        }

        /// <summary>把 BlockId 映射到侧面 atlas slot；草侧用 grass_side；
        /// dirt/stone 用 (wx, wz) + 1 偏移哈希避免顶/侧总是相同变体。</summary>
        public static int SlotForSide(byte blockId, int wx, int wz)
        {
            switch (blockId)
            {
                case VoxelBlockTypes.Grass: return GrassSide;
                // 偏移让侧面变体与顶面变体不锁定（同列 dirt 顶/侧若同变体没意义，错开更自然）
                case VoxelBlockTypes.Dirt:  return DirtBase  + (int)(Hash2D(wx + 1, wz - 1) % (uint)DirtVariantCount);
                case VoxelBlockTypes.Stone: return StoneBase + (int)(Hash2D(wx + 1, wz - 1) % (uint)StoneVariantCount);
                case VoxelBlockTypes.Sand:  return Sand;
                case VoxelBlockTypes.Snow:  return Snow;
                case VoxelBlockTypes.Water: return WaterStill;
                default: return StoneBase;
            }
        }

        // ── 旧 API 兼容（无 wx/wz 时退回到基础 slot，不做变体）──
        public static int SlotForTop(byte blockId)  => SlotForTop(blockId, 0, 0);
        public static int SlotForSide(byte blockId) => SlotForSide(blockId, 0, 0);

        /// <summary>2D 整数哈希（无符号），变体散列用。FNV-like 大质数乘积异或，确定性。</summary>
        private static uint Hash2D(int x, int z)
        {
            unchecked
            {
                var ux = (uint)x * 73856093u;
                var uz = (uint)z * 19349663u;
                var h  = ux ^ uz;
                h ^= h >> 16;
                h *= 0x7feb352du;
                h ^= h >> 15;
                return h;
            }
        }
    }
}
