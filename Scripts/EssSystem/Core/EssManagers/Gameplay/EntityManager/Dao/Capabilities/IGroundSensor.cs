namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 地面检测能力 —— Entity 能感知自身是否站在地面上。
    /// <para>典型实现走 <c>Physics2D.Raycast</c> 向下检测；高级实现可叠加 OverlapBox / multiple feet 等。</para>
    /// </summary>
    public interface IGroundSensor : IEntityCapability
    {
        /// <summary>最近一次 <see cref="Refresh"/> 后的地面状态。</summary>
        bool IsGrounded { get; }

        /// <summary>主动刷新一次检测；返回最新的 <see cref="IsGrounded"/>。</summary>
        bool Refresh();
    }
}
