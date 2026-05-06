using System;

namespace EssSystem.EssManager.MapManager.Persistence.Dao
{
    /// <summary>
    /// 单个 Tile 的玩家覆盖记录（区块级存档差量项）。
    /// <para>
    /// 仅记录被业务层显式改写过的 Tile（如挖矿/铺路）。生成器输出不入此列表 ——
    /// 加载时永远先跑 <c>IMapGenerator.FillChunk</c> 重新生成默认地形，再应用 override 列表。
    /// 这样地形数据本身**不需要存盘**，存储成本只与玩家修改量成正比。
    /// </para>
    /// <para>
    /// 仅覆盖 <see cref="EssSystem.EssManager.MapManager.Dao.Tile.TypeId"/>；
    /// 其余字段（Elevation/Temperature/Moisture/RiverFlow）保持生成器值。
    /// </para>
    /// </summary>
    [Serializable]
    public struct TileOverride
    {
        /// <summary>区块内本地坐标 X，范围 [0, ChunkSize)。</summary>
        public int LocalX;
        /// <summary>区块内本地坐标 Y，范围 [0, ChunkSize)。</summary>
        public int LocalY;
        /// <summary>覆盖后的 TypeId。</summary>
        public string TypeId;
    }
}
