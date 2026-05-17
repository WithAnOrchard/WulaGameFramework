using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions
{
    /// <summary>
    /// 攻击动作 —— 对目标发起一次攻击，走 <see cref="IAttacker"/> 能力。
    /// <para>
    /// 前置条件：自身有 <see cref="IAttacker"/>，目标在攻击范围内且冷却就绪。
    /// <see cref="IAttacker.CanAttack"/> 内部已含距离+冷却检查；不满足时返回 Failure。
    /// 攻击后等待冷却，然后 Success。
    /// </para>
    /// </summary>
    public class AttackAction : IBrainAction
    {
        private Entity _target;
        private IAttacker _attacker;
        private float _cooldownRemaining;
        private bool _attacked;

        /// <param name="target">攻击目标（null 时从 Context.ThreatSource 取）。</param>
        public AttackAction(Entity target = null)
        {
            _target = target;
        }

        public void OnEnter(BrainContext ctx)
        {
            _attacked = false;
            _cooldownRemaining = 0f;
            if (_target == null) _target = ctx.ThreatSource;
            _attacker = ctx.Self.Get<IAttacker>();
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            if (_attacker == null || _target == null) return BrainStatus.Failure;

            // 目标已死
            var targetDmg = _target.Get<IDamageable>();
            if (targetDmg != null && targetDmg.IsDead) return BrainStatus.Success;

            if (!_attacked)
            {
                // CanAttack 内部已含距离+冷却检查
                if (!_attacker.CanAttack(_target)) return BrainStatus.Failure;

                // 执行攻击
                var hit = _attacker.Attack(_target);
                _attacked = true;
                _cooldownRemaining = hit ? _attacker.AttackCooldown * 0.5f : 0f; // 命中时短后摇
            }

            // 等待攻击后摇
            _cooldownRemaining -= deltaTime;
            return _cooldownRemaining <= 0f ? BrainStatus.Success : BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx) { }
    }
}
