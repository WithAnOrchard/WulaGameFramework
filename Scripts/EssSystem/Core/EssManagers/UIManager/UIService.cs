using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.UIManager.Entity;
using EssSystem.Core.EssManagers.UIManager.Dao;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
using UnityEngine;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.UIManager
{
    /// <summary>
    ///     UI服务 - 包含所有UI业务逻辑和事件处理
    /// </summary>
    public class UIService : Service<UIService>
    {
        public const string UI_COMPONENTS_CATEGORY = "UIComponents";

        // 内存中缓存UIEntity（不持久化到文件）
        private readonly Dictionary<string, UIEntity> _uiEntityCache = new Dictionary<string, UIEntity>();

        protected override void Initialize()
        {
            base.Initialize();
            Log("UIService 初始化完成", UnityEngine.Color.green);
        }

        /// <summary>
        /// 热重载UI配置
        /// </summary>
        [Event("ServiceHotReloadUIConfigs")]
        public System.Collections.Generic.List<object> HotReloadUIConfigs(System.Collections.Generic.List<object> data)
        {
            try
            {
                Log("UIService开始热重载UI配置", UnityEngine.Color.yellow);

                // 重新加载Service数据
                LoadData();

                Log("UI配置热重载完成", UnityEngine.Color.green);
                return new System.Collections.Generic.List<object> { "成功" };
            }
            catch (System.Exception ex)
            {
                Log($"UI配置热重载失败: {ex.Message}", UnityEngine.Color.red);
                return new System.Collections.Generic.List<object> { $"失败: {ex.Message}" };
            }
        }

        /// <summary>
        ///     注册UI实体
        /// </summary>
        [Event("ServiceRegisterUIEntity")]
        public System.Collections.Generic.List<object> RegisterUIEntity(System.Collections.Generic.List<object> data)
        {
            try
            {
                Log("RegisterUIEntity开始", UnityEngine.Color.yellow);

                string daoId = data[0] as string;
                var component = data[1] as UIComponent;

                Log($"参数: daoId={daoId}, component={(component != null)}", UnityEngine.Color.yellow);

                if (string.IsNullOrEmpty(daoId) || component == null)
                {
                    Log("参数无效", UnityEngine.Color.red);
                    return new System.Collections.Generic.List<object> { "参数无效" };
                }

                // 获取UIManager的Canvas
                if (UIManager.HasInstance == false || UIManager.Instance == null)
                {
                    Log("UIManager未初始化", UnityEngine.Color.red);
                    return new System.Collections.Generic.List<object> { "UIManager未初始化" };
                }

                // 获取Canvas Transform（通过反射获取私有字段）
                var canvasTransform = GetUIManagerTransform();
                if (canvasTransform == null)
                {
                    Log("Canvas未找到", UnityEngine.Color.red);
                    return new System.Collections.Generic.List<object> { "Canvas未找到" };
                }

                Log($"父对象: {canvasTransform.name}, 路径: {canvasTransform.gameObject.scene.name}/{canvasTransform.gameObject.name}", UnityEngine.Color.yellow);

                // 将UIComponent存储到数据存储中（用于热重载）
                SetData(UI_COMPONENTS_CATEGORY, daoId, component);
                StoreComponentTreeRecursive(component);

                // 递归创建UIEntity（包括子组件）
                Log("开始创建UIEntity", UnityEngine.Color.yellow);
                var entity = CreateEntityRecursive(component, canvasTransform);
                if (entity == null)
                {
                    Log("创建UIEntity失败", UnityEngine.Color.red);
                    return new System.Collections.Generic.List<object> { "创建UIEntity失败" };
                }

                Log($"UIEntity创建成功: {entity.gameObject?.name}, GameObject存在: {entity.gameObject != null}", UnityEngine.Color.green);

                // 存储UIEntity到内存缓存
                _uiEntityCache[daoId] = entity;

                Log($"RegisterUIEntity完成，缓存数量: {_uiEntityCache.Count}", UnityEngine.Color.green);

                // 检查GameObject状态
                if (entity.gameObject != null)
                {
                    Log($"GameObject状态: 名称={entity.gameObject.name}, activeSelf={entity.gameObject.activeSelf}, 父对象={entity.gameObject.transform.parent?.name}, 父对象路径={entity.gameObject.transform.parent?.gameObject.scene.name}/{entity.gameObject.transform.parent?.gameObject.name}");
                }

                return new System.Collections.Generic.List<object> { "成功" };
            }
            catch (System.Exception ex)
            {
                Log($"ServiceRegisterUIEntity异常: {ex.Message}\n{ex.StackTrace}", UnityEngine.Color.red);
                return new System.Collections.Generic.List<object> { $"异常: {ex.Message}" };
            }
        }

      

        /// <summary>
        /// 递归存储UIComponent树到数据存储
        /// </summary>
        private void StoreComponentTreeRecursive(UIComponent component)
        {
            if (component == null || string.IsNullOrEmpty(component.Id))
                return;

            // 存储当前组件
            SetData(UI_COMPONENTS_CATEGORY, component.Id, component);

            // 递归存储子组件
            foreach (var child in component.GetChildren())
            {
                StoreComponentTreeRecursive(child);
            }
        }

        /// <summary>
        /// 获取UIManager的Transform
        /// </summary>
        private Transform GetUIManagerTransform()
        {
            if (UIManager.HasInstance == false || UIManager.Instance == null)
            {
                return null;
            }

            // 通过反射获取UIManager的Canvas Transform
            var canvasField = typeof(UIManager).GetField("_uiCanvas",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (canvasField != null)
            {
                var canvas = canvasField.GetValue(UIManager.Instance) as Canvas;
                if (canvas != null)
                {
                    return canvas.transform;
                }
            }

            // 如果没有Canvas，使用UIManager的Transform
            return UIManager.Instance.transform;
        }

        /// <summary>
        /// 递归创建UIEntity及其子组件
        /// </summary>
        private UIEntity CreateEntityRecursive(UIComponent component, Transform parent)
        {
            // 使用UIEntityFactory创建UIEntity
            var entity = Entity.UIEntityFactory.CreateEntity(component, parent);
            if (entity == null) return null;

            // 递归创建子组件
            foreach (var childComponent in component.GetChildren())
            {
                var childEntity = CreateEntityRecursive(childComponent, entity.transform);
            }

            return entity;
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

            if (_uiEntityCache.TryGetValue(daoId, out var entity))
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

            Log($"UnregisterUIEntity调用: {daoId}, 当前缓存数量: {_uiEntityCache.Count}", UnityEngine.Color.yellow);

            if (_uiEntityCache.TryGetValue(daoId, out var entity))
            {
                // 销毁GameObject
                if (entity != null && entity.gameObject != null)
                {
                    Log($"销毁GameObject: {entity.gameObject.name}", UnityEngine.Color.yellow);
                    Object.Destroy(entity.gameObject);
                }
                _uiEntityCache.Remove(daoId);
                Log($"从缓存移除后，剩余数量: {_uiEntityCache.Count}", UnityEngine.Color.yellow);
            }
            else
            {
                Log($"UIEntity不存在于缓存中: {daoId}", UnityEngine.Color.yellow);
            }

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

            _uiEntityCache.TryGetValue(daoId, out var entity);
            return entity;
        }

        /// <summary>
        ///     直接注册UI实体（供内部调用，非事件处理器）
        /// </summary>
        public void RegisterUIEntity(string daoId, UIEntity entity)
        {
            if (string.IsNullOrEmpty(daoId) || entity == null)
                return;

            _uiEntityCache[daoId] = entity;
        }

        /// <summary>
        ///     直接注销UI实体（供内部调用，非事件处理器）
        /// </summary>
        public void UnregisterUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId))
                return;

            if (_uiEntityCache.TryGetValue(daoId, out var entity))
            {
                // 销毁GameObject
                if (entity != null && entity.gameObject != null)
                {
                    Object.Destroy(entity.gameObject);
                }
                _uiEntityCache.Remove(daoId);
            }
        }

        #endregion
    }
}