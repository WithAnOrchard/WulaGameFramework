using System;
using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.Dao;
using EssSystem.Core.Singleton;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EssSystem.UIManager
{
    public class UIManager:SingletonMono<UIManager>
    {
        //所有的Canvas数据
        public List<UICanvas> canvases = new();
        
        public List<UICanvasGameObject> canvasGameObjects = new();
        
        public Font defaultFont=ResourceLoaderManager.Instance.GetFont("LegacyRuntime");

        //所有的组件实体
        
        public override void Init(bool logMessage)
        {
            DataService.Instance.CreateDataFolder("Data/UIData/Canvas");
            GenerateExamples();
            LoadData();
        }

        public void LoadData()
        {
           DataService.Instance.LoadAllJson("Data/UIData/Canvas",canvases);
        }




        public UICanvasGameObject GetCanvasGameObject(string id)
        {
            foreach (var uiCanvasGameObject in canvasGameObjects)
            {
                if(uiCanvasGameObject.UICanvas.id.Equals(id))
                {return uiCanvasGameObject;}
            }
            return null;
        }


        //生成一个UI
        public void LoadCanvas(UICanvas canvas)
        {
            GameObject canvasObj = new GameObject();
            canvasObj.name = canvas.id;
            canvasObj.transform.SetParent(transform);
            canvasObj.AddComponent<Canvas>();
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            
            GameObject root = new GameObject();
            root.name = "root";
            root.AddComponent<RectTransform>();
            root.transform.SetParent(canvasObj.transform);
            
            UICanvasGameObject uiCanvasGameObject=  root.AddComponent<UICanvasGameObject>();
            uiCanvasGameObject.UICanvas = canvas;
            
            
            RectTransform canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvas.size.x, canvas.size.y);
            canvasRect.anchoredPosition=new Vector2(canvas.position.x,canvas.position.y);

            foreach (var uiComponent in canvas.components)
            {
                GenerateComponent(uiComponent,uiCanvasGameObject);
            }
            
            canvasGameObjects.Add(uiCanvasGameObject);
            
        }

        public void GenerateComponent(UIComponent component,UICanvasGameObject parent)
        {
            GameObject uiObj = new GameObject();
            UIComponentGameObject uiComponentGameObject = uiObj.AddComponent<UIComponentGameObject>();
            uiComponentGameObject.UIComponent = component;
            
            uiObj.name = component.id;
            uiObj.transform.SetParent(parent.transform);
          
            switch (component.uiType)
            {
                case UIType.Button:
                {
                    uiObj.AddComponent<Button>();
                    Image rawImage = uiObj.AddComponent<Image>();
                    rawImage.sprite =ResourceLoaderManager.Instance.GetSprite(component.value);
                    break;
                }

                case UIType.Image:
                { 
                    Image rawImage = uiObj.AddComponent<Image>();
                    rawImage.sprite =ResourceLoaderManager.Instance.GetSprite(component.value);
                    break;
                }
                case UIType.Text:
                {
                    Text text= uiObj.AddComponent<Text>();
                    text.font = ResourceLoaderManager.Instance.GetFont(component.value);
                    text.fontSize = 30;
                    text.alignment = TextAnchor.MiddleCenter;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
            RectTransform componentRect = uiObj.GetComponent<RectTransform>();
            componentRect.sizeDelta = new Vector2(component.size.x, component.size.y);
            componentRect.anchoredPosition=new Vector2(component.position.x,component.position.y);
            parent.components.Add(uiComponentGameObject);
            
        }

        public void GenerateExamples()
        {
            UICanvas canvas = new UICanvas();
            canvas.id = "ExampleCanvas";
            canvas.position = new Vector2Int(0, 0);
            canvas.size = new Vector2Int(100, 100);
            
            UIComponent uiComponent = new UIComponent();
            uiComponent.id = "ExampleUIComponent";
            uiComponent.position = new Vector2Int(0, 0);
            uiComponent.size = new Vector2Int(100, 100);
            uiComponent.uiType = UIType.Image;
            
            canvas.components.Add(uiComponent);
            DataService.Instance.SaveJson(canvas, "Data/UIData/Canvas/ExampleCanvas.json");
        }
    }





    public class UIComponentGameObject:MonoBehaviour
    {
        public UIComponent UIComponent;
    }
    
    public class UICanvasGameObject:MonoBehaviour
    {
        public UICanvas UICanvas;
        public List<UIComponentGameObject> components = new List<UIComponentGameObject>();

        public UIComponentGameObject GetComponent(string id)
        {
            foreach (var component in components)
            {
                if (component.UIComponent.id.Equals(id))
                {
                    return component;
                }
            }

            return null;
        }
    }


    [System.Serializable]
    public class UICanvas
    {
        public string id;
        public Vector2Int position;
        public Vector2Int size;
        public List<UIComponent> components = new List<UIComponent>();

        public UIComponent GetComponent(string id)
        {
            foreach (var component in components)
            {
                if (component.id.Equals(id))
                {
                    return component;
                }
            }
            return null;
        }
    }

    [System.Serializable]
    public class UIComponent
    {
        public string id;
        public UIType uiType;
        public Vector2Int position;
        public Vector2Int size;
        public string value;
    }
    
    public enum UIType{
        Button,
        Image,
        Text,}
}