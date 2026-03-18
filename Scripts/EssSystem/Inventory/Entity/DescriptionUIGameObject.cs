using EssSystem;
using EssSystem.Core;
using EssSystem.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Inventory.GameObject
{
    public class DescriptionUIGameObject : MonoBehaviour
    {
        public Text itemName;
        
        public Text text;
        
        public RawImage image;
        
        public RawImage itemNameImage;
        // Start is called before the first frame update
        void Start()
        {
        }

        public void LoadDescription(InventoryManager.DescriptionUIConfig description)
        { 
            UnityEngine.GameObject textObject = new UnityEngine.GameObject();
            UnityEngine.GameObject ImageOjbect = new UnityEngine.GameObject();
            ImageOjbect.transform.SetParent(transform);
            textObject.transform.SetParent(transform);
            text = textObject.AddComponent<Text>();
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 25;
            text.color = Color.black;
            text.fontStyle = FontStyle.Bold;
            image=ImageOjbect.AddComponent<RawImage>();
            image.texture = ResourceLoaderManager.Instance.GetSprite(description.spriteName).texture;
            RectTransform imageRectTransform= image.GetComponent<RectTransform>();
            gameObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(description.decriptionX,description.decriptionY);
            imageRectTransform.sizeDelta = new Vector2(description.descriptionWidth, description.descriptionHeight);
            imageRectTransform.anchoredPosition = new Vector2(description.decriptionX,description.decriptionY);
            text.rectTransform.sizeDelta = new Vector2(description.textWidth, description.textHeight);
            text.rectTransform.anchoredPosition = new Vector2(description.textX, description.textY);
           
            UnityEngine.GameObject itemImageGameObject = new UnityEngine.GameObject();
            itemImageGameObject.transform.SetParent(transform);
            itemName = itemImageGameObject.AddComponent<Text>();
            itemName.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemName.fontSize = 40;
            itemName.alignment = TextAnchor.MiddleLeft;
            itemName.color = Color.black;
            itemName.fontStyle = FontStyle.Bold;
            itemName.rectTransform.sizeDelta = new Vector2(description.itemNameWidth, description.itemNameHeight);
            itemName.rectTransform.anchoredPosition = new Vector2(description.itemNameX, description.itemNameY);
            
            
        }
        
        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
