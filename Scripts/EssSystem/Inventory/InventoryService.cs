using EssSystem.Core.AbstractClass;
using EssSystem.Core.Dao;
using Unity.VisualScripting;

namespace EssSystem.Inventory
{
    public class InventoryService : ServiceBase
    {
        public static InventoryService Instance => InstanceWithInit<InventoryService>(instance =>
        {
            // DataService.Instance.DataSpaces["InventoryService"].Add("");
        });

        //创建容器
        public Dao.Inventory CreateInventory(string inventoryName, int maxStack,int x,int y)
        {
            Dao.Inventory inventory = new Dao.Inventory(inventoryName, maxStack,x,y);
            // Inventorys.Add(inventory);
            return inventory;
        }

        public void OpenOrInventoryUI(string inventoryName)
        {
            // foreach (var inventoryGameObject in InventoryGameObjects)
            // {
            //     if (inventoryGameObject.name.Equals(inventoryName))
            //     {
            //         if (!inventoryGameObject.gameObject.activeSelf)
            //         {
            //             inventoryGameObject.gameObject.SetActive(true);
            //             inventoryGameObject.DescriptionUIGameObject.text.text = "";
            //             inventoryGameObject.DescriptionUIGameObject.itemName.text = "";
            //         }
            //         else
            //         { 
            //             inventoryGameObject.gameObject.SetActive(false);
            //             
            //         }
            //     }
            // }
        }

        public Dao.Inventory GetInventory(string inventoryName)
        {
            // foreach (var inventory in Inventorys)
            // {
            //     if (inventoryName.Equals(inventory.InventoryName))
            //     {
            //         return inventory;
            //     }
            // }
            return null;
        }

        public Item GetItem(string itemName)
        {
            // foreach (var item in Items)
            // {
            //     if (item.ItemName .Equals(itemName))
            //     {
            //         return item;
            //     }
            // }
            return null;
        }
    }
}