using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.UIManager.Entity;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;

namespace EssSystem.Core.EssManagers.UIManager
{
    /// <summary>
    ///     UI服务 - 包含所有UI业务逻辑和事件处理
    /// </summary>
    public class UIService : Service<UIService>
    {
        public const string UI_COMPONENTS_CATEGORY = "UIComponents";
        public const string UI_ENTITIES_CATEGORY = "UIEntities";

        protected override void Initialize()
        {
            base.Initialize();
            Log("UIService 初始化完成", UnityEngine.Color.green);
        }

        /// <summary>
        ///     注册UI实体
        /// </summary>
        [Event("ServiceRegisterUIEntity")]
        public System.Collections.Generic.List<object> RegisterUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;
            var entity = data[1] as UIEntity;

            if (string.IsNullOrEmpty(daoId) || entity == null)
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            SetData(UI_ENTITIES_CATEGORY, daoId, entity);
            return new System.Collections.Generic.List<object> { "成功" };
        }

        /// <summary>
        ///     获取UI实体
        /// </summary>
        [Event("ServiceGetUIEntity")]
        public System.Collections.Generic.List<object> GetUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;

            if (string.IsNullOrEmpty(daoId))
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            var entity = GetData<UIEntity>(UI_ENTITIES_CATEGORY, daoId);
            if (entity != null)
            {
                return new System.Collections.Generic.List<object> { "成功", entity };
            }
            return new System.Collections.Generic.List<object> { "未找到实体" };
        }

        /// <summary>
        ///     注销UI实体
        /// </summary>
        [Event("ServiceUnregisterUIEntity")]
        public System.Collections.Generic.List<object> UnregisterUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;

            if (string.IsNullOrEmpty(daoId))
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            RemoveData(UI_ENTITIES_CATEGORY, daoId);
            return new System.Collections.Generic.List<object> { "成功" };
        }

        // ─────────────────────────────────────────────────────────────
        #region Direct Methods (for internal use)

        /// <summary>
        ///     直接获取UI实体（供内部调用，非事件处理器）
        /// </summary>
        public UIEntity GetUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId))
                return null;

            return GetData<UIEntity>(UI_ENTITIES_CATEGORY, daoId);
        }

        /// <summary>
        ///     直接注册UI实体（供内部调用，非事件处理器）
        /// </summary>
        public void RegisterUIEntity(string daoId, UIEntity entity)
        {
            if (string.IsNullOrEmpty(daoId) || entity == null)
                return;

            SetData(UI_ENTITIES_CATEGORY, daoId, entity);
        }

        /// <summary>
        ///     直接注销UI实体（供内部调用，非事件处理器）
        /// </summary>
        public void UnregisterUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId))
                return;

            RemoveData(UI_ENTITIES_CATEGORY, daoId);
        }

        #endregion
    }
}