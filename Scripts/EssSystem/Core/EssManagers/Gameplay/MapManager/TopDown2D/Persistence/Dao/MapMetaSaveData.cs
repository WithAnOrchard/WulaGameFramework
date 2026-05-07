using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Persistence.Dao
{
    /// <summary>
    /// 地图级元数据存档（每张地图一份）。
    /// <para>
    /// 文件路径：<c>{persistentDataPath}/MapData/{MapId}/Meta.json</c>
    /// </para>
    /// <para>
    /// 主要用途：
    /// <list type="bullet">
    /// <item>启动时检测某 MapId 是否有过存档（判断"新建 vs 续存"）</item>
    /// <item>调试 / 版本迁移（<see cref="Version"/>）</item>
    /// <item>种子一致性校验（<see cref="Seed"/> / <see cref="ConfigId"/> 与当前配置不一致时按策略处理）</item>
    /// </list>
    /// </para>
    /// </summary>
    [Serializable]
    public class MapMetaSaveData
    {
        public string MapId;
        public string ConfigId;
        public int ChunkSize;
        /// <summary>该存档创建时使用的种子（生成器自报，TopDownRandom 取 <c>PerlinMapConfig.Seed</c>）。</summary>
        public int Seed;
        /// <summary>
        /// 该存档创建时 <c>MapConfig</c> 的完整 JSON 快照（含派生字段）。
        /// <para>启动时与当前 ConfigId 的最新 JSON 字符串比对：不一致即视为配置变更，
        /// 已有 chunk 文件保留原内容（边界可能割裂），新 chunk 用最新 Seed 生成。</para>
        /// </summary>
        public string ConfigJsonSnapshot;
        public long CreatedAtUnixMs;
        public long LastSavedAtUnixMs;
        /// <summary>v1 = 1。结构升级时 +1，<c>MapPersistenceService</c> 据此决定是否迁移或重建。</summary>
        public int Version = 1;

        public MapMetaSaveData() { }

        public MapMetaSaveData(string mapId, string configId, int chunkSize, int seed)
        {
            MapId = mapId;
            ConfigId = configId;
            ChunkSize = chunkSize;
            Seed = seed;
        }
    }
}
