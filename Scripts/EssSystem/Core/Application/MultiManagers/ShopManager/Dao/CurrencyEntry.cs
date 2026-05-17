using System;

namespace EssSystem.Core.Application.MultiManagers.ShopManager.Dao
{
    /// <summary>
    /// 货币条目 —— 注册到 ShopManager 的虚拟货币定义。
    /// <para>
    /// 内部实现上货币是特殊 InventoryItem（<c>InventoryItemType.Currency</c>），
    /// 玩家钱包是命名 Inventory（如 <c>wallet_{playerId}</c>），
    /// 但 ShopManager 单独索引 <see cref="CurrencyEntry"/> 以避免遍历 InventoryService。
    /// </para>
    /// </summary>
    [Serializable]
    public class CurrencyEntry
    {
        /// <summary>唯一 Id（"gold" / "silver" / "rune_token"）。</summary>
        public string Id;

        /// <summary>显示名。</summary>
        public string DisplayName;

        /// <summary>图标 Sprite Id。</summary>
        public string IconSpriteId;
    }
}
