using System;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 可受伤能力 —— Entity 有血量、可被攻击结算伤害。
    /// <para>
    /// 注意：<see cref="IInvulnerable"/> 同时存在时，业务的伤害结算方（如 <c>EntityService.TryDamage</c>）
    /// 应自动忽略本能力的 <see cref="TakeDamage"/> 调用。
    /// </para>
    /// </summary>
    public interface IDamageable : IEntityCapability
    {
        float CurrentHp { get; }
        float MaxHp { get; }
        bool IsDead { get; }

        /// <summary>
        /// 结算伤害；返回实际扣除的血量（考虑防御 / 抗性 / 减伤后的值）。
        /// 实现负责触发 <see cref="Damaged"/> / <see cref="Died"/>。
        /// </summary>
        float TakeDamage(float amount, Entity source = null, string damageType = null);

        /// <summary>直接治疗，返回实际回复量。</summary>
        float Heal(float amount, Entity source = null);

        /// <summary>受伤事件：(self, source, damageDealt, damageType)</summary>
        event Action<Entity, Entity, float, string> Damaged;

        /// <summary>死亡事件：(self, killer)</summary>
        event Action<Entity, Entity> Died;
    }
}
