using System;
using System.Collections.Generic;
using EssSystem.Core.Dao;

namespace EssSystem.Inventory.Dao
{
    [System.Serializable]
    public class Inventory
    {
        public string InventoryName;
        public int MaxStack;
        public int x;
        public int y;
        public List<Item> Items = new List<Item>();

        public Inventory(string inventoryName, int maxStack,int x ,int y)
        {
            this.InventoryName = inventoryName;
            this.MaxStack = maxStack;
            this.x = x;
            this.y = y;
        }

        public Inventory()
        {
        }

        public Boolean AddItem(Item item)
        {
            item=DataService.Instance.DeepClone(item);
            List<Item> existingItem = Items.FindAll(i => i.ItemName == item.ItemName);
            int canPutIn = 0;
            //获取现有物品能放进去的个数
            if (existingItem.Count > 0)
            {
                existingItem.ForEach(i => { canPutIn += i.MaxNumber - i.Number; });
            }

            //加上空的位置的数量
            canPutIn += (MaxStack - Items.Count) * item.MaxNumber;

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
                    Item cloneItem = DataService.Instance.DeepClone(item);
                    cloneItem.Number = item.Number > item.MaxNumber ? cloneItem.MaxNumber : item.Number;
                    Items.Add(cloneItem);
                    item.Number -= item.MaxNumber;
                }

                return true;
            }

            return false;
        }

        public Boolean RemoveItem(Item item)
        {
            List<Item> existingItem = Items.FindAll(i => i.ItemName == item.ItemName);
            int canRemove = 0;
            //获取现有物品能放进去的个数
            if (existingItem.Count > 0)
            {
                existingItem.ForEach(i => { canRemove += i.Number; });
            }

            if (canRemove >= item.Number)
            {
                existingItem.ForEach(i =>
                {
                    if (item.Number >= i.Number)
                    {
                        Items.Remove(i);
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