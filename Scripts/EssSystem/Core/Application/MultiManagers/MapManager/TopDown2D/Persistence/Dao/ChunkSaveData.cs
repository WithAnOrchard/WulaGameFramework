using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Persistence.Dao
{
    /// <summary>
    /// 单个区块的存档差量数据。
    /// <para>
    /// **不存地形**：地形由 <c>IMapGenerator.FillChunk</c> 确定性派生，存档只记录"玩家改过的内容"。
    /// </para>
    /// <para>
    /// 持久化时被聚合进 <see cref="RegionSaveData"/>，每个 region 文件存
    /// <c>RegionSize²</c>（默认 10×10 = 100）个 chunk 的差量；路径：
    /// <c>{persistentDataPath}/MapData/{MapId}/Chunks/r_{RegionX}_{RegionY}.json</c>
    /// </para>
    /// </summary>
    [Serializable]
    public class ChunkSaveData
    {
        /// <summary>所属地图 ID（冗余字段，便于离线工具一眼分辨）。</summary>
        public string MapId;

        /// <summary>区块坐标 X。</summary>
        public int ChunkX;

        /// <summary>区块坐标 Y。</summary>
        public int ChunkY;

        /// <summary>已被玩家永久销毁的 spawn 实体 instanceId 列表。</summary>
        public List<string> DestroyedSpawnIds = new();

        /// <summary>玩家修改过的 Tile（差量）。</summary>
        public List<TileOverride> TileOverrides = new();

        /// <summary>玩家手动放置的实体（v2 预留，v1 写空 list）。</summary>
        public List<PlacedSpawn> PlacedSpawns = new();

        /// <summary>最近一次写盘时的 Unix ms 时间戳（仅调试 / 排序用，无业务语义）。</summary>
        public long SavedAtUnixMs;

        /// <summary>存档结构版本号；不同版本之间的迁移由 <see cref="MapPersistenceService"/> 决定（v1 = 1）。</summary>
        public int Version = 1;

        public ChunkSaveData() { }

        public ChunkSaveData(string mapId, int cx, int cy)
        {
            MapId = mapId;
            ChunkX = cx;
            ChunkY = cy;
        }

        /// <summary>无任何差量时返回 true（用于卸载时判断是否值得写盘）。</summary>
        public bool IsEmpty =>
            (DestroyedSpawnIds == null || DestroyedSpawnIds.Count == 0) &&
            (TileOverrides == null || TileOverrides.Count == 0) &&
            (PlacedSpawns == null || PlacedSpawns.Count == 0);
    }
}
