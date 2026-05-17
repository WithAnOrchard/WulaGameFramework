using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions
{
    /// <summary>
    /// 移动到目标动作 —— 朝目标 Entity 或固定坐标直线移动。
    /// <para>
    /// 到达 <see cref="StopDistance"/> 内返回 Success；目标丢失返回 Failure。
    /// 移动方式：每帧写 <see cref="Entity.WorldPosition"/>（Dynamic 实体自动同步到 CharacterRoot）。
    /// </para>
    /// </summary>
    public class MoveToAction : IBrainAction
    {
        private readonly Entity _target;
        private readonly Vector3? _fixedDestination;
        private readonly float _stopDistance;
        private float _speed;

        /// <summary>追踪目标实体。</summary>
        public MoveToAction(Entity target, float stopDistance = 1.5f)
        {
            _target = target;
            _stopDistance = Mathf.Max(0.1f, stopDistance);
        }

        /// <summary>移动到固定坐标。</summary>
        public MoveToAction(Vector3 destination, float stopDistance = 0.3f)
        {
            _fixedDestination = destination;
            _stopDistance = Mathf.Max(0.1f, stopDistance);
        }

        public void OnEnter(BrainContext ctx)
        {
            // 从 IMovable 读取速度，否则用默认值
            var movable = ctx.Self.Get<IMovable>();
            _speed = movable?.MoveSpeed ?? 3f;
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            var dest = GetDestination();
            if (dest == null) return BrainStatus.Failure;

            var self = ctx.Self;
            var currentPos = self.WorldPosition;
            var targetPos = dest.Value;
            var diff = targetPos - currentPos;
            var dist = diff.magnitude;

            if (dist <= _stopDistance) return BrainStatus.Success;

            // 移动
            var step = _speed * deltaTime;
            if (step >= dist)
                self.WorldPosition = targetPos;
            else
                self.WorldPosition = currentPos + diff.normalized * step;

            // 写入运动状态供动画层读取
            ctx.FacingDirection = diff.x >= 0f ? 1 : -1;
            ctx.IsMoving = true;

            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            ctx.IsMoving = false;
        }

        private Vector3? GetDestination()
        {
            if (_fixedDestination.HasValue) return _fixedDestination.Value;
            if (_target == null) return null;
            return _target.CharacterRoot != null
                ? _target.CharacterRoot.position
                : _target.WorldPosition;
        }
    }
}
