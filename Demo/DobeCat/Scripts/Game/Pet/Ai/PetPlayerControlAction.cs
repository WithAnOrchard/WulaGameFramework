using System;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet.Ai
{
    /// <summary>
    /// 玩家手动操控 AI 行为 —— 把全局 WASD 轴位映射成移动指令。
    /// <list type="bullet">
    /// <item>每帧从 <see cref="ReadAxis"/> 拉一次 axis。</item>
    /// <item>axis 非零 → 接管移动、写 IsMoving/FacingDirection，返回 <c>Running</c>。</item>
    /// <item>axis 归零 → 返回 <c>Success</c>，Brain 自动重评估，wander 等低优先级行为接管。</item>
    /// </list>
    /// <para>抢占机制走 Brain 的 Consideration Score：只要 axis 非零就给高分，自然压过 wander。</para>
    /// </summary>
    public class PetPlayerControlAction : IBrainAction
    {
        private readonly Func<Vector2> _readAxis;
        private float _speed;

        public PetPlayerControlAction(Func<Vector2> readAxis)
        {
            _readAxis = readAxis ?? (() => Vector2.zero);
        }

        public void OnEnter(BrainContext ctx)
        {
            var mv = ctx.Self.Get<IMovable>();
            _speed = mv?.MoveSpeed ?? 4f;
        }

        public BrainStatus Tick(BrainContext ctx, float dt)
        {
            var axis = _readAxis();
            if (axis.sqrMagnitude < 1e-3f)
            {
                ctx.IsMoving = false;
                return BrainStatus.Success;
            }
            if (axis.sqrMagnitude > 1f) axis.Normalize();

            BrainMoveHelper.ApplyMove(ctx.Self, axis, _speed, dt);
            ctx.IsMoving = true;
            if (Mathf.Abs(axis.x) > 0.05f) ctx.FacingDirection = axis.x > 0f ? 1 : -1;
            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            BrainMoveHelper.ApplyMove(ctx.Self, Vector2.zero, 0f, 0f);
            ctx.IsMoving = false;
        }
    }
}
