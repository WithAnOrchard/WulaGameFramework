using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 需求能力接口 —— 通用参数字典（饥饿、口渴、疲劳等），值域 0~1。
    /// <para>
    /// Brain 的 Score 函数通过 <see cref="BrainContext.GetNeed(string)"/> 读取，
    /// 业务层通过 <see cref="Set"/>/<see cref="Add"/> 修改。
    /// </para>
    /// <para>
    /// 实现 <see cref="ITickableCapability"/> 时可自动按速率增长需求（如每秒饥饿 +0.01）。
    /// </para>
    /// </summary>
    public interface INeeds : IEntityCapability
    {
        /// <summary>读取需求值（0~1）。不存在返回 0。</summary>
        float Get(string needId);

        /// <summary>设置需求值（自动 Clamp 到 0~1）。</summary>
        void Set(string needId, float value);

        /// <summary>增量修改需求值（正 = 增长，负 = 满足）。</summary>
        void Add(string needId, float delta);

        /// <summary>所有需求的只读快照。</summary>
        IReadOnlyDictionary<string, float> All { get; }
    }
}
