using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet.Ai
{
    /// <summary>
    /// 桌宠随机游荡 AI 行为。状态循环：
    /// <list type="bullet">
    /// <item>Idle：随机停留 <see cref="IdleMin"/>~<see cref="IdleMax"/> 秒。</item>
    /// <item>Walk：在主相机视口内随机挑一点，朝它走（用 MoveToAction 内部逻辑）。</item>
    /// </list>
    /// <para>整个 Action 永远返回 <c>Running</c>，由 Brain 的 Consideration 评分系统决定是否被高优先级行为（如玩家操作）抢占。</para>
    /// </summary>
    public class PetWanderAction : IBrainAction
    {
        public float IdleMin = 1.5f;
        public float IdleMax = 4f;
        public float ArriveDistance = 0.1f;

        private enum Phase { Idle, Walk }
        private Phase _phase;
        private float _idleTimer;
        private Vector3 _target;
        private float _speed;

        public PetWanderAction(float idleMin = 1.5f, float idleMax = 4f)
        {
            IdleMin = idleMin;
            IdleMax = idleMax;
        }

        public void OnEnter(BrainContext ctx)
        {
            var mv = ctx.Self.Get<IMovable>();
            _speed = mv?.MoveSpeed ?? 1.5f;
            EnterIdle(ctx);
        }

        public BrainStatus Tick(BrainContext ctx, float dt)
        {
            if (_phase == Phase.Idle)
            {
                ctx.IsMoving = false;
                _idleTimer -= dt;
                if (_idleTimer <= 0f) EnterWalk(ctx);
                return BrainStatus.Running;
            }

            // Walk
            var pos = ctx.Self.WorldPosition;
            var diff = _target - pos;
            diff.z = 0f;
            var dist = diff.magnitude;
            if (dist <= ArriveDistance)
            {
                EnterIdle(ctx);
                return BrainStatus.Running;
            }

            var dirN = (Vector2)(diff / dist);
            BrainMoveHelper.ApplyMove(ctx.Self, dirN, _speed, dt);
            ctx.IsMoving = true;
            if (Mathf.Abs(diff.x) > 0.05f) ctx.FacingDirection = diff.x > 0f ? 1 : -1;
            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            BrainMoveHelper.ApplyMove(ctx.Self, Vector2.zero, 0f, 0f);
            ctx.IsMoving = false;
        }

        private void EnterIdle(BrainContext ctx)
        {
            _phase = Phase.Idle;
            _idleTimer = Random.Range(IdleMin, IdleMax);
            ctx.IsMoving = false;
        }

        private void EnterWalk(BrainContext ctx)
        {
            _phase = Phase.Walk;
            _target = PickRandomScreenTarget(ctx.Self.WorldPosition);
        }

        /// <summary>在主相机视口内挑一个随机点（保留 10% 边距）。无相机时退化为当前点附近。</summary>
        private static Vector3 PickRandomScreenTarget(Vector3 fallback)
        {
            var cam = Camera.main;
            if (cam == null)
                return fallback + new Vector3(Random.Range(-3f, 3f), Random.Range(-1f, 1f), 0f);
            var z = Mathf.Abs(cam.transform.position.z);
            var vp = new Vector3(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), z);
            var world = cam.ViewportToWorldPoint(vp);
            world.z = 0f;
            return world;
        }
    }
}
