using System;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager.Dao
{
    /// <summary>
    /// 配方输入材料 —— "需要 N 个 X 物品"。
    /// </summary>
    [Serializable]
    public class CraftIngredient
    {
        /// <summary>InventoryItem.Id。</summary>
        public string ItemId;

        /// <summary>所需数量。</summary>
        public int Count = 1;

        public CraftIngredient() { }
        public CraftIngredient(string itemId, int count) { ItemId = itemId; Count = count; }
    }
}
