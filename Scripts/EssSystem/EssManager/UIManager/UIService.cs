using System;
using System.Collections.Generic;
using System.Linq;
using EssSystem.Core.Event;
using EssSystem.Core.Manager;
using EssSystem.UIManager.Dao;
using EssSystem.EssManager.UIManager.Entity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EssSystem.EssManager.UIManager
{
    /// <summary>
    /// UI服务 - 包含所有UI业务逻辑和事件处理
    /// </summary>
    public class UIService : Service<UIService>
    {
        public const string UI_COMPONENTS_CATEGORY = "UIComponents";
        public const string UI_ENTITIES_CATEGORY = "UIEntities";

        protected override void Initialize()
        {
            base.Initialize();
        }

        /// <summary>
        /// 注册UI实体
        /// </summary>
        /// <param name="daoId">Dao ID</param>
        /// <param name="entity">UI实体</param>
        public void RegisterUIEntity(string daoId, UIEntity entity)
        {
            if (string.IsNullOrEmpty(daoId) || entity == null) return;
            SetData(UI_ENTITIES_CATEGORY, daoId, entity);
        }

        /// <summary>
        /// 获取UI实体
        /// </summary>
        /// <param name="daoId">Dao ID</param>
        /// <returns>UI实体</returns>
        public UIEntity GetUIEntity(string daoId)
        {
            return GetData<UIEntity>(UI_ENTITIES_CATEGORY, daoId);
        }

        /// <summary>
        /// 注销UI实体
        /// </summary>
        /// <param name="daoId">Dao ID</param>
        public void UnregisterUIEntity(string daoId)
        {
            RemoveData(UI_ENTITIES_CATEGORY, daoId);
        }

       
    }
}
