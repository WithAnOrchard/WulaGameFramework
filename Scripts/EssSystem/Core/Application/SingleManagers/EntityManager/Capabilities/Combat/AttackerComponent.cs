using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IAttacker"/> 最小默认实现：按冷却 + 距离判定后结算一次伤害。
    /// 业务可继承覆盖 <see cref="CanAttack"/> / <see cref="Attack"/> 加入技能 / 命中率 / 多段攻击。
    /// </summary>
    public class AttackerComponent : IAttacker
    {
        public float AttackPower { get; protected set; }
        public float AttackRange { get; protected set; }
        public float AttackCooldown { get; protected set; }

        protected Entity _owner;
        protected float _lastAttackTime = -999f;

        public AttackerComponent(float attackPower, float attackRange = 1.5f, float attackCooldown = 0.6f)
        {
            AttackPower = Mathf.Max(0f, attackPower);
            AttackRange = Mathf.Max(0f, attackRange);
            AttackCooldown = Mathf.Max(0f, attackCooldown);
        }

        public virtual void OnAttach(Entity owner) { _owner = owner; }
        public virtual void OnDetach(Entity owner) { _owner = null; }

        public virtual bool CanAttack(Entity target)
        {
            if (_owner == null || target == null || target == _owner) return false;
            if (Time.time - _lastAttackTime < AttackCooldown) return false;
            if (AttackRange > 0f)
            {
                var sqr = (target.WorldPosition - _owner.WorldPosition).sqrMagnitude;
                if (sqr > AttackRange * AttackRange) return false;
            }
            return true;
        }

        public virtual bool Attack(Entity target)
        {
            if (!CanAttack(target)) return false;

            // 无敌拦截（框架级语义）
            if (target.Has<IInvulnerable>() && target.Get<IInvulnerable>().Active) return false;

            var dmg = target.Get<IDamageable>();
            if (dmg == null) return false;

            _lastAttackTime = Time.time;
            dmg.TakeDamage(AttackPower, _owner);
            return true;
        }
    }
}
