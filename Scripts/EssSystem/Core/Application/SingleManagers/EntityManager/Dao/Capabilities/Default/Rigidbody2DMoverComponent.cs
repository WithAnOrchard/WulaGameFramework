using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IMovable"/> 物理实现 —— 通过 <see cref="Rigidbody2D"/> 设置速度。
    /// <para>区别于纯逻辑的 <see cref="MovableComponent"/>（直接写 <c>Entity.WorldPosition</c>）：
    /// 本组件交由 Unity 物理积分，自动响应碰撞 / 重力，并把 <c>Entity.WorldPosition</c> 同步为 rb.position。</para>
    /// <para>支持横版（仅 X 受输入驱动，Y 由重力 / Jump 决定）与俯视（XY 都受输入驱动）两种模式。</para>
    /// </summary>
    public class Rigidbody2DMoverComponent : IMovable
    {
        public float MoveSpeed { get; protected set; }

        /// <summary>当前速度（读自 Rigidbody2D）。<c>set</c> 强制写入 rb.velocity。</summary>
        public Vector3 Velocity
        {
            get => _rb != null ? (Vector3)_rb.velocity : Vector3.zero;
            set { if (_rb != null) _rb.velocity = value; }
        }

        /// <summary>冲刺倍率（&gt;1 加速）。运行时可改。</summary>
        public float SprintMultiplier { get; set; } = 1f;

        /// <summary>本帧是否处于冲刺状态（由外部置位，例如按住 Shift）。</summary>
        public bool Sprinting { get; set; }

        /// <summary>是否启用横版模式：仅 X 受输入驱动，Y 由 Rigidbody2D 重力决定。</summary>
        public bool SideScroller { get; set; }

        private readonly Rigidbody2D _rb;
        private Entity _owner;

        public Rigidbody2DMoverComponent(Rigidbody2D rb, float moveSpeed, bool sideScroller = false)
        {
            _rb = rb;
            MoveSpeed = Mathf.Max(0f, moveSpeed);
            SideScroller = sideScroller;
        }

        public void OnAttach(Entity owner) { _owner = owner; }
        public void OnDetach(Entity owner) { _owner = null; if (_rb != null) _rb.velocity = Vector2.zero; }

        /// <summary>
        /// 把 <paramref name="direction"/> 转化为 Rigidbody2D 速度；<paramref name="deltaTime"/> 未使用（物理由 Unity 积分）。
        /// <para>Direction.x 用作横向输入；纵向是否被采纳由 <see cref="SideScroller"/> 决定。</para>
        /// </summary>
        public void Move(Vector3 direction, float deltaTime)
        {
            if (_rb == null) return;
            var speed = MoveSpeed * (Sprinting ? Mathf.Max(1f, SprintMultiplier) : 1f);
            var dir = direction.sqrMagnitude > 1f ? direction.normalized : direction;
            _rb.velocity = SideScroller
                ? new Vector2(dir.x * speed, _rb.velocity.y)
                : new Vector2(dir.x * speed, dir.y * speed);
            if (_owner != null) _owner.WorldPosition = _rb.position;
        }
    }
}
