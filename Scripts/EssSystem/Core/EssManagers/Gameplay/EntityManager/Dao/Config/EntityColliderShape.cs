namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config
{
    /// <summary>
    /// Entity 可挂的 2D 碰撞体形状。<see cref="None"/> 表示不挂。
    /// 框架目前只支持 2D（项目用 Tilemap / SpriteRenderer）。
    /// </summary>
    public enum EntityColliderShape
    {
        None = 0,
        Box = 1,
        Circle = 2,
    }
}
