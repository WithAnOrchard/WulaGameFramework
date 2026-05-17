using System;

namespace EssSystem.Core.Application.SingleManagers.ShopManager.Dao
{
    /// <summary>
    /// 单条商品库存。
    /// <para>
    /// <see cref="BasePrice"/> ≤ 0 时回退使用 <c>InventoryItem.Value</c>；
    /// <see cref="Stock"/> = -1 表示无限库存（魔法商店常用）。
    /// </para>
    /// </summary>
    [Serializable]
    public class ShopStock
    {
        /// <summary>关联的 InventoryItem.Id。</summary>
        public string ItemId;

        /// <summary>基础售价（金币）。≤0 = 用 Item.Value 回退。</summary>
        public int BasePrice;

        /// <summary>当前库存。-1 = 无限。</summary>
        public int Stock;

        /// <summary>玩家卖回该物品的价格倍率（基于 Item.Value）。</summary>
        public float SellbackRatio = 0.5f;

        /// <summary>是否启用补货。</summary>
        public bool RestockEnabled;

        /// <summary>补货间隔（秒）。</summary>
        public float RestockSeconds;

        /// <summary>每次补货数量。</summary>
        public int RestockAmount;
    }
}
