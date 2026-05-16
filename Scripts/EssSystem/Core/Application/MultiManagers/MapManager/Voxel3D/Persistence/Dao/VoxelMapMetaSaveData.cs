using System;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Persistence.Dao
{
    /// <summary>
    /// 体素地图级元数据存档（每张地图一份）。文件路径 <c>{persistentDataPath}/VoxelMapData/{MapId}/Meta.json</c>。
    /// <para>用途：</para>
    /// <list type="bullet">
    /// <item>启动时检测是否有过存档（"新建 vs 续存"）</item>
    /// <item>种子一致性校验（<see cref="Seed"/> 与当前配置不一致 → LogWarning，已有 chunk 文件保留）</item>
    /// <item>Config 漂移检测（<see cref="ConfigJsonSnapshot"/> 比对当前 JsonUtility.ToJson）</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class VoxelMapMetaSaveData
    {
        public string MapId;
        public string ConfigId;
        public int ChunkSize;
        public int Seed;
        public string ConfigJsonSnapshot;
        public long CreatedAtUnixMs;
        public long LastSavedAtUnixMs;
        public int Version = 1;

        public VoxelMapMetaSaveData() { }

        public VoxelMapMetaSaveData(string mapId, string configId, int chunkSize, int seed)
        {
            MapId = mapId;
            ConfigId = configId;
            ChunkSize = chunkSize;
            Seed = seed;
        }
    }
}
