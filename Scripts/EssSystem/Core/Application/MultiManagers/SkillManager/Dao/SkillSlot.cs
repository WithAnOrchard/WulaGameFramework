namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao
{
    /// <summary>
    /// 技能槽位 —— 快捷栏中的一个格子，绑定一个 <see cref="SkillInstance"/>。
    /// </summary>
    public class SkillSlot
    {
        /// <summary>槽位索引（0-based）。</summary>
        public int Index;

        /// <summary>绑定的技能实例（null = 空槽）。</summary>
        public SkillInstance Skill;

        /// <summary>槽位是否为空。</summary>
        public bool IsEmpty => Skill == null;

        public SkillSlot(int index) => Index = index;

        /// <summary>绑定技能到此槽位。</summary>
        public void Bind(SkillInstance skill) => Skill = skill;

        /// <summary>清空槽位。</summary>
        public void Clear() => Skill = null;
    }
}
