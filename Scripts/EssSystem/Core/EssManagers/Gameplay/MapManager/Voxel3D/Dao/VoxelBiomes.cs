namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>群系外观档：决定该群系的 (顶/侧) 方块。</summary>
    public struct VoxelBiomeProfile
    {
        public byte   TopBlock;
        public byte   SideBlock;
        public string Name;

        public VoxelBiomeProfile(byte top, byte side, string name)
        {
            TopBlock  = top;
            SideBlock = side;
            Name      = name;
        }
    }

    /// <summary>
    /// 内置群系 → (Top, Side) 映射表。生成器拿到 BiomeId 后查这里写入 chunk.TopBlocks / SideBlocks。
    /// <para>未来要扩材质（如沼泽特殊水、热带丛林深绿）只需扩 atlas slot + 加群系档。</para>
    /// </summary>
    public static class VoxelBiomes
    {
        /// <summary>按 BiomeId 索引的档表；改样式只改这里。</summary>
        public static readonly VoxelBiomeProfile[] Profiles = BuildDefault();

        private static VoxelBiomeProfile[] BuildDefault()
        {
            var arr = new VoxelBiomeProfile[VoxelBiomeIds.Count];

            arr[VoxelBiomeIds.Ocean]    = new VoxelBiomeProfile(VoxelBlockTypes.Water, VoxelBlockTypes.Sand,  "Ocean");
            arr[VoxelBiomeIds.Beach]    = new VoxelBiomeProfile(VoxelBlockTypes.Sand,  VoxelBlockTypes.Sand,  "Beach");
            arr[VoxelBiomeIds.Plains]   = new VoxelBiomeProfile(VoxelBlockTypes.Grass, VoxelBlockTypes.Dirt,  "Plains");
            arr[VoxelBiomeIds.Forest]   = new VoxelBiomeProfile(VoxelBlockTypes.Grass, VoxelBlockTypes.Dirt,  "Forest");
            arr[VoxelBiomeIds.Desert]   = new VoxelBiomeProfile(VoxelBlockTypes.Sand,  VoxelBlockTypes.Sand,  "Desert");
            arr[VoxelBiomeIds.Savanna]  = new VoxelBiomeProfile(VoxelBlockTypes.Grass, VoxelBlockTypes.Dirt,  "Savanna");
            arr[VoxelBiomeIds.Taiga]    = new VoxelBiomeProfile(VoxelBlockTypes.Grass, VoxelBlockTypes.Dirt,  "Taiga");
            arr[VoxelBiomeIds.Tundra]   = new VoxelBiomeProfile(VoxelBlockTypes.Snow,  VoxelBlockTypes.Dirt,  "Tundra");
            arr[VoxelBiomeIds.Hills]    = new VoxelBiomeProfile(VoxelBlockTypes.Grass, VoxelBlockTypes.Dirt,  "Hills");
            arr[VoxelBiomeIds.Mountain] = new VoxelBiomeProfile(VoxelBlockTypes.Stone, VoxelBlockTypes.Stone, "Mountain");
            arr[VoxelBiomeIds.SnowPeak] = new VoxelBiomeProfile(VoxelBlockTypes.Snow,  VoxelBlockTypes.Stone, "SnowPeak");

            return arr;
        }
    }
}
