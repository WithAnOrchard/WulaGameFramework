using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default
{
    public class MovableComponent : IMovable
    {
        public float MoveSpeed { get; protected set; }
        public Vector3 Velocity { get; set; }

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
            Velocity = direction.sqrMagnitude > 1f ? direction.normalized * MoveSpeed : direction * MoveSpeed;
            _owner.WorldPosition += Velocity * Mathf.Max(0f, deltaTime);
        }
    }
}
