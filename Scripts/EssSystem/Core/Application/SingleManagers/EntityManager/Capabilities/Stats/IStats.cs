using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 实体属性能力 —— 给任意 <see cref="Entity"/> 装上一张可读 / 可改 / 可叠加修饰器的属性面板。
    /// <para>
    /// 与 <see cref="IAttacker"/> / <see cref="IDamageable"/> 等 Capability 同级，
    /// 通过 <c>entity.Add&lt;IStats&gt;(new StatsComponent(...))</c> 挂载。
    /// </para>
    /// <para>
    /// <b>跨模块协作</b>（设计 #2 / #4 / #5）：
    /// <list type="bullet">
    /// <item>InventoryManager 读 <see cref="DerivedStat.CarryCapacity"/> 算 MaxWeight。</item>
    /// <item>ShopManager 读 <see cref="PrimaryStat.CHA"/> 算价格折扣。</item>
    /// <item>CraftingManager 读 <see cref="PrimaryStat.INT"/> 算品质 roll。</item>
    /// <item>装备穿戴 / Buff 通过 <see cref="AddModifier"/> 临时改变属性。</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IStats : IEntityCapability
    {
        /// <summary>读基础属性（已叠加 Primary 上的 Modifier）。</summary>
        int GetPrimary(PrimaryStat stat);

        /// <summary>读派生属性（已叠加 Derived 上的 Modifier）。</summary>
        float GetDerived(DerivedStat stat);

        /// <summary>添加一条修饰器（同 SourceId 的旧条目会被先移除）。</summary>
        void AddModifier(StatModifier modifier);

        /// <summary>移除某来源的所有修饰器（卸装备 / Buff 失效）。</summary>
        void RemoveBySource(string sourceId);

        /// <summary>覆盖 Primary 基础值（升级 / 编辑器调试 / 外部存档恢复）。</summary>
        void SetPrimary(PrimaryStat stat, int value);

        /// <summary>底层数据结构（持久化 / 调试用，慎写）。</summary>
        AttributeSet Attributes { get; }
    }
}
