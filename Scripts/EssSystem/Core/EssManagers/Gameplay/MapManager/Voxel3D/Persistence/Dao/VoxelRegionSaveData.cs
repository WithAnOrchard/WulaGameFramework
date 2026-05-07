using System;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Persistence.Dao
{
    /// <summary>
    /// 区域聚合存档：把 <c>RegionSize × RegionSize</c> 个 <see cref="VoxelChunkSaveData"/> 打包成一个文件，
    /// 减少小文件 IO 次数（与 2D <c>RegionSaveData</c> 平行）。
    /// <para>区域坐标 <c>(RegionX, RegionZ) = (floor(cx / RegionSize), floor(cz / RegionSize))</c>。
    /// 当前 RegionSize=10（在 <c>VoxelMapPersistenceService.REGION_SIZE</c>），意味着每文件最多 100 个 chunk。</para>
    /// </summary>
    [Serializable]
    public class VoxelRegionSaveData
    {
        public string MapId;
        public int RegionX;
        public int RegionZ;

        /// <summary>该区域内的 chunk 差量（无序；按需用 ChunkX/ChunkZ 定位）。</summary>
        public List<VoxelChunkSaveData> Chunks = new();

        public long SavedAtUnixMs;
        public int Version = 1;

        public VoxelRegionSaveData() { }

        public VoxelRegionSaveData(string mapId, int rx, int rz)
        {
            MapId = mapId;
            RegionX = rx;
            RegionZ = rz;
        }

        /// <summary>区域内全部 chunk 都 IsEmpty → 文件可删除。</summary>
        public bool IsEmpty
        {
            get
            {
                if (Chunks == null || Chunks.Count == 0) return true;
                for (var i = 0; i < Chunks.Count; i++)
                {
                    var c = Chunks[i];
                    if (c != null && !c.IsEmpty) return false;
                }
                return true;
            }
        }
    }
}
