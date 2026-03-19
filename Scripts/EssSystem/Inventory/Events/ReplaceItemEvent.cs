using System.Collections.Generic;
using EssSystem.Core.EventManager;
using EssSystem.Inventory;

namespace Inventory.Events
{
    public class ReplaceItemEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            // if(o.Count!=4){return;}
            // string inventoryName = o[0] as string;
            // string itemName = o[1] as string;
            // string targetName = o[2] as string;
            //
            // int count= int.Parse((o[3] as string)!);
            //
            // EssSystem.Inventory.Dao.Inventory inventory= InventoryManager.Instance.GetInventory(inventoryName);
            // Item item = InventoryManager.Instance.GetItem(itemName);
            // item.Number = count;
            // Item target = InventoryManager.Instance.GetItem(targetName);
            // target.Number = count;
            //
            // bool result= inventory.RemoveItem(item);
            //
            // if (result)
            // {
            //     inventory.AddItem(target);
            // }
            // else
            // {
            //     return;
            // }
            //
            // foreach (var instanceInventoryGameObject in InventoryManager.Instance.InventoryGameObjects)
            // {
            //     if (instanceInventoryGameObject.gameObject.name.Equals(inventoryName))
            //     {
            //         instanceInventoryGameObject.LoadInventoryUI(inventory,0);
            //     }
            // }
        }

        public override void Init()
        {
            
            EventName="ReplaceItemEvent";
            base.Init();
        }


    }
}