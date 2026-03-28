using System.Collections.Generic;
using EssSystem.Core.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager.Dao
{
    public class Inventory
    {
        public List<Item> ItemList = new();

        public int MaxStackSize { get; set; }
    }
}