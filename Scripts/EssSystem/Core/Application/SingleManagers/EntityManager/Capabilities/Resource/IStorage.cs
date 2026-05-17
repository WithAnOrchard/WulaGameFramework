using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 可存储能力 —— Entity 自带一个容器（箱子 / 尸体掉落池 / 采集物品槽 / 玩家背包 …）。
    /// <para>
    /// 与 <c>InventoryManager</c> 对接：实现通常持有一个 <c>InventoryService</c> 的 <see cref="ContainerId"/>，
    /// 业务通过 <c>InventoryService.Instance.GetContainer(ContainerId)</c> 拿到实际容器数据。
    /// </para>
    /// </summary>
    public interface IStorage : IEntityCapability
    {
        /// <summary>
        /// 对应 InventoryManager 里的容器 ID。实体创建时由实现负责在 InventoryService 中注册一个容器；
        /// 实体销毁时可选择保留（掉落在地）或同步删除。
        /// </summary>
        string ContainerId { get; }

        /// <summary>容器总容量（槽位数 / 格子数）。</summary>
        int Capacity { get; }

        /// <summary>当前是否可被其他实体打开交互（例如"生者禁止开箱"场景可置 false）。</summary>
        bool CanInteract { get; }
    }
}
