namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 需要每帧驱动的能力 —— 实现此接口的能力会被 <see cref="EntityService"/> 在
    /// <c>Tick</c> 中自动调用 <see cref="Tick"/>，无需外部手动 cast + 调用。
    /// </summary>
    public interface ITickableCapability : IEntityCapability
    {
        /// <summary>每帧推进（由框架自动调用）。</summary>
        void Tick(float deltaTime);
    }
}
