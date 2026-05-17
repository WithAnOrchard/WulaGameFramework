using System;

namespace EssSystem.Core.Application.SingleManagers.ShopManager.Dao
{
    /// <summary>
    /// 商店全局策略 —— 价格 markup / markdown / 白名单 / CHA 阈值。
    /// 与 <see cref="ShopStock.SellbackRatio"/> 共同作用：
    /// <c>买入价 = stock.BasePrice * BuyMarkupRatio * (1 - 折扣)</c>，
    /// <c>卖出价 = item.Value * stock.SellbackRatio * SellMarkdownRatio * (1 + CHA加成)</c>。
    /// </summary>
    [Serializable]
    public class ShopPolicy
    {
        /// <summary>商人加价倍率（基础售价 × 此值）。1.2 = 加价 20%。</summary>
        public float BuyMarkupRatio = 1.2f;

        /// <summary>商人收购降价倍率。0.5 = 收 50%。</summary>
        public float SellMarkdownRatio = 0.5f;

        /// <summary>商人接收的物品类型白名单（int = InventoryItemType 枚举值；空 = 全部接受）。</summary>
        public int[] AcceptedSellTypes;

        /// <summary>玩家初始折扣需要达到的最低 CHA 阈值（≤ 此值 = 无折扣）。</summary>
        public int PlayerInitialDiscountChaThreshold = 12;
    }
}
