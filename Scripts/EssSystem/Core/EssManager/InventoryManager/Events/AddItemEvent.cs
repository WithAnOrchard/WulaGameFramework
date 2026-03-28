using System.Collections.Generic;
using EssSystem.Core.EssManager.InventoryManager.Dao;
using EssSystem.Core.EventManager;
using EssSystem.EssManager.InventoryManager.Dao;
using UnityEngine;

namespace EssSystem.EssManager.InventoryManager.Events
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

            bool hasAdd= InventoryManager.Instance.AddItem(item,inventory);
            
        }

        public override void Init()
        {
            
            EventName="AddItemEvent";
            base.Init();
        }
    }
}
