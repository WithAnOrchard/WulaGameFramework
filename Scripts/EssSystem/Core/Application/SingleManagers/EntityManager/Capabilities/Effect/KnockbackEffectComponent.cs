using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 击退效果组件 —— 受伤时物理击退。
    /// <para>
    /// 采用 <b>逻辑位移</b>（直接写 <c>CharacterRoot.position</c>）而非 <c>velocity</c>——
    /// 原因：若 Entity 同时拥有逻辑模式巡逻（<see cref="HorizontalPatrolComponent"/> 无 RB），
    /// 巡逻每帧直写 position 会完全覆盖 velocity 产生的位移。
    /// </para>
    /// <para>击退期间自动暂停 <see cref="IPatrol"/>，结束后恢复。</para>
    /// </summary>
    public class KnockbackEffectComponent : IKnockbackEffect, ITickableCapability
    {
        private readonly float _knockbackForce;
        private readonly float _knockbackDuration;

        private Entity _owner;
        private Transform _root;
        private Vector3 _knockbackDir;
        private float _knockbackTimer;
        private bool _wasPatrolPaused;

        public KnockbackEffectComponent(Rigidbody2D rb, float knockbackForce = 5f, float knockbackDuration = 0.2f)
        {
            _knockbackForce = knockbackForce;
            _knockbackDuration = knockbackDuration;
        }

        public void OnAttach(Entity owner)
        {
            _owner = owner;
            _root = owner.CharacterRoot;
        }

        public void OnDetach(Entity owner)
        {
            // 确保巡逻恢复
            if (_knockbackTimer > 0f) ResumePatrol();
            _owner = null;
            _root = null;
        }

        public void OnKnockback(Vector3 damageSource)
        {
            if (_root == null) return;

            var direction = (_root.position - damageSource).normalized;
            direction.y = 0f; // 只在 X 轴击退
            if (direction.sqrMagnitude < 0.01f)
                direction.x = _root.localScale.x > 0 ? 1f : -1f;

            _knockbackDir = direction;
            _knockbackTimer = _knockbackDuration;

            // 暂停巡逻，避免巡逻每帧覆盖位移
            PausePatrol();
        }

        public void Tick(float deltaTime)
        {
            if (_knockbackTimer <= 0f) return;

            // 逻辑位移：直接写 position
            if (_root != null)
            {
                var displacement = _knockbackDir * (_knockbackForce * deltaTime);
                _root.position += displacement;
                if (_owner != null) _owner.WorldPosition = _root.position;
            }

            _knockbackTimer -= deltaTime;
            if (_knockbackTimer <= 0f)
                ResumePatrol();
        }

        private void PausePatrol()
        {
            var patrol = _owner?.Get<IPatrol>();
            if (patrol == null) return;
            _wasPatrolPaused = patrol.Paused;
            patrol.Paused = true;
        }

        private void ResumePatrol()
        {
            var patrol = _owner?.Get<IPatrol>();
            if (patrol != null) patrol.Paused = _wasPatrolPaused;
        }
    }
}
