using System;
using Unity.VisualScripting;

namespace EssSystem.Inventory
{
    [Serializable]
    public class Item
    {
        public string ItemName;
        public string ItemDescription;
        public string Sprite;
        public int MaxNumber;
        public int Number;
    }
}