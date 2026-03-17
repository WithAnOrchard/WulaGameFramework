using System;
using System.Collections.Generic;
using System.Linq;
using EssSystem.Core;
using Inventory.GameObject;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Inventory.InventoryUI
{
    public class InventoryGameObject : MonoBehaviour
    {
        //使用的uiConfig
        public InventoryManager.InventoryUIConfig InventoryUIConfig;

        public List<ItemUIGameObject> ItemObjects = new List<ItemUIGameObject>();
        
        public int currentPage = 0;
        
        
        public DescriptionUIGameObject DescriptionUIGameObject;
        
        public void LoadInventoryUI(Inventory inventory,int page)
        {
            if(page<0||page>=inventory.MaxStack/(inventory.x*inventory.y)){return;}
            foreach (var itemObject in ItemObjects.ToList())
            {
                ItemObjects.Remove(itemObject);
                Destroy(itemObject.gameObject);
            }
            RawImage rawImage;
            if (GetComponent<RawImage>())
            {
                rawImage = gameObject.GetComponent<RawImage>();
            }
            else
            {
                rawImage =  gameObject.AddComponent<RawImage>();
            }
            rawImage.texture=ResourceLoaderManager.Instance.GetSprite(InventoryUIConfig.inventoryBackGroundSprite).texture;
            rawImage.rectTransform.sizeDelta = new Vector2(InventoryUIConfig.width,InventoryUIConfig.height);
            rawImage.rectTransform.anchoredPosition=new Vector2(InventoryUIConfig.positionX,InventoryUIConfig.positionY);
            
            //初始化容器
            for (int j = inventory.y; j >0 ; j--)
            { 
                for (int i =0 ; i <inventory.x ; i++)
                {
                
                    UnityEngine.GameObject ItemGameObject= new UnityEngine.GameObject();
                    ItemUIGameObject itemUIGameObject = ItemGameObject.AddComponent<ItemUIGameObject>();
                    
                   
                    ItemObjects.Add(itemUIGameObject);
                    ItemGameObject.transform.SetParent(transform);
                    itemUIGameObject.Init(this,InventoryUIConfig);
                    RectTransform rectTransform = itemUIGameObject.GetComponent<RectTransform>();
                    rectTransform.anchoredPosition = new Vector2
                    (InventoryUIConfig.inventorypadding+i*InventoryUIConfig.slotpadding-InventoryUIConfig.width/inventory.x
                        , InventoryUIConfig.inventorypadding+(j-1)*InventoryUIConfig.slotpadding-InventoryUIConfig.width/inventory.y);
                    
                    if (Math.Abs(j-inventory.y) * inventory.x + i+page*inventory.x*inventory.y< inventory.Items.Count)
                    {
                        itemUIGameObject.LoadItem(inventory.Items[Math.Abs(j-inventory.y) * inventory.x +  i+page*inventory.x*inventory.y]);
                    }
                    
                }
            }
            currentPage = page;
        }
        
    }
}