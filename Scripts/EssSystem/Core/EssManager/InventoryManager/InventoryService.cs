using System.Collections.Generic;
using System.Linq;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.Dao;
using EssSystem.Core.EssManager.InventoryManager.Dao;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager
{
    public class InventoryService : ServiceBase
    {
        public static InventoryService Instance => InstanceWithInit<InventoryService>(instance =>
        {
            if (!instance.DataSpaces.ContainsKey("Item"))
                instance.DataSpaces.Add("Item", new Dictionary<string, object>());
            if (!instance.DataSpaces.ContainsKey("Inventory"))
                instance.DataSpaces.Add("Inventory", new Dictionary<string, object>());
        });


        public Dao.Inventory GetInventory(string id)
        {
            if (Instance.DataSpaces["Inventory"].ContainsKey(id))
            {
                return Instance.DataSpaces["Inventory"][id] as Dao.Inventory;
            }
            return null;
        }
        
        public Item GetItem(string id)
        {
            if (Instance.DataSpaces["Item"].ContainsKey(id))
            {
                return Instance.DataSpaces["Item"][id] as Item;
            }
            return null;
        }

        

        public bool AddItem(Dao.Inventory inventory, Item item)
        {
            item = DataService.Instance.DeepClone(item);
            var existingItem = Instance.DataSpaces["Items"].Values
                .OfType<Item>()
                .Where(i => i.Id == item.Id)
                .ToList();
            var canPutIn = 0;
            //获取现有物品能放进去的个数
            if (existingItem.Count > 0) existingItem.ForEach(i => { canPutIn += i.MaxNumber - i.Number; });

            //加上空的位置的数量
            canPutIn += (inventory.MaxStackSize - inventory.ItemList.Count) * item.MaxNumber;

            //如果能放的下
            if (canPutIn > item.Number)
            {
                //实际操作 此时应该对list上锁 避免并发
                existingItem.ForEach(i =>
                {
                    if (item.Number <= 0) return;
                    if (item.Number > i.MaxNumber - i.Number)
                    {
                        item.Number -= i.MaxNumber - i.Number;
                        i.Number = i.MaxNumber;
                    }
                    else
                    {
                        i.Number += item.Number;
                        item.Number = 0;
                    }
                });
                //把剩下的物品都新建新物品加进去

                while (item.Number > 0)
                {
                    var cloneItem = DataService.Instance.DeepClone(item);
                    cloneItem.Number = item.Number > item.MaxNumber ? cloneItem.MaxNumber : item.Number;
                    inventory.ItemList.Add(cloneItem);
                    item.Number -= item.MaxNumber;
                }

                return true;
            }

            return false;
        }

        public bool RemoveItem(Dao.Inventory inventory, Item item)
        {
            var existingItem = inventory.ItemList.FindAll(i => i.Id == item.Id);
            var canRemove = 0;
            //获取现有物品能放进去的个数
            if (existingItem.Count > 0) existingItem.ForEach(i => { canRemove += i.Number; });

            if (canRemove >= item.Number)
            {
                existingItem.ForEach(i =>
                {
                    if (item.Number >= i.Number)
                    {
                        inventory.ItemList.Remove(i);
                        item.Number -= i.Number;
                    }
                    else
                    {
                        i.Number -= item.Number;
                        item.Number = 0;
                    }
                });
                return true;
            }

            return false;
        }
    }
}