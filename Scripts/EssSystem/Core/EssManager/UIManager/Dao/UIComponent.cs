using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.EssManager.UIManager.Dao
{
    [Serializable]
    public class UIComponent : AdjustableUI
    {
        public UIType uiType;
        public string value;
        public GameObject entity;
        public UICanvas canvas;
        public Dictionary<string, UIComponent> Children;

        public void Size(Vector2Int size)
        {
            UIService.Instance.Size(this, size);
        }

        public void Position(Vector2Int position)
        {
            UIService.Instance.Position(this, position);
        }
    }
}