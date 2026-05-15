namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 穿墙能力 —— Entity 能在普通碰撞与穿透状态间切换。
    /// <para>典型实现切换 <c>Collider2D.isTrigger</c> 或 Layer，实体可临时无视障碍。</para>
    /// </summary>
    public interface IPhaseThrough : IEntityCapability
    {
        /// <summary>当前是否处于穿墙状态。</summary>
        bool PhasingThrough { get; }

        /// <summary>切到指定穿墙状态；与当前相同则忽略。</summary>
        void SetPhasing(bool phasing);
    }
}
