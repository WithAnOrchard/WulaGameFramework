using System;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Persistence.Dao
{
    /// <summary>
    /// 单个体素区块的存档差量数据（与 2D <c>ChunkSaveData</c> 平行）。
    /// <para>**不存地形**：地形由 <c>IVoxelMapGenerator.Generate</c> 确定性派生，
    /// 存档只记录玩家改过的 column（顶/侧/高度三值）。</para>
    /// <para>持久化时被聚合进 <see cref="VoxelRegionSaveData"/>，路径：
    /// <c>{persistentDataPath}/VoxelMapData/{MapId}/Chunks/r_{RegionX}_{RegionZ}.json</c></para>
    /// </summary>
    [Serializable]
    public class VoxelChunkSaveData
    {
        public string MapId;
        public int ChunkX;
        public int ChunkZ;

        /// <summary>玩家修改过的 column（差量）。</summary>
        public List<VoxelColumnOverride> ColumnOverrides = new();

        /// <summary>已被玩家永久销毁的 spawn 实体 instanceId 列表（v2 Phase 4b 接 spawn 时使用，目前留空）。</summary>
        public List<string> DestroyedSpawnIds = new();

        public long SavedAtUnixMs;
        public int Version = 1;

        public VoxelChunkSaveData() { }

        public VoxelChunkSaveData(string mapId, int cx, int cz)
        {
            MapId = mapId;
            ChunkX = cx;
            ChunkZ = cz;
        }

        /// <summary>无任何差量时返回 true（卸载时据此判断是否值得写盘）。</summary>
        public bool IsEmpty =>
            (ColumnOverrides == null || ColumnOverrides.Count == 0) &&
            (DestroyedSpawnIds == null || DestroyedSpawnIds.Count == 0);
    }
}
