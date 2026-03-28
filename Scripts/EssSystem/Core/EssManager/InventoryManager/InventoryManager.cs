using System;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.EssManager.InventoryManager.Dao;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager
{
    public class InventoryManager : ManagerBase
    {
        private bool _hasInit;

        public static InventoryManager Instance => InstanceWithInit<InventoryManager>(instance => { instance.Init(true); });

        public void Init(bool logMessage)
        {
            if (_hasInit) return;
            _hasInit = true;
        }

        public Dao.Inventory GetInventory(string inventoryName)
        {
            return InventoryService.Instance.GetInventory(inventoryName);
        }
        
        public Item GetItem(string itemId)
        {
            return InventoryService.Instance.GetItem(itemId);
        }

        public Boolean RemoveItem(Item item,Dao.Inventory inventory)
        {
            return InventoryService.Instance.RemoveItem(inventory, item);
        }

        public Boolean AddItem(Item item,Dao.Inventory inventory)
        {
            return InventoryService.Instance.AddItem(inventory, item);
        }
    }
}