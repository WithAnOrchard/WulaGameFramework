namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Brain
{
    /// <summary>
    /// Brain 可执行动作接口 —— 由 <see cref="Consideration"/> 创建，被 BrainComponent 调度。
    /// <para>
    /// 生命周期：<c>OnEnter</c> → 每帧 <c>Tick</c>（直到非 Running）→ <c>OnExit</c>。
    /// 被更高优先级行为抢占时也保证调 <c>OnExit</c>。
    /// </para>
    /// </summary>
    public interface IBrainAction
    {
        /// <summary>动作开始时调用（初始化状态、播动画等）。</summary>
        void OnEnter(BrainContext ctx);

        /// <summary>每帧推进，返回当前状态。</summary>
        BrainStatus Tick(BrainContext ctx, float deltaTime);

        /// <summary>动作结束/被抢占时调用（清理状态、停动画等）。</summary>
        void OnExit(BrainContext ctx);
    }
}
