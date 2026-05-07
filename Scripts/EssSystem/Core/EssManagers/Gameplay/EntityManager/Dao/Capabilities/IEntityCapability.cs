namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// Entity 能力（Capability）基接口 —— 所有可挂到 <see cref="Entity"/> 上的功能契约都派生于此。
    /// <para>
    /// <b>约定</b>：每种"能力"用一个<b>独立接口</b>（<c>IDamageable</c> / <c>IAttacker</c> …），
    /// <see cref="Entity"/> 以"接口类型"作字典键注册，便于 <c>Has&lt;IDamageable&gt;() / Get&lt;IDamageable&gt;()</c> 查询。
    /// </para>
    /// <para>
    /// <b>生命周期</b>：能力实例在 <c>EntityService.CreateEntity</c> 之后由业务代码 / 配置驱动挂载；
    /// Entity 销毁时可选调用 <see cref="OnDetach"/>（默认 Service 会按字典枚举调用一次）。
    /// </para>
    /// </summary>
    public interface IEntityCapability
    {
        /// <summary>挂到实体上时回调（由 <c>Entity.Add</c> 自动触发）。</summary>
        void OnAttach(Entity owner);

        /// <summary>从实体卸载 / 实体销毁时回调。</summary>
        void OnDetach(Entity owner);
    }
}
