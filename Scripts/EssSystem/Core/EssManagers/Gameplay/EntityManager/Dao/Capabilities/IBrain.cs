using System.Collections.Generic;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Brain;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// Brain 能力接口 —— Utility AI 决策引擎。
    /// <para>
    /// 实现为 <see cref="ITickableCapability"/>，由 EntityService.Tick 自动驱动。
    /// 每个决策周期对所有 <see cref="Considerations"/> 打分，最高分者执行对应 Action。
    /// </para>
    /// <para>
    /// <b>互斥规则</b>：挂载 IBrain 的 Entity 不应同时挂 <see cref="IPatrol"/>（Brain 接管移动决策）。
    /// Entity.CanThink() 链式方法会自动移除 IPatrol。
    /// </para>
    /// </summary>
    public interface IBrain : ITickableCapability
    {
        /// <summary>决策上下文（黑板）。</summary>
        BrainContext Context { get; }

        /// <summary>是否启用（false 时 Tick 空跑，当前 Action 保持暂停）。</summary>
        bool Enabled { get; set; }

        /// <summary>当前正在执行的 Action（null = 空闲）。</summary>
        IBrainAction CurrentAction { get; }

        /// <summary>当前胜出的 Consideration ID（调试用）。</summary>
        string CurrentConsiderationId { get; }

        /// <summary>所有候选行为。</summary>
        IReadOnlyList<Consideration> Considerations { get; }
    }
}
