using System;
using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IPatrol"/> 横向往返实现 —— 围绕注册时的初始位置在 <see cref="PatrolDistance"/> 半径内来回移动。
    /// <para>
    /// 移动方式两种，按构造时是否传入 <see cref="Rigidbody2D"/> 选择：
    /// <list type="bullet">
    /// <item>有 RB（物理）：每帧设 <c>rb.velocity.x = direction * speed</c>，Y 由重力 / 跳跃保留。</item>
    /// <item>无 RB（逻辑）：每帧直接平移 <c>CharacterRoot.position</c> 或 <c>WorldPosition</c>。</item>
    /// </list>
    /// 边界由本能力维护，不依赖 collider；越界后翻转方向并广播 <see cref="DirectionChanged"/>。
    /// </para>
    /// </summary>
    public class HorizontalPatrolComponent : IPatrol
    {
        public float MoveSpeed { get; }
        public float PatrolDistance { get; }
        public int Direction { get; private set; } = 1;
        public bool Paused { get; set; }
        public bool IsMoving => !Paused && MoveSpeed > 0f;
        public event Action<int> DirectionChanged;

        private readonly Rigidbody2D _rb;
        private Entity _owner;
        private float _originX;

        public HorizontalPatrolComponent(float moveSpeed, float patrolDistance, Rigidbody2D rb = null)
        {
            MoveSpeed = Mathf.Max(0f, moveSpeed);
            PatrolDistance = Mathf.Max(0f, patrolDistance);
            _rb = rb;
        }

        public void OnAttach(Entity owner)
        {
            _owner = owner;
            _originX = owner.CharacterRoot != null ? owner.CharacterRoot.position.x : owner.WorldPosition.x;
        }

        public void OnDetach(Entity owner)
        {
            _owner = null;
            if (_rb != null) _rb.velocity = new Vector2(0f, _rb.velocity.y);
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null || Paused || MoveSpeed <= 0f) return;

            // 1) 推进位置（物理 vs 逻辑）
            if (_rb != null)
            {
                _rb.velocity = new Vector2(Direction * MoveSpeed, _rb.velocity.y);
            }
            else if (_owner.CharacterRoot != null)
            {
                var p = _owner.CharacterRoot.position;
                _owner.CharacterRoot.position = new Vector3(p.x + Direction * MoveSpeed * deltaTime, p.y, p.z);
                _owner.WorldPosition = _owner.CharacterRoot.position;
            }
            else
            {
                var p = _owner.WorldPosition;
                p.x += Direction * MoveSpeed * deltaTime;
                _owner.WorldPosition = p;
            }

            // 2) 边界检查
            if (PatrolDistance <= 0f) return;
            var currentX = _owner.CharacterRoot != null ? _owner.CharacterRoot.position.x : _owner.WorldPosition.x;
            if (Mathf.Abs(currentX - _originX) > PatrolDistance)
            {
                Direction = -Direction;
                DirectionChanged?.Invoke(Direction);
            }
        }
    }
}
