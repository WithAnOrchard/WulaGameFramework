using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.ShopManager.Dao
{
    /// <summary>
    /// 商店配置 —— 一家商店的全部静态数据。
    /// 多个 NPC 商人可关联到同一份 ShopConfig（如连锁商铺）。
    /// </summary>
    [Serializable]
    public class ShopConfig
    {
        /// <summary>唯一 Id。</summary>
        public string Id;

        /// <summary>显示名。</summary>
        public string DisplayName;

        /// <summary>关联的 NpcConfig.Id（反向索引；可选，多个 NPC 可指向同一 Shop）。</summary>
        public string OwnerNpcConfigId;

        /// <summary>商店类别。</summary>
        public ShopType Type = ShopType.General;

        /// <summary>商品列表。</summary>
        public List<ShopStock> Stock = new List<ShopStock>();

        /// <summary>价格 / 政策。</summary>
        public ShopPolicy Policy = new ShopPolicy();

        /// <summary>使用的货币 Id（默认 "gold"）。</summary>
        public string CurrencyId = "gold";
    }
}
