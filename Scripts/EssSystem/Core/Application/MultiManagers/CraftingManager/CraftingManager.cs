using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager
{
    /// <summary>
    /// 制作门面 —— 配方注册 / 蓝图学习 / 工作台路由 / 制作 Session 控制。
    /// <para>
    /// 设计依据：<c>Demo/Tribe/ToDo.md #5</c>（装备制作 + 蓝图系统 v1）。<br/>
    /// 与 InventoryManager（材料消耗 / 产出 / 蓝图物品）+ IStats(#2)（CraftSkill / INT / STR
    /// 门槛和品质 roll）+ ShopManager(#4)（蓝图售卖）+ Tribe World Features（地图工作台）协作。
    /// </para>
    /// <para>
    /// <b>骨架阶段</b>：Manager / Service 已挂链；事件 API、品质 Modifier 注入、CraftSkill
    /// Capability 待 ToDo #5 各里程碑实施。
    /// </para>
    /// </summary>
    [Manager(18)]
    public class CraftingManager : Manager<CraftingManager>
    {
        public CraftingService Service => CraftingService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("CraftingManager 初始化完成（骨架）", Color.green);
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
