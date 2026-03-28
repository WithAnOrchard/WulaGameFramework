using System.Collections.Generic;
using EssSystem.EssManager.UIManager.Dao;
using UnityEngine;

namespace EssSystem.EssManager.UIManager.Entity
{
    public class UICanvasGameObject : MonoBehaviour
    {
        public List<UIComponentGameObject> components = new();
        public UICanvas UICanvas;

        public UIComponentGameObject GetComponent(string id)
        {
            foreach (var component in components)
                if (component.UIComponent.id.Equals(id))
                    return component;

            return null;
        }
    }
}