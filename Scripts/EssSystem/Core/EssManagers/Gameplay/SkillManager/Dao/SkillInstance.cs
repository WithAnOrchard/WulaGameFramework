namespace EssSystem.Core.EssManagers.Gameplay.SkillManager.Dao
{
    /// <summary>
    /// 技能运行时实例 —— 绑定到某个实体的一个技能的实时状态。
    /// <para>包含冷却计时、当前等级、是否解锁等可变状态。</para>
    /// </summary>
    public class SkillInstance
    {
        /// <summary>关联的技能定义 ID。</summary>
        public string SkillId;

        /// <summary>技能定义引用（运行时缓存）。</summary>
        public SkillDefinition Definition;

        /// <summary>当前等级（1-based）。</summary>
        public int Level = 1;

        /// <summary>是否已解锁（可使用）。</summary>
        public bool Unlocked = true;

        /// <summary>剩余冷却时间（秒），0 = 可用。</summary>
        public float CooldownRemaining;

        /// <summary>冷却是否就绪。</summary>
        public bool IsReady => Unlocked && CooldownRemaining <= 0f;

        /// <summary>推进冷却计时。</summary>
        public void TickCooldown(float deltaTime)
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = UnityEngine.Mathf.Max(0f, CooldownRemaining - deltaTime);
        }

        /// <summary>开始冷却。</summary>
        public void StartCooldown()
        {
            if (Definition != null)
                CooldownRemaining = Definition.Cooldown;
        }
    }
}
