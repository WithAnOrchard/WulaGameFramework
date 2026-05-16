namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Brain
{
    /// <summary>
    /// Brain Action 执行状态。
    /// </summary>
    public enum BrainStatus
    {
        /// <summary>动作仍在进行中，下帧继续 Tick。</summary>
        Running,
        /// <summary>动作成功完成。</summary>
        Success,
        /// <summary>动作失败（目标丢失、不可达等）。</summary>
        Failure
    }
}
