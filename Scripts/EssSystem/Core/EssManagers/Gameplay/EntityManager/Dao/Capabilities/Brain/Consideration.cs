using System;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Brain
{
    /// <summary>
    /// 候选行为 —— Utility AI 的基本决策单元。
    /// <para>
    /// 每次决策周期，BrainComponent 对所有 Consideration 调 <see cref="Score"/> 打分（0~1），
    /// 最高分者通过 <see cref="CreateAction"/> 生成 <see cref="IBrainAction"/> 执行。
    /// </para>
    /// </summary>
    public class Consideration
    {
        /// <summary>行为标识（调试 / 日志用）。</summary>
        public string Id;

        /// <summary>
        /// 打分函数 —— 返回 0~1，越高越优先。
        /// <para>返回 0 或负数表示"不参与竞争"。</para>
        /// </summary>
        public Func<BrainContext, float> Score;

        /// <summary>
        /// 行为工厂 —— 当本 Consideration 胜出时调用，创建要执行的 Action 实例。
        /// <para>每次胜出都重新创建（允许 Action 是有状态的一次性对象）。</para>
        /// </summary>
        public Func<BrainContext, IBrainAction> CreateAction;

        /// <summary>
        /// 冷却时间（秒）—— 该行为执行完毕后多久内不再参与打分。0 = 无冷却。
        /// </summary>
        public float Cooldown;

        // ─── 运行时状态（BrainComponent 内部维护） ─────────────────
        internal float CooldownRemaining;
    }
}
