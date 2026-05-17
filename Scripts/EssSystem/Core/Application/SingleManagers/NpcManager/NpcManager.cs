using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.SingleManagers.NpcManager
{
    /// <summary>
    /// NPC 门面 —— 配置注册 / 实例化 / 互动路由。
    /// <para>
    /// 设计依据：<c>Demo/Tribe/ToDo.md #4</c>（NPC + Shop 双 EssManager v1）。<br/>
    /// 与 DialogueManager / CharacterManager / ShopManager 协作：本 Manager 仅管"是谁/在哪"，
    /// 具体对白 / 商店 / 任务由 NpcConfig 字段反向触达对应模块。
    /// </para>
    /// <para>
    /// <b>骨架阶段</b>：Manager / Service 已挂链；事件 API 与 InteractionPanel 待 M1-M3 实施。
    /// </para>
    /// </summary>
    [Manager(17)]
    public class NpcManager : Manager<NpcManager>
    {
        public NpcService Service => NpcService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("NpcManager 初始化完成（骨架）", Color.green);
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
