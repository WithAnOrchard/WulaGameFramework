using System.Collections.Generic;
using EssSystem.EssManager.UIManager.Entity;

namespace EssSystem.EssManager.UIManager.Dao
{
    public class UICanvas : AdjustableUI
    {
        public Dictionary<string, UIComponent> components = new();

        public UICanvasGameObject UICanvasGameObject;

        public UIComponent GetComponent(string id)
        {
            if (components.TryGetValue(id, out var component)) return component;
            return null;
        }
    }
}