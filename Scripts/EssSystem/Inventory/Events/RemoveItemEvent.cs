using System.Collections.Generic;
using EssSystem.Core.Dao;
using EssSystem.Core.EventManager;
using EssSystem.Inventory;

namespace Inventory.Events
{
    public class RemoveItemEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            // if(o.Count!=3){return;}
            // string inventoryName = o[0] as string;
            // string itemName = o[1] as string;
            //
            // int count= int.Parse((o[2] as string)!);
            //
            // EssSystem.Inventory.Dao.Inventory inventory= InventoryManager.Instance.GetInventory(inventoryName);
            // Item item =DataService.Instance.DeepClone( InventoryManager.Instance.GetItem(itemName));
            // item.Number = count;
            // bool result= inventory.RemoveItem(item);
            //
            // EventManager.Instance.TriggerString("PlayerAudioEvent.Interaction.拾取.1");
            // EventManager.Instance.TriggerString("AddToolTipEvent.移除物品"+item.ItemName);
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
            EventName="RemoveItemEvent";
            base.Init();
        }


    }
}
