using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    public class MovableComponent : IMovable
    {
        public float MoveSpeed { get; protected set; }
        public Vector3 Velocity { get; set; }

        /// <summary>速度倍率（Buff 用，&lt;1 减速 / &gt;1 加速；缺省 1）。</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        private Entity _owner;

        public MovableComponent(float moveSpeed)
        {
            MoveSpeed = Mathf.Max(0f, moveSpeed);
        }

        public void OnAttach(Entity owner)
        {
            _owner = owner;
        }

        public void OnDetach(Entity owner)
        {
            _owner = null;
            Velocity = Vector3.zero;
        }

        public void Move(Vector3 direction, float deltaTime)
        {
            if (_owner == null || MoveSpeed <= 0f) return;
            // Stun 短路：当前帧无法移动（速度清零，避免靠惯性继续前进）
            var ctrl = _owner.Get<IControllable>();
            if (ctrl != null && ctrl.Stunned) { Velocity = Vector3.zero; return; }
            var speed = MoveSpeed * Mathf.Max(0f, SpeedMultiplier);
            Velocity = direction.sqrMagnitude > 1f ? direction.normalized * speed : direction * speed;
            _owner.WorldPosition += Velocity * Mathf.Max(0f, deltaTime);
        }
    }
}
