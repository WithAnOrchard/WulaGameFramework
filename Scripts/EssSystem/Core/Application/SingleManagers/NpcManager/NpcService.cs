using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.SingleManagers.NpcManager
{
    /// <summary>
    /// NPC 业务服务 —— 持久化 NpcConfig 注册表 + 维护运行时 NpcInstance 集合。
    /// <para>
    /// <b>骨架阶段</b>：仅承载 Service 数据存储约定与日志通道；
    /// Spawn / Despawn / InteractNpc / InteractionPanel UI 在
    /// <c>Demo/Tribe/ToDo.md #4</c> 前置 NPC 里程碑（M1-M3）实施。
    /// </para>
    /// </summary>
    public class NpcService : Service<NpcService>
    {
        #region 数据分类

        /// <summary>已注册的 NpcConfig（按 Id）。</summary>
        public const string CAT_CONFIGS   = "NpcConfigs";

        /// <summary>运行时 NpcInstance（按 InstanceId）。</summary>
        public const string CAT_INSTANCES = "NpcInstances";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("NpcService 初始化完成（骨架）", Color.green);
        }
    }
}
