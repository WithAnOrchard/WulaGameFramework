using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn
{
    /// <summary>
    /// 选择性接口 —— 由 <c>IMapGenerator</c> 实现，向 spawn 装饰器及业务层暴露
    /// 单 Tile 的元数据（群系 / 海拔 / 温湿度）。
    /// <para>
    /// 内存策略：**不**把这些字段塞进 <c>Tile</c> 结构（每格 +20 字节，流式加载下成本不可接受），
    /// 改由生成器按 (worldX, worldY) 即时采样 Perlin/Voronoi 还原。
    /// 生成器对同坐标多次采样应得到相同结果（用 Seed 派生），与 <c>FillChunk</c> 同源。
    /// </para>
    /// <para>
    /// 不实现此接口的生成器（未来 WFC / 手绘读图 / 纯随机等）：依赖 meta 的规则会在
    /// <c>EntitySpawnDecorator</c> 内自动跳过（视为不命中），不影响其它仅靠 TileTypeId 的规则。
    /// </para>
    /// </summary>
    public interface IMapMetaProvider
    {
        /// <summary>
        /// 按世界 Tile 坐标查询元数据。无可用数据返回 false（如坐标超出生成器认知范围）。
        /// </summary>
        bool TryGetTileMeta(int worldX, int worldY, out TileMeta meta);
    }
}
