using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.SingleManagers.SceneInstanceManager
{
    /// <summary>
    /// 子场景 / 副本业务服务 —— 维护 InstanceConfig 注册表 + 玩家 ↔ Instance 成员表。
    /// <para>
    /// <b>骨架阶段</b>：仅承载 Service 数据存储约定与日志通道；
    /// 进 / 出 Instance 流程、Hibernation、Membership、事件 API 在
    /// <c>Demo/Tribe/ToDo.md #3.M1</c> 里程碑实施时补齐。
    /// </para>
    /// </summary>
    public class SceneInstanceService : Service<SceneInstanceService>
    {
        #region 数据分类

        /// <summary>已注册的 InstanceConfig（按 Id）。</summary>
        public const string CAT_INSTANCES = "Instances";

        /// <summary>玩家 ↔ 当前所在 Instance 的 Membership（按 playerId）。</summary>
        public const string CAT_MEMBERSHIP = "Membership";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("SceneInstanceService 初始化完成（骨架）", Color.green);
        }
    }
}
