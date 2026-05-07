using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IDamageable"/> 的最小默认实现：线性扣血、事件广播、无防御计算。
    /// 业务可直接继承覆盖 <see cref="TakeDamage"/> 加入护甲 / 元素抗性 / 暴击倍率等规则。
    /// </summary>
    public class DamageableComponent : IDamageable
    {
        public float CurrentHp { get; protected set; }
        public float MaxHp { get; protected set; }
        public bool IsDead => CurrentHp <= 0f;

        public event Action<Entity, Entity, float, string> Damaged;
        public event Action<Entity, Entity> Died;

        protected Entity _owner;

        public DamageableComponent(float maxHp)
        {
            MaxHp = Mathf.Max(1f, maxHp);
            CurrentHp = MaxHp;
        }

        public virtual void OnAttach(Entity owner) { _owner = owner; }
        public virtual void OnDetach(Entity owner) { _owner = null; }

        public virtual float TakeDamage(float amount, Entity source = null, string damageType = null)
        {
            if (IsDead || amount <= 0f) return 0f;

            // IInvulnerable 由上层（EntityService.TryDamage）拦截；这里做兜底 assert。
            var dealt = Mathf.Min(CurrentHp, amount);
            CurrentHp -= dealt;
            Damaged?.Invoke(_owner, source, dealt, damageType);

            if (IsDead) Died?.Invoke(_owner, source);
            return dealt;
        }

        public virtual float Heal(float amount, Entity source = null)
        {
            if (IsDead || amount <= 0f) return 0f;
            var healed = Mathf.Min(MaxHp - CurrentHp, amount);
            CurrentHp += healed;
            return healed;
        }
    }
}
