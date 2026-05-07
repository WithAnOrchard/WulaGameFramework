namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao
{
    /// <summary>
    /// Character 整体渲染模式 —— 决定 <see cref="EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime.CharacterView"/>
    /// 为每个 Part 实例化哪种 PartView 组件。
    /// <list type="bullet">
    /// <item><see cref="Sprite2D"/>：每个 Part = SpriteRenderer + 帧序列（默认值，2D 流程）</item>
    /// <item><see cref="Prefab3D"/>：每个 Part = 加载 Prefab/FBX 实例化 + 现有 AnimatorController 状态切换（需 .controller 资产）</item>
    /// <item><see cref="Prefab3DClips"/>：每个 Part = 加载 FBX 实例化 + Playables 直接播 AnimationClip（**零配置**，无需 AnimatorController）</item>
    /// </list>
    /// </summary>
    public enum CharacterRenderMode
    {
        /// <summary>2D Sprite 模式（默认；保持向后兼容）。</summary>
        Sprite2D = 0,

        /// <summary>3D Prefab + AnimatorController 模式（要求 Prefab/FBX 上挂好 AnimatorController）。</summary>
        Prefab3D = 1,

        /// <summary>
        /// 3D Prefab + Playables Clip 模式 —— 直接播 FBX 内的 AnimationClip，
        /// <b>不需要任何 AnimatorController 资产</b>。运行时用 <c>AnimationMixerPlayable</c> 做 CrossFade。
        /// </summary>
        Prefab3DClips = 2,
    }
}
