using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.UIManager.Entity;
using EssSystem.Core.EssManagers.UIManager.Dao;

namespace EssSystem.Core.EssManagers.UIManager
{
    /// <summary>UI 服务 — 业务逻辑核心，外部统一通过 <see cref="UIManager"/> 暴露的事件调用。</summary>
    public class UIService : Service<UIService>
    {
        public const string UI_COMPONENTS_CATEGORY = "UIComponents";

        /// <summary>UIEntity 内存缓存（不持久化）。</summary>
        private readonly Dictionary<string, UIEntity> _uiEntityCache = new();

        protected override void Initialize()
        {
            base.Initialize();
            Log("UIService 初始化完成", Color.green);
        }

        #region Public API (typed)

        /// <summary>热重载 UI 配置（重载磁盘数据）。</summary>
        public bool HotReloadConfigs()
        {
            try
            {
                Log("UIService开始热重载UI配置", Color.yellow);
                LoadData();
                Log("UI配置热重载完成", Color.green);
                return true;
            }
            catch (System.Exception ex)
            {
                Log($"UI配置热重载失败: {ex.Message}", Color.red);
                return false;
            }
        }

        /// <summary>
        ///     注册UI实体（直接调用，UIManager 传入 canvasTransform，无需反射）
        /// </summary>
        /// <param name="daoId">UI 组件 ID</param>
        /// <param name="component">UI 组件数据</param>
        /// <param name="canvasTransform">由 UIManager 提供的 Canvas Transform</param>
        /// <returns>创建的 UIEntity，失败返回 null</returns>
        public UIEntity RegisterUIEntity(string daoId, UIComponent component, Transform canvasTransform)
        {
            if (string.IsNullOrEmpty(daoId) || component == null || canvasTransform == null)
            {
                Log("RegisterUIEntity 参数无效", Color.red);
                return null;
            }

            try
            {
                Log($"RegisterUIEntity 开始: {daoId}", Color.yellow);

                // 将UIComponent存储到数据存储中（用于热重载）
                SetData(UI_COMPONENTS_CATEGORY, daoId, component);
                StoreComponentTreeRecursive(component);

                // 递归创建UIEntity（包括子组件）
                var entity = CreateEntityRecursive(component, canvasTransform);
                if (entity == null)
                {
                    Log("创建UIEntity失败", Color.red);
                    return null;
                }

                // 存储UIEntity到内存缓存
                _uiEntityCache[daoId] = entity;

                Log($"RegisterUIEntity 完成: {daoId}, 缓存数量: {_uiEntityCache.Count}", Color.green);
                return entity;
            }
            catch (System.Exception ex)
            {
                Log($"RegisterUIEntity 异常: {ex.Message}\n{ex.StackTrace}", Color.red);
                return null;
            }
        }

        /// <summary>
        ///     直接获取UI实体
        /// </summary>
        public UIEntity GetUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId))
                return null;

            _uiEntityCache.TryGetValue(daoId, out var entity);
            return entity;
        }

        /// <summary>
        ///     直接注册已有的UI实体到缓存
        /// </summary>
        public void RegisterUIEntity(string daoId, UIEntity entity)
        {
            if (string.IsNullOrEmpty(daoId) || entity == null)
                return;

            _uiEntityCache[daoId] = entity;
        }

        /// <summary>
        ///     仅从缓存移除（不销毁 GameObject）— 供 UIEntity.OnDestroy 调用
        /// </summary>
        public void UnregisterUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId))
                return;

            _uiEntityCache.Remove(daoId);
        }

        /// <summary>
        ///     注销并销毁UI实体（销毁 GameObject + 移除缓存）
        /// </summary>
        public void DestroyUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId))
                return;

            if (_uiEntityCache.TryGetValue(daoId, out var entity))
            {
                if (entity != null && entity.gameObject != null)
                {
                    Log($"销毁GameObject: {entity.gameObject.name}", Color.yellow);
                    Object.Destroy(entity.gameObject);
                }
                _uiEntityCache.Remove(daoId);
                Log($"从缓存移除: {daoId}, 剩余数量: {_uiEntityCache.Count}", Color.yellow);
            }
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// 递归存储UIComponent树到数据存储
        /// </summary>
        private void StoreComponentTreeRecursive(UIComponent component)
        {
            if (component == null || string.IsNullOrEmpty(component.Id))
                return;

            SetData(UI_COMPONENTS_CATEGORY, component.Id, component);

            foreach (var child in component.GetChildren())
            {
                StoreComponentTreeRecursive(child);
            }
        }

        /// <summary>
        /// 递归创建UIEntity及其子组件
        /// </summary>
        private UIEntity CreateEntityRecursive(UIComponent component, Transform parent)
        {
            var entity = Entity.UIEntityFactory.CreateEntity(component, parent);
            if (entity == null) return null;

            foreach (var childComponent in component.GetChildren())
            {
                CreateEntityRecursive(childComponent, entity.transform);
            }

            return entity;
        }

        #endregion
    }
}