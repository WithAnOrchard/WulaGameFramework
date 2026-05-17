using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.SingleManagers.ShopManager
{
    /// <summary>
    /// 商店门面 —— 配置注册 / 货币 / 钱包 / Buy-Sell 事务原子性。
    /// <para>
    /// 设计依据：<c>Demo/Tribe/ToDo.md #4</c>（NPC + Shop 双 EssManager v1）。<br/>
    /// 与 InventoryManager / IStats(#2) / NpcManager 协作。事务原子性策略：
    /// 校验 → 扣钱 → 加物品 → 减库存，任一步失败立即回滚（详见 ToDo #4 第 (7) 节）。
    /// </para>
    /// <para>
    /// <b>骨架阶段</b>：Manager / Service 已挂链；具体业务逻辑待 ToDo #4 后置 M4-M6 实施。
    /// </para>
    /// </summary>
    [Manager(19)]
    public class ShopManager : Manager<ShopManager>
    {
        public ShopService Service => ShopService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("ShopManager 初始化完成（骨架）", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }
    }
}
