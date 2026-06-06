namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config
{
    /// <summary>
    /// Entity 可挂的碰撞体形状。<see cref="None"/> 表示不挂（业务方自己挂 Collider）。
    /// <para>
    /// 2D 系列（<see cref="Box"/> / <see cref="Circle"/>）挂 <c>BoxCollider2D</c> / <c>CircleCollider2D</c>，
    /// 配合 <c>Rigidbody2D</c> 走 Physics2D 系统；3D 系列（<see cref="Box3D"/> / <see cref="Sphere3D"/>
    /// / <see cref="Capsule3D"/>）挂 <c>BoxCollider</c> / <c>SphereCollider</c> / <c>CapsuleCollider</c>，
    /// 配合 <c>Rigidbody</c> 走 Physics 系统。
    /// </para>
    /// </summary>
    public enum EntityColliderShape
    {
        None = 0,
        Box = 1,
        Circle = 2,
        // ── 3D 系列（项目从 2D 切 3D 后新增）──────────────────
        Box3D = 3,
        Sphere3D = 4,
        Capsule3D = 5,
    }
}
