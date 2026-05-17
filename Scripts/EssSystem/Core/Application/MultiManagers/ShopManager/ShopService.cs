using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.MultiManagers.ShopManager
{
    /// <summary>
    /// 商店业务服务 —— 持久化 ShopConfig + CurrencyEntry，运行时维护钱包余额查询。
    /// <para>
    /// <b>骨架阶段</b>：仅承载 Service 数据存储约定与日志通道；
    /// Buy / Sell 事务、价格公式、Wallet API、ShopUI 在
    /// <c>Demo/Tribe/ToDo.md #4</c> 后置 Shop 里程碑（M4-M6）实施。
    /// </para>
    /// </summary>
    public class ShopService : Service<ShopService>
    {
        #region 数据分类

        /// <summary>已注册的 ShopConfig（按 Id）。</summary>
        public const string CAT_CONFIGS    = "ShopConfigs";

        /// <summary>已注册的 CurrencyEntry（按 Id）。</summary>
        public const string CAT_CURRENCIES = "Currencies";

        /// <summary>钱包余额索引（按 playerId+currencyId 复合键，运行时查询用）。</summary>
        public const string CAT_WALLETS    = "Wallets";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("ShopService 初始化完成（骨架）", Color.green);
        }
    }
}
