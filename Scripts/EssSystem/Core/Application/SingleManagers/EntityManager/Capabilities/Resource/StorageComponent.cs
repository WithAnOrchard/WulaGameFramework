using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IStorage"/> 最简实现：持有一个 InventoryManager 容器 ID。
    /// <para>
    /// 本组件不负责创建 / 销毁 InventoryContainer —— 由业务在挂载前后自行调用
    /// <c>InventoryService.Instance.CreateContainer(containerId, capacity)</c> 来管理容器生命周期。
    /// 这样可以保持 EntityManager 与 InventoryManager 的弱耦合。
    /// </para>
    /// </summary>
    public class StorageComponent : IStorage
    {
        public string ContainerId { get; protected set; }
        public int Capacity { get; protected set; }
        public bool CanInteract { get; set; }

        public StorageComponent(string containerId, int capacity, bool canInteract = true)
        {
            ContainerId = containerId;
            Capacity = capacity;
            CanInteract = canInteract;
        }

        public void OnAttach(Entity owner) { }
        public void OnDetach(Entity owner) { }
    }
}
