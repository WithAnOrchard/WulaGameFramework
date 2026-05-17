namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 周期采集能力 —— 定时往某容器丢一份物品。
    /// 典型用例：自动农场、矿石钻头、风车 → 木材。
    /// <para>实现一般是 <see cref="ITickableCapability"/>，每 <see cref="Interval"/> 秒触发一次
    /// <c>InventoryService.EVT_ADD</c>（bare-string）。</para>
    /// </summary>
    public interface IHarvester : IEntityCapability
    {
        /// <summary>产出物品 id（来自 <c>InventoryManager</c> 的物品模板表）。</summary>
        string ItemId { get; }

        /// <summary>每次产出的数量。</summary>
        int Amount { get; }

        /// <summary>产出间隔（秒）。</summary>
        float Interval { get; }

        /// <summary>目标容器 id（一般是 <c>"player"</c>）。</summary>
        string TargetInventoryId { get; }
    }
}
