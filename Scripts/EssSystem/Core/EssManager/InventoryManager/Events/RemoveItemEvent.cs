using System.Collections.Generic;
using EssSystem.Core.Dao;
using EssSystem.Core.EssManager.InventoryManager.Dao;
using EssSystem.Core.EventManager;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager.Events
{
    public class RemoveItemEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if(o.Count!=3){return;}
            string inventoryName = o[0] as string;
            string itemName = o[1] as string;
            
            int count= int.Parse((o[2] as string)!);
            
            EssSystem.EssManager.InventoryManager.Dao.Inventory inventory= InventoryManager.Instance.GetInventory(inventoryName);
            Item item =DataService.Instance.DeepClone( InventoryManager.Instance.GetItem(itemName));
            item.Number = count;
            bool result= InventoryManager.Instance.RemoveItem(item,inventory);
        }

        public override void Init()
        {
            EventName="RemoveItemEvent";
            base.Init();
        }


    }
}
