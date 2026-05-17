using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 可发动攻击能力 —— Entity 能对目标造成伤害。
    /// <para>
    /// 典型实现读取自身攻击力 / 武器加成 / 暴击等修正后，调用目标 <see cref="IDamageable.TakeDamage"/>。
    /// </para>
    /// </summary>
    public interface IAttacker : IEntityCapability
    {
        /// <summary>基础攻击力（最终伤害由实现按自身规则修正）。</summary>
        float AttackPower { get; }

        /// <summary>攻击范围（世界单位；0 = 近战贴身）。</summary>
        float AttackRange { get; }

        /// <summary>两次攻击之间的最短冷却（秒）。</summary>
        float AttackCooldown { get; }

        /// <summary>是否可在本帧发动攻击（距离、冷却、状态 …）。</summary>
        bool CanAttack(Entity target);

        /// <summary>
        /// 对 <paramref name="target"/> 发动一次攻击；返回是否命中并结算。
        /// 实现内部负责查 target 是否 <see cref="IDamageable"/>、是否 <see cref="IInvulnerable"/>，并调用 <c>TakeDamage</c>。
        /// </summary>
        bool Attack(Entity target);
    }
}
