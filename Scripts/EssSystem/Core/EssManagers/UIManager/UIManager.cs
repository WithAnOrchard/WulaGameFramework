using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;

namespace EssSystem.Core.EssManagers.UIManager
{
    /// <summary>
    ///     UI管理器 - Unity MonoBehaviour单例，用于UI管理
    /// </summary>
    [Manager(5)]
    public class UIManager : Manager<UIManager>
    {
        protected override void Initialize()
        {
            base.Initialize();
            Log("UIManager 初始化完成", UnityEngine.Color.green);
        }

        /// <summary>
        ///     注册UI实体
        /// </summary>
        [Event("RegisterUIEntity")]
        public System.Collections.Generic.List<object> RegisterUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;
            var entity = data[1] as Entity.UIEntity;

            if (string.IsNullOrEmpty(daoId) || entity == null)
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceRegisterUIEntity",
                new System.Collections.Generic.List<object> { daoId, entity });

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }

        /// <summary>
        ///     获取UI实体
        /// </summary>
        [Event("GetUIEntity")]
        public System.Collections.Generic.List<object> GetUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;

            if (string.IsNullOrEmpty(daoId))
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceGetUIEntity",
                new System.Collections.Generic.List<object> { daoId });

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }

        /// <summary>
        ///     注销UI实体
        /// </summary>
        [Event("UnregisterUIEntity")]
        public System.Collections.Generic.List<object> UnregisterUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;

            if (string.IsNullOrEmpty(daoId))
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceUnregisterUIEntity",
                new System.Collections.Generic.List<object> { daoId });

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }
    }
}