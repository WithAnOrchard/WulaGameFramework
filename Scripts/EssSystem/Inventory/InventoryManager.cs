using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.Dao;
using EssSystem.Core.Singleton;
using EssSystem.Inventory.InventoryUI;
using Inventory.GameObject;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Inventory
{
    public class InventoryManager : SingletonMono<InventoryManager>
    {
        //所有容器的数据
        public List<Dao.Inventory> Inventorys = new List<Dao.Inventory>();

        [SerializeField]
        public List<InventoryGameObject> InventoryGameObjects = new List<InventoryGameObject>();

        //所有的UI类型显示
        public List<InventoryUIConfig> UIConfigs = new List<InventoryUIConfig>();
        
        [Serialize]
        public List<Item> Items = new List<Item>();



        public override void Init(bool logMessage = true)
        {
            this.logMessage = logMessage;
            CreateInventoryFolder();
            LoadInventoryData();
        }


        public void CreateInventoryFolder()
        {
            DataService.Instance.CreateDataFolder("Data/InventoryData");
            DataService.Instance.CreateDataFolder("Data/InventoryData/Inventory");
            DataService.Instance.CreateDataFolder("Data/InventoryData/InventoryConfigs");
            DataService.Instance.CreateDataFolder("Data/InventoryData/Items");
            DataService.Instance.CreateDataFolder("Data/InventoryData/Sprites");
        }

        public void LoadInventoryData()
        {
            LogMessage("读取容器数据");
            DataService.Instance.SaveJson(new InventoryUIConfig(), "Data/InventoryData/example.json");
            DataService.Instance.SaveJson(new Item(),"Data/InventoryData/Items/example.json");
            UIConfigs = DataService.Instance.LoadAllJson("Data/InventoryData/InventoryConfigs", UIConfigs);
            Inventorys = DataService.Instance.LoadAllJson("Data/InventoryData/Inventory", Inventorys);
            Items = DataService.Instance.LoadAllJson("Data/InventoryData/Items", Items);
        }


        //保存所有的容器
        public void SaveInventoryData()
        {
            foreach (var inventory in Inventorys)
            {
                DataService.Instance.SaveJson(inventory, "Data/InventoryData/Inventory" + inventory.InventoryName);
            }
        }


        [System.Serializable]
        public class InventoryUIConfig
        {
            public int positionX;

            public int positionY;

            //容器格子行数
            public int height;

            //容器格子列数
            public int width;

            //背景素材
            public string inventoryBackGroundSprite;

            //格子素材
            public string itemContainerSprite;

            //格子的间隔
            public int slotpadding;
            public int inventorypadding;
            
            public int slotHeight;
            public int slotWidth;

            public DescriptionUIConfig DescriptionUIConfig;

            public SwitchPageConfig SwitchPageConfig;

        }

        [System.Serializable]
        public class DescriptionUIConfig
        {
            public string spriteName;
            public int descriptionHeight;
            public int descriptionWidth;
            public int decriptionX;
            public int decriptionY;
            
            public int textHeight;
            public int textWidth;
            public int textX;
            public int textY;
            
            public int itemNameHeight;
            public int itemNameWidth;
            public int itemNameX;
            public int itemNameY;
            public string itemSpriteName;
        }
        [System.Serializable]
        public class SwitchPageConfig
        {
            public string leftButtonSprite;
            public string rightButtonSprite;
            public int buttonWidth;
            public int buttonHeight;
            public int leftButtonx;
            public int leftButtony;
            public int rightButtonx;
            public int rightButtony;
        }
    }
}