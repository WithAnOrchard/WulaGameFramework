using EssSystem;
using EssSystem.Core;
using EssSystem.Inventory;
using EssSystem.Inventory.InventoryUI;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.GameObject
{
    public class  ItemUIGameObject: MonoBehaviour
    {
        public Item item;
        public UnityEngine.GameObject ItemGameObject;
        public RawImage itemImage;
        public RawImage backgroundImage;
        
        public Button ItemButton;

        private void Awake()
        {
           
        }

        public void Init(InventoryGameObject inventoryGameObject,InventoryManager.InventoryUIConfig inventoryUIConfig)
        {
          
            backgroundImage=gameObject.AddComponent<RawImage>();
            backgroundImage.texture = ResourceLoaderManager.Instance.GetSprite(inventoryUIConfig.itemContainerSprite).texture;
            ItemGameObject=new UnityEngine.GameObject();
            ItemGameObject.transform.SetParent(transform);
            itemImage=ItemGameObject.AddComponent<RawImage>();
            RectTransform rectTransform=gameObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta=new Vector2(inventoryUIConfig.slotWidth,inventoryUIConfig.slotHeight);
            
             ItemButton=gameObject.AddComponent<Button>();
             ItemButton.onClick.AddListener(() =>
             {
                 ShowDescription(inventoryGameObject);
             });
        }

        public void ShowDescription(InventoryGameObject inventoryGameObject)
        {
            if(item==null)return;
            inventoryGameObject.DescriptionUIGameObject.text.text = "\n\n\n\n"+item.ItemDescription;
            inventoryGameObject.DescriptionUIGameObject.itemName.text = ""+item.ItemName;
        }

        public void LoadItem(Item item)
        {
            this.item = item;
            itemImage.texture = ResourceLoaderManager.Instance.GetSprite(item.Sprite).texture;
        }
        
        
    }
}