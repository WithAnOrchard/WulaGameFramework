using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao
{
    /// <summary>
    /// 技能效果接口 —— 技能命中时执行的单个效果。
    /// <para>
    /// 多个 ISkillEffect 组成效果链，由 <see cref="SkillExecutor"/> 按顺序 Apply。
    /// 实现类应是无状态的纯函数式设计（参数通过 <see cref="SkillEffectContext"/> 传入）。
    /// </para>
    /// </summary>
    public interface ISkillEffect
    {
        /// <summary>执行效果。</summary>
        void Apply(SkillEffectContext ctx);
    }

    /// <summary>
    /// 技能效果执行上下文 —— 传递施法者、目标、技能实例等信息。
    /// </summary>
    public class SkillEffectContext
    {
        /// <summary>施法者实体。</summary>
        public Entity Caster;

        /// <summary>目标实体（可为 null，如 AOE / 自身技能）。</summary>
        public Entity Target;

        /// <summary>技能定义。</summary>
        public SkillDefinition Definition;

        /// <summary>技能实例（含当前等级等运行时状态）。</summary>
        public SkillInstance Instance;

        /// <summary>施放方向（归一化，横版通常只有 X）。</summary>
        public UnityEngine.Vector3 Direction;

        /// <summary>施放位置（AOE 中心 / 投射物起点）。</summary>
        public UnityEngine.Vector3 Position;

        /// <summary>技能当前等级。</summary>
        public int Level => Instance?.Level ?? 1;
    }
}
