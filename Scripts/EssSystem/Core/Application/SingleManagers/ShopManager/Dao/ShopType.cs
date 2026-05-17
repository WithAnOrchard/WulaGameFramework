namespace EssSystem.Core.Application.SingleManagers.ShopManager.Dao
{
    /// <summary>
    /// 商店类别 —— 决定 UI 标题 / 默认接收的物品白名单 / 价格倾向。
    /// </summary>
    public enum ShopType
    {
        /// <summary>杂货 / 通用商店（默认）。</summary>
        General = 0,
        /// <summary>武器铺。</summary>
        Weapon = 1,
        /// <summary>护甲铺。</summary>
        Armor = 2,
        /// <summary>魔法 / 卷轴 / 药水店。</summary>
        Magic = 3,
        /// <summary>黑市（接收 Quest / 稀有物品，价格波动大）。</summary>
        BlackMarket = 4,
    }
}
