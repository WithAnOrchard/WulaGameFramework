using System;
using System.Collections.Generic;
using EssSystem.Core.AbstractClass;
using EssSystem.EssManager.UIManager.Dao;
using EssSystem.EssManager.UIManager.Entity;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.EssManager.UIManager
{
    public class UIService : ServiceBase
    {
        public static UIService Instance => InstanceWithInit<UIService>(instance =>
        {
            if (!instance.DataSpaces.ContainsKey("Canvas"))
                instance.DataSpaces.Add("Canvas", new Dictionary<string, object>());
        });


        //通过DataSpaces获取Canvas运行时实体
        public UICanvas GetCanvas(string id)
        {
            if (DataSpaces["Canvas"].ContainsKey(id)) return DataSpaces["Canvas"][id] as UICanvas;
            return null;
        }

        //生成一个Canvas
        public UICanvasGameObject LoadCanvas(UICanvas canvas, Transform parent)
        {
            var canvasObj = new GameObject();
            canvasObj.name = canvas.id;
            canvasObj.transform.SetParent(parent);
            canvasObj.AddComponent<Canvas>();
            canvasObj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var root = new GameObject();
            root.name = "root";
            root.AddComponent<RectTransform>();
            root.transform.SetParent(canvasObj.transform);

            var uiCanvasGameObject = root.AddComponent<UICanvasGameObject>();
            uiCanvasGameObject.UICanvas = canvas;

            var canvasRect = root.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(canvas.size.x, canvas.size.y);

            foreach (var uiComponent in canvas.components.Values) GenerateComponent(uiComponent, canvas);

            //存入DataSpaces
            DataSpaces["Canvas"].Add(canvas.id, canvas);

            return uiCanvasGameObject;
        }

        //为uiCanvas内部生成component
        public void GenerateComponent(UIComponent uiComponent, UICanvas uiCanvas)
        {
            uiComponent.canvas = uiCanvas;
            switch (uiComponent.uiType)
            {
                case UIType.Button:
                    Button(uiComponent, uiComponent.id);
                    break;
                case UIType.Text:
                    ;
                    Text(uiComponent, uiComponent.id);
                    break;
                case UIType.Image:
                    ;
                    Image(uiComponent, uiComponent.id);
                    break;
                case UIType.Bar:
                    ;
                    Bar(uiComponent, uiComponent.id);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }


        //对一个UIComponent初始化 并且将其作为一个Bar 返还回去
        public void Bar(UIComponent uiComponent, string name)
        {
            uiComponent.entity = new GameObject(name);
        }

        public void Button(UIComponent uiComponent, string name)
        {
            uiComponent.entity = new GameObject(name);
            uiComponent.entity.AddComponent<Button>();
            uiComponent.entity.AddComponent<Image>();
        }

        public void Text(UIComponent uiComponent, string name)
        {
            uiComponent.entity = new GameObject(name);
            uiComponent.entity.AddComponent<Text>();
        }

        public void Image(UIComponent uiComponent, string name)
        {
            uiComponent.uiType = UIType.Image;
            uiComponent.entity = new GameObject(name);
            uiComponent.entity.AddComponent<Image>();
        }

        public void Size(UIComponent uiComponent, Vector2Int size)
        {
            var componentRect = uiComponent.entity.GetComponent<RectTransform>();
            componentRect.sizeDelta = new Vector2(size.x, size.y);
        }

        public void Position(UIComponent uiComponent, Vector2Int position)
        {
            var componentRect = uiComponent.entity.GetComponent<RectTransform>();
            componentRect.anchoredPosition = new Vector2(position.x, position.y);
        }
    }
}