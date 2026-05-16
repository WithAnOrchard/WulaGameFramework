namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Generator
{
    /// <summary>
    /// 区块装饰器 —— 在 <see cref="IMapGenerator.FillChunk"/> 完成地形填充后，
    /// 对同一 <see cref="Chunk"/> 做二次加工（生成植物 / 生物 / 建筑 / 道具 / 结构等）。
    /// <para>
    /// 注册方式：<c>MapService.Instance.RegisterDecorator(new MyDecorator())</c>。
    /// 注册后所有后续创建的 <see cref="Map"/> 的新区块会按 <see cref="Priority"/>
    /// 从小到大依次调用 <see cref="Decorate"/>，装饰器全部跑完才会广播
    /// <c>MapService.ChunkGenerated</c> 给业务层。
    /// </para>
    /// <para>
    /// **确定性约定**：实现类不应使用全局随机或帧时间。若需要随机，请通过
    /// <see cref="Dao.Util.ChunkSeed.Rng"/> 基于 <c>(mapId, chunkCoord, tag)</c>
    /// 派生独立 <c>System.Random</c>，保证同一区块每次生成的分布一致、支持区块卸载/重载。
    /// </para>
    /// <para>
    /// **线程约束**：当前 <see cref="Map.GetOrGenerateChunk"/> 在主线程懒调用，
    /// 装饰器可直接创建 Unity GameObject / 访问场景。后续若接入异步生成，
    /// 装饰器需自行把 Unity API 调用切回主线程。
    /// </para>
    /// <para>
    /// **生命周期配对**：通常装饰器负责 spawn，但 despawn 应由业务层监听
    /// <c>MapService.ChunkUnloaded</c> 自行处理（装饰器本身不持状态，便于热插拔）。
    /// </para>
    /// </summary>
    public interface IChunkDecorator
    {
        /// <summary>唯一 ID。用于 <c>UnregisterDecorator</c> 注销与日志定位。</summary>
        string Id { get; }

        /// <summary>执行顺序，越小越先。例如：地面装饰(100) → 植被(200) → 生物(300) → 结构(400)。</summary>
        int Priority { get; }

        /// <summary>装饰回调。<paramref name="chunk"/> 的 <c>Tiles</c> 已由生成器填好。</summary>
        void Decorate(Map map, Chunk chunk);
    }
}
