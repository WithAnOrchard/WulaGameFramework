namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 可跳跃能力 —— Entity 能向上发起一次冲量。
    /// <para>触发权 / 落地判定由业务侧（通常配合 <see cref="IGroundSensor"/>）。
    /// 本能力只关心"能否跳"和"执行一次跳"。</para>
    /// </summary>
    public interface IJumpable : IEntityCapability
    {
        /// <summary>跳跃初速（世界单位 / 秒）。</summary>
        float JumpForce { get; }

        /// <summary>本帧是否允许跳跃（如冷却 / 二段跳限制）；地面检查不在这里，由调用方协调。</summary>
        bool CanJump { get; }

        /// <summary>执行一次跳跃；不可跳时返回 false。</summary>
        bool Jump();
    }
}
