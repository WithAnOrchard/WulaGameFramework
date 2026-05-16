namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    /// <summary>
    /// 角色部件类型 —— 决定运行时 <c>CharacterPartView</c> 的行为。
    /// </summary>
    public enum CharacterPartType
    {
        /// <summary>静态部件：固定一张 Sprite，不参与动画驱动。</summary>
        Static = 0,

        /// <summary>动态部件：按帧序列播放动画，可根据动作切换不同的 Sprite 列表。</summary>
        Dynamic = 1,
    }
}
