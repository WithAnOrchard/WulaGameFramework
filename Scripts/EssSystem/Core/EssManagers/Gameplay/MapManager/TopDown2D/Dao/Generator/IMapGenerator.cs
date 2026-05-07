namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Generator
{
    /// <summary>
    /// 地图生成策略接口 —— 由 <see cref="Config.MapConfig.CreateGenerator"/> 工厂方法产出。
    /// <para>
    /// 通用层只规定"按区块填 Tile"这一最小契约，具体如何填由各模板自行实现
    /// （Perlin / Random / WFC / 手绘读图 / ……）。
    /// </para>
    /// <para>
    /// 实现类应当是**确定性**的：相同 ChunkCoord 多次生成应得到相同结果，
    /// 以便区块卸载后再次进入时保持一致（或自行做缓存层）。
    /// </para>
    /// </summary>
    public interface IMapGenerator
    {
        /// <summary>
        /// 用 Tile 填满给定 <paramref name="chunk"/>。<see cref="Chunk.ChunkX"/> /
        /// <see cref="Chunk.ChunkY"/> / <see cref="Chunk.Size"/> 已就绪，<see cref="Chunk.Tiles"/>
        /// 数组也已分配，实现只需写入每个 slot。
        /// </summary>
        void FillChunk(Chunk chunk);

        /// <summary>
        /// 异步预热以 (chunkX, chunkY) 为中心的相关计算（如 Perlin RiverRegion）。
        /// 由 MapView 在焦点跨 Chunk 时调用一次，便于实现把重计算挪到 worker thread 完成，
        /// 主线程在真正生成 chunk 时只读取已缓存结果，零阻塞。无重计算可做的实现留空即可。
        /// </summary>
        void PrewarmAround(int chunkX, int chunkY, int chunkSize);
    }
}
