using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 光环能力 —— 持续对范围内的 Entity 提供治疗 / 增益。
    /// 典型用例：治疗塔、buff 图腾。
    /// <para>实现一般是 <see cref="ITickableCapability"/>，按 <see cref="TickInterval"/> 节奏扫描 <see cref="Radius"/>。</para>
    /// <para>负值 <see cref="HealPerTick"/> 表示对范围内的目标造成伤害（毒气云等反向用法）。</para>
    /// </summary>
    public interface IAura : IEntityCapability
    {
        /// <summary>每次结算回复的血量（负值则造成伤害）。</summary>
        float HealPerTick { get; }

        /// <summary>结算间隔（秒）。</summary>
        float TickInterval { get; }

        /// <summary>光环半径（世界单位）。</summary>
        float Radius { get; }
    }
}
