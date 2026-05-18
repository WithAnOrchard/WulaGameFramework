using System;
using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
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

        /// <summary>
        /// 入伤减免（0..1）—— 由 Buff / 装备 / 状态外部写入；
        /// <see cref="TakeDamage"/> 在结算前以 <c>amount * (1 - DamageReduction)</c> 缩减。
        /// 主要用途：临时强化（如"巨大化"）、护盾、护甲百分比减伤。
        /// </summary>
        public float DamageReduction { get; set; }

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

            // 入伤减免：先按 DamageReduction 缩减，再按当前血量裁剪。
            // IInvulnerable 由上层（EntityService.TryDamage）拦截；这里做兜底 assert。
            var reduced = amount * Mathf.Clamp01(1f - DamageReduction);
            if (reduced <= 0f) return 0f;
            var dealt = Mathf.Min(CurrentHp, reduced);
            CurrentHp -= dealt;
            Damaged?.Invoke(_owner, source, dealt, damageType);

            if (IsDead) Died?.Invoke(_owner, source);
            return dealt;
        }

        /// <summary>
        /// 调整最大血量（Buff / 等级提升用）。
        /// <list type="bullet">
        /// <item><paramref name="refill"/>=true：把 CurrentHp 也置为 newMax（"巨大化"语义）。</item>
        /// <item>=false：只截断超过新上限的当前血（缩血时不爆血）。</item>
        /// </list>
        /// </summary>
        public virtual void SetMaxHp(float newMax, bool refill)
        {
            newMax = Mathf.Max(1f, newMax);
            MaxHp = newMax;
            CurrentHp = refill ? newMax : Mathf.Min(CurrentHp, newMax);
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
