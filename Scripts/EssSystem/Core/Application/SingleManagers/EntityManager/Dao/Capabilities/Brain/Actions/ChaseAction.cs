using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Brain.Actions
{
    /// <summary>
    /// 追击动作 —— 朝目标移动（仅 X 轴），进入攻击范围后自动攻击。
    /// <para>
    /// 追到攻击范围内则尝试攻击，攻击后短暂后摇继续追。
    /// 目标死亡、目标丢失、超出追击距离、或超时则 Success。
    /// </para>
    /// </summary>
    public class ChaseAction : IBrainAction
    {
        private readonly float _giveUpDistance;
        private readonly float _maxDuration;
        private readonly float _speedMultiplier;

        private Entity _target;
        private float _speed;
        private float _elapsed;
        private float _attackCooldown;
        private IAttacker _attacker;

        /// <param name="target">追击目标（null 时从 Context.ThreatSource 取）。</param>
        /// <param name="giveUpDistance">超过此距离放弃追击。</param>
        /// <param name="maxDuration">最大追击时长（秒）。0 = 无限。</param>
        /// <param name="speedMultiplier">追击速度倍率（相对 IMovable.MoveSpeed）。</param>
        public ChaseAction(Entity target = null, float giveUpDistance = 12f,
            float maxDuration = 8f, float speedMultiplier = 1.8f)
        {
            _target = target;
            _giveUpDistance = Mathf.Max(1f, giveUpDistance);
            _maxDuration = maxDuration;
            _speedMultiplier = Mathf.Max(1f, speedMultiplier);
        }

        public void OnEnter(BrainContext ctx)
        {
            _elapsed = 0f;
            _attackCooldown = 0f;
            if (_target == null) _target = ctx.ThreatSource;
            _attacker = ctx.Self.Get<IAttacker>();
            var movable = ctx.Self.Get<IMovable>();
            _speed = (movable?.MoveSpeed ?? 3f) * _speedMultiplier;
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            _elapsed += deltaTime;
            if (_maxDuration > 0f && _elapsed >= _maxDuration) return BrainStatus.Success;

            // 目标丢失或已死
            if (_target == null) return BrainStatus.Success;
            var targetDmg = _target.Get<IDamageable>();
            if (targetDmg != null && targetDmg.IsDead) return BrainStatus.Success;

            // 获取目标位置
            var threatPos = _target.CharacterRoot != null
                ? _target.CharacterRoot.position
                : _target.WorldPosition;
            var selfPos = ctx.Self.WorldPosition;

            var diffX = threatPos.x - selfPos.x;
            var distX = Mathf.Abs(diffX);

            // 超出追击距离，放弃
            if (distX > _giveUpDistance) return BrainStatus.Success;

            // 攻击冷却
            if (_attackCooldown > 0f) _attackCooldown -= deltaTime;

            // 在攻击范围内则尝试攻击
            if (_attacker != null && _attackCooldown <= 0f && _attacker.CanAttack(_target))
            {
                _attacker.Attack(_target);
                _attackCooldown = _attacker.AttackCooldown;
            }

            // 朝目标移动（仅 X 轴）
            var attackRange = _attacker?.AttackRange ?? 1.5f;
            if (distX > attackRange * 0.8f)
            {
                var dirX = diffX > 0f ? 1f : -1f;
                var pos = selfPos;
                pos.x += dirX * _speed * deltaTime;
                ctx.Self.WorldPosition = pos;

                ctx.FacingDirection = dirX > 0f ? 1 : -1;
                ctx.IsMoving = true;
                ctx.IsRunning = true;
            }
            else
            {
                // 在攻击范围内，面朝目标但不移动
                ctx.FacingDirection = diffX >= 0f ? 1 : -1;
                ctx.IsMoving = false;
                ctx.IsRunning = false;
            }

            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            ctx.IsMoving = false;
            ctx.IsRunning = false;
        }
    }
}
