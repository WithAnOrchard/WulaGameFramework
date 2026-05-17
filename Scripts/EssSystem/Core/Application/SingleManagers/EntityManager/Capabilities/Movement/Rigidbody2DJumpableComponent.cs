using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IJumpable"/> 默认实现 —— 通过 <see cref="Rigidbody2D"/> 设置 Y 速度发起一次跳跃。
    /// <para>本组件不检查地面（那是 <see cref="IGroundSensor"/> 的职责），仅按 <see cref="JumpCooldown"/> 节流。</para>
    /// </summary>
    public class Rigidbody2DJumpableComponent : IJumpable
    {
        public float JumpForce { get; protected set; }

        /// <summary>两次跳跃之间的最短冷却（秒）；默认 0，配合 <see cref="IGroundSensor"/> 已足够防连跳。</summary>
        public float JumpCooldown { get; set; }

        public bool CanJump => _rb != null && Time.time - _lastJumpTime >= JumpCooldown;

        private readonly Rigidbody2D _rb;
        private float _lastJumpTime = -999f;
        private Entity _owner;

        public Rigidbody2DJumpableComponent(Rigidbody2D rb, float jumpForce, float jumpCooldown = 0f)
        {
            _rb = rb;
            JumpForce = Mathf.Max(0f, jumpForce);
            JumpCooldown = Mathf.Max(0f, jumpCooldown);
        }

        public void OnAttach(Entity owner) { _owner = owner; }
        public void OnDetach(Entity owner) { _owner = null; }

        public bool Jump()
        {
            if (!CanJump) return false;
            _rb.velocity = new Vector2(_rb.velocity.x, JumpForce);
            _lastJumpTime = Time.time;
            return true;
        }
    }
}
