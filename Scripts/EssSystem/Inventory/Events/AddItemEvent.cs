using System.Collections.Generic;
using EssSystem.Core.EventManager;
using EssSystem.Inventory.InventoryUI;
using UnityEngine;

namespace EssSystem.Inventory.Events
{
    public class AddItemEvent : TriggerEvent
    {

        public override void Action(List<object> o)
        {
            if(o.Count!=3){return;}
            string inventoryName = o[0] as string;
            string itemName = o[1] as string;
            
            int count= int.Parse((o[2] as string)!);
            
            Dao.Inventory inventory= InventoryManager.Instance.GetInventory(inventoryName);
           
            Item item =  JsonUtility.FromJson<Item>(JsonUtility.ToJson(InventoryManager.Instance.GetItem(itemName))); 
            item.Number = count;

            // inventory.RemoveItem(item);

            foreach (var i in inventory.Items)
            {
                if(i.ItemName.Equals(item.ItemName)){return;}
            }
            bool hasAdd= inventory.AddItem(item);
            
          
            

            if (hasAdd)
            {
                EventManager.Instance.TriggerString("PlayerAudioEvent.Interaction.拾取.1");
            
                EventManager.Instance.TriggerString("AddToolTipEvent.拾取物品"+item.ItemName);
            }
            
           
            foreach (var instanceInventoryGameObject in InventoryManager.Instance.InventoryGameObjects)
            {
                if (instanceInventoryGameObject.gameObject.name.Equals(inventoryName))
                {
                    instanceInventoryGameObject.LoadInventoryUI(inventory,0);
                }
            }
        }

        public override void Init()
        {
            
            EventName="AddItemEvent";
            base.Init();
        }
    }
}
