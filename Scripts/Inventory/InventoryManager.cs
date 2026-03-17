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
        public List<Inventory> Inventorys = new List<Inventory>();

        [SerializeField]
        public List<InventoryGameObject> InventoryGameObjects = new List<InventoryGameObject>();

        //所有的UI类型显示
        public List<InventoryUIConfig> UIConfigs = new List<InventoryUIConfig>();
        
        [Serialize]
        public List<Item> Items = new List<Item>();



        //显示容器UI
        public void CreateInventoryUI(Inventory inventory, InventoryUIConfig inventoryUIConfig)
        {
            UnityEngine.GameObject gameObject= new UnityEngine.GameObject(inventory.InventoryName);
            gameObject.transform.SetParent(transform);
            InventoryGameObject inventoryGameObject = gameObject.AddComponent<InventoryGameObject>();
            InventoryGameObjects.Add(inventoryGameObject);
            
            inventoryGameObject.InventoryUIConfig = inventoryUIConfig;
            inventoryGameObject.name = inventory.InventoryName;
            inventoryGameObject.LoadInventoryUI(inventory,0);
            inventoryGameObject.gameObject.SetActive(false);
            
            UnityEngine.GameObject description= new UnityEngine.GameObject("description");
            description.AddComponent<RectTransform>();
            description.transform.SetParent(gameObject.transform);
            
            DescriptionUIGameObject descriptionUIGameObject = description.AddComponent<DescriptionUIGameObject>();
            descriptionUIGameObject.LoadDescription(inventoryUIConfig.DescriptionUIConfig);

            CreateSwitchButton(inventory, inventoryGameObject,inventoryUIConfig);
            
            inventoryGameObject.DescriptionUIGameObject = descriptionUIGameObject;

        }

        public void CreateSwitchButton(Inventory inventory, InventoryGameObject inventoryGameObject,InventoryUIConfig inventoryUIConfig)
        {
            UnityEngine.GameObject leftButtonGameobject = new UnityEngine.GameObject("leftButton");
            UnityEngine.GameObject rightButtonGameobject = new UnityEngine.GameObject("rightButton");
            leftButtonGameobject.transform.SetParent(inventoryGameObject.transform);
            rightButtonGameobject.transform.SetParent(inventoryGameObject.transform);
            Button leftButton= leftButtonGameobject.AddComponent<Button>();
            Button rightButton = rightButtonGameobject.AddComponent<Button>();
            leftButton.image=leftButton.gameObject.AddComponent<Image>();
            rightButton.image=rightButton.gameObject.AddComponent<Image>();
            
            leftButton.image.sprite = ResourceLoaderManager.Instance.GetSprite(inventoryUIConfig.SwitchPageConfig.leftButtonSprite);
            rightButton.image.sprite = ResourceLoaderManager.Instance.GetSprite(inventoryUIConfig.SwitchPageConfig.rightButtonSprite);

            leftButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(inventoryUIConfig.SwitchPageConfig.leftButtonx,inventoryUIConfig.SwitchPageConfig.leftButtony);
            rightButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(inventoryUIConfig.SwitchPageConfig.rightButtonx,inventoryUIConfig.SwitchPageConfig.rightButtony);
            
            leftButton.image.rectTransform.sizeDelta = new Vector2(inventoryUIConfig.SwitchPageConfig.buttonWidth,inventoryUIConfig.SwitchPageConfig.buttonHeight);
            rightButton.image.rectTransform.sizeDelta = new Vector2(inventoryUIConfig.SwitchPageConfig.buttonWidth,inventoryUIConfig.SwitchPageConfig.buttonHeight);
            
            leftButton.onClick.AddListener(() =>
            {
                inventoryGameObject.LoadInventoryUI(inventory,inventoryGameObject.currentPage-1);
            });
            rightButton.onClick.AddListener(() =>
            {
                inventoryGameObject.LoadInventoryUI(inventory,inventoryGameObject.currentPage+1);
            });
        }
        

        //创建容器
        public Inventory CreateInventory(string inventoryName, int maxStack,int x,int y)
        {
            Inventory inventory = new Inventory(inventoryName, maxStack,x,y);
            Inventorys.Add(inventory);
            return inventory;
        }

        public void OpenOrInventoryUI(string inventoryName)
        {
            LogMessage("打开背包"+inventoryName);
            foreach (var inventoryGameObject in InventoryGameObjects)
            {
                if (inventoryGameObject.name.Equals(inventoryName))
                {
                    if (!inventoryGameObject.gameObject.activeSelf)
                    {
                        inventoryGameObject.gameObject.SetActive(true);
                        inventoryGameObject.DescriptionUIGameObject.text.text = "";
                        inventoryGameObject.DescriptionUIGameObject.itemName.text = "";
                    }
                    else
                    { 
                        inventoryGameObject.gameObject.SetActive(false);
                        
                    }
                }
            }
        }

        public Inventory GetInventory(string inventoryName)
        {
            foreach (var inventory in Inventorys)
            {
                if (inventoryName.Equals(inventory.InventoryName))
                {
                    return inventory;
                }
            }
            return null;
        }

        public Item GetItem(string itemName)
        {
            foreach (var item in Items)
            {
                if (item.ItemName .Equals(itemName))
                {
                    return item;
                }
            }
            return null;
        }

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