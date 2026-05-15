namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 接触伤害能力 —— 持续对范围内的其他 Entity（具备 <see cref="IDamageable"/>）造成伤害。
    /// 典型用例：铁丝网 / 火焰陷阱 / 荆棘地。
    /// <para>实现一般是 <see cref="ITickableCapability"/>，按 <see cref="TickInterval"/> 节奏扫描 <see cref="Radius"/>。</para>
    /// </summary>
    public interface IContactDamage : IEntityCapability
    {
        /// <summary>每次结算造成的伤害值。</summary>
        float DamagePerTick { get; }

        /// <summary>结算间隔（秒）。</summary>
        float TickInterval { get; }

        /// <summary>扫描半径（世界单位）。0 表示只命中重叠的碰撞体。</summary>
        float Radius { get; }

        /// <summary>伤害标签（如 <c>"BarbedWire"</c>），透传给 <see cref="IDamageable.TakeDamage"/>。</summary>
        string DamageType { get; }
    }
}
