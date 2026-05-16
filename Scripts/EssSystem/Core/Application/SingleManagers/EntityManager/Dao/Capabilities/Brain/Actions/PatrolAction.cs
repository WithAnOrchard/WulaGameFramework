using System;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Brain.Actions
{
    /// <summary>
    /// 巡逻动作 —— 在初始位置附近水平往返移动。
    /// <para>
    /// 取代独立的 <see cref="IPatrol"/> 能力，由 Brain 统一调度。
    /// 无限循环（永远返回 Running），直到被更高优先级行为抢占。
    /// </para>
    /// </summary>
    public class PatrolAction : IBrainAction
    {
        private readonly float _speed;
        private readonly float _distance;
        private float _originX;
        private int _direction = 1;

        /// <summary>方向切换时回调（可选，用于翻转动画）。</summary>
        public event Action<int> DirectionChanged;

        public int Direction => _direction;

        /// <param name="speed">移动速度（单位/秒）。</param>
        /// <param name="distance">从起始点的最大偏移距离。</param>
        public PatrolAction(float speed, float distance)
        {
            _speed = Mathf.Max(0f, speed);
            _distance = Mathf.Max(0f, distance);
        }

        public void OnEnter(BrainContext ctx)
        {
            _originX = ctx.Self.CharacterRoot != null
                ? ctx.Self.CharacterRoot.position.x
                : ctx.Self.WorldPosition.x;
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            if (_speed <= 0f || _distance <= 0f) return BrainStatus.Running;

            // 边界检测：超出巡逻范围时，强制设方向朝向原点（不是简单翻转）
            // —— 防止击退后卡在边界外反复翻转抖动
            var curX = ctx.Self.WorldPosition.x;
            var offset = curX - _originX;
            if (Mathf.Abs(offset) >= _distance)
            {
                var newDir = offset > 0f ? -1 : 1; // 朝原点方向走
                if (newDir != _direction)
                {
                    _direction = newDir;
                    DirectionChanged?.Invoke(_direction);
                }
            }

            // 移动
            var pos = ctx.Self.WorldPosition;
            pos.x += _direction * _speed * deltaTime;
            ctx.Self.WorldPosition = pos;

            // 写入运动状态供动画层读取
            ctx.FacingDirection = _direction;
            ctx.IsMoving = true;
            ctx.IsRunning = false;

            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            ctx.IsMoving = false;
        }
    }
}
