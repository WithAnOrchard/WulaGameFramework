using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.SingleManagers.SceneInstanceManager
{
    /// <summary>
    /// 子场景 / 副本门面 —— 多人在线友好的实例化场景管理器。
    /// <para>
    /// 设计依据：<c>Demo/Tribe/ToDo.md #3</c>（传送门 + 子场景实例 v1）。<br/>
    /// 关键策略：所有 Instance 与 OverWorld 共存于同一 Unity Scene，
    /// 通过坐标偏移并存（不冻结 OverWorld），多个玩家可分布于不同 Instance。
    /// </para>
    /// <para>
    /// <b>骨架阶段</b>：Manager / Service 已注册到优先级链，但事件 API
    /// （RegisterInstance / EnterInstance / ExitInstance / Hibernation）尚未实现；
    /// 详见 ToDo #3.M1。
    /// </para>
    /// </summary>
    [Manager(16)]
    public class SceneInstanceManager : Manager<SceneInstanceManager>
    {
        public SceneInstanceService Service => SceneInstanceService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("SceneInstanceManager 初始化完成（骨架）", Color.green);
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
