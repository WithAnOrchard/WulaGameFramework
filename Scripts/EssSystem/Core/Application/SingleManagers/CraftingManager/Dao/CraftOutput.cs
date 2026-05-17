using System;

namespace EssSystem.Core.Application.SingleManagers.CraftingManager.Dao
{
    /// <summary>
    /// 配方输出物品 —— "成功后产出 N 个 X 物品（按 Chance 概率）"。
    /// </summary>
    [Serializable]
    public class CraftOutput
    {
        /// <summary>InventoryItem.Id。</summary>
        public string ItemId;

        /// <summary>产出数量。</summary>
        public int Count = 1;

        /// <summary>命中概率（0~1）；&lt; 1 = 副产物（如制铁可能掉炉灰）。</summary>
        public float Chance = 1f;

        public CraftOutput() { }
        public CraftOutput(string itemId, int count = 1, float chance = 1f)
        {
            ItemId = itemId; Count = count; Chance = chance;
        }
    }
}
