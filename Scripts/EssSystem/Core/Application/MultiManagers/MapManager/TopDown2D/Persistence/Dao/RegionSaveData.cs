using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Persistence.Dao
{
    /// <summary>
    /// 区域聚合存档：把 <c>RegionSize × RegionSize</c> 个 <see cref="ChunkSaveData"/> 打包成一个文件，
    /// 减少小文件 IO 次数 + 文件系统 inode 压力。
    /// <para>
    /// 文件路径：<c>{persistentDataPath}/MapData/{MapId}/Chunks/r_{RegionX}_{RegionY}.json</c>
    /// </para>
    /// <para>
    /// 区域坐标定义：<c>RegionX = floor(ChunkX / RegionSize)</c>（向下取整支持负坐标）。
    /// 当前 <c>RegionSize = 10</c>，意味着每文件最多容纳 100 个 chunk。
    /// </para>
    /// </summary>
    [Serializable]
    public class RegionSaveData
    {
        /// <summary>所属地图 ID。</summary>
        public string MapId;

        /// <summary>区域坐标 X / Y。</summary>
        public int RegionX;
        public int RegionY;

        /// <summary>该区域内的 chunk 差量数据（无序；按需用 ChunkX/ChunkY 定位）。</summary>
        public List<ChunkSaveData> Chunks = new();

        /// <summary>最近一次写盘时间戳（调试用）。</summary>
        public long SavedAtUnixMs;

        /// <summary>存档结构版本（与 ChunkSaveData.Version 对齐）。</summary>
        public int Version = 1;

        public RegionSaveData() { }

        public RegionSaveData(string mapId, int rx, int ry)
        {
            MapId = mapId;
            RegionX = rx;
            RegionY = ry;
        }

        /// <summary>区域内没有任何 chunk 有差量（全部为 IsEmpty）→ 文件本身可删除。</summary>
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
