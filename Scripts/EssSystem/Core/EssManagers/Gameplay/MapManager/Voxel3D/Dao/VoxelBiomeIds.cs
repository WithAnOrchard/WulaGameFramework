namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 3D 体素生物群系 ID 常量（与 2D <c>TopDownTileTypes</c> 对应的子集，去掉了
    /// Voxel 阶段还无法可视化区分的 Swamp/Rainforest 等）。
    /// <para>顺序 = byte 值；新增请追加在末尾，避免老存档的 Biomes 数组解读偏移。</para>
    /// </summary>
    public static class VoxelBiomeIds
    {
        public const byte Ocean        = 0;  // 水面
        public const byte Beach        = 1;  // 海岸沙滩
        public const byte Plains       = 2;  // 平原（草地，干燥温带）
        public const byte Forest       = 3;  // 森林（草地，湿润温带）
        public const byte Desert       = 4;  // 沙漠（沙，热带干燥）
        public const byte Savanna      = 5;  // 热带草原（草，热带半干）
        public const byte Taiga        = 6;  // 寒带针叶林（草，寒带湿润）
        public const byte Tundra       = 7;  // 苔原（雪+土，寒带干燥）
        public const byte Hills        = 8;  // 丘陵（草顶 + 石侧）
        public const byte Mountain     = 9;  // 山地（石顶 + 石侧）
        public const byte SnowPeak     = 10; // 雪峰（雪顶 + 石侧）

        /// <summary>群系总数 —— <see cref="VoxelBiomes"/>.Profiles 长度按此分配。</summary>
        public const int Count = 11;
    }
}
