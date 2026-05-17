using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions
{
    /// <summary>
    /// 逃离动作 —— 沿远离威胁源方向移动（横版模式仅 X 轴）。
    /// <para>
    /// 逃到 <see cref="_safeDistance"/> 外返回 Success；威胁消失也返回 Success。
    /// 可选逃跑时长上限（避免永远逃）。
    /// </para>
    /// </summary>
    public class FleeAction : IBrainAction
    {
        private readonly float _safeDistance;
        private readonly float _maxDuration;
        private readonly float _speedMultiplier;
        private float _speed;
        private float _elapsed;
        private Entity _threat;

        /// <param name="threat">逃离的目标（可为 null，此时从 Context.ThreatSource 取）。</param>
        /// <param name="safeDistance">逃到此距离外视为安全。</param>
        /// <param name="maxDuration">最大逃跑时长（秒）。0 = 无限。</param>
        /// <param name="speedMultiplier">逃跑速度倍率（相对 IMovable.MoveSpeed）。</param>
        public FleeAction(Entity threat = null, float safeDistance = 8f, float maxDuration = 5f, float speedMultiplier = 2.5f)
        {
            _threat = threat;
            _safeDistance = Mathf.Max(1f, safeDistance);
            _maxDuration = maxDuration;
            _speedMultiplier = Mathf.Max(1f, speedMultiplier);
        }

        public void OnEnter(BrainContext ctx)
        {
            _elapsed = 0f;
            if (_threat == null) _threat = ctx.ThreatSource;
            var movable = ctx.Self.Get<IMovable>();
            _speed = (movable?.MoveSpeed ?? 3f) * _speedMultiplier;
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            _elapsed += deltaTime;
            if (_maxDuration > 0f && _elapsed >= _maxDuration) return BrainStatus.Success;

            // 威胁丢失 → 安全
            if (_threat == null || (_threat.CharacterRoot == null && _threat.WorldPosition == Vector3.zero))
                return BrainStatus.Success;

            var selfPos = ctx.Self.WorldPosition;
            var threatPos = _threat.CharacterRoot != null
                ? _threat.CharacterRoot.position
                : _threat.WorldPosition;

            // 横版模式：仅使用 X 轴差异计算距离和方向，避免 Y 分量（重力轴）干扰
            var diffX = selfPos.x - threatPos.x;
            var distX = Mathf.Abs(diffX);

            // 已到安全距离
            if (distX >= _safeDistance) return BrainStatus.Success;

            // 远离方向移动（仅 X 轴）
            var dirX = diffX >= 0f ? 1f : -1f;
            var pos = selfPos;
            pos.x += dirX * _speed * deltaTime;
            ctx.Self.WorldPosition = pos;

            // 写入运动状态供动画层读取
            ctx.FacingDirection = dirX >= 0f ? 1 : -1;
            ctx.IsMoving = true;
            ctx.IsRunning = true;

            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            ctx.IsMoving = false;
            ctx.IsRunning = false;
        }
    }
}
