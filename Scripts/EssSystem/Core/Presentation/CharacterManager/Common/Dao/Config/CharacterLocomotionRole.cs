namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    /// <summary>
    /// 部件在运动 / 战斗状态机中的角色 —— 由
    /// <see cref="Runtime.CharacterView.PlayLocomotion"/> 与
    /// <see cref="Runtime.CharacterView.TriggerAttack"/> 用作分派依据。
    /// </summary>
    public enum CharacterLocomotionRole
    {
        /// <summary>普通可见装饰（默认）：只关心是否在移动 → Walk / Idle，攻击/跳跃不影响。</summary>
        Movement = 0,

        /// <summary>身体躯干：地面时 Walk / Idle，离地时 Jump。</summary>
        Body = 1,

        /// <summary>持械部件（武器 / 盾）：攻击窗口内播 Attack，其余按 Movement 走 Walk / Idle。</summary>
        Attack = 2,
    }
}
