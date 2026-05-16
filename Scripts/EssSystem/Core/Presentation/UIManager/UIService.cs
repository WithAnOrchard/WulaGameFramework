using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Presentation.UIManager.Dao;
using EssSystem.Core.Presentation.UIManager.Entity;

namespace EssSystem.Core.Presentation.UIManager
{
    /// <summary>UI 服务 —— 业务核心：UIEntity 缓存 + UIComponent 树持久化 + 创建/销毁。外部统一通过 <see cref="UIManager"/> 的事件调用。</summary>
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

        // ============================================================
        // 注册 / 获取 / 销毁
        // ============================================================
        /// <summary>注册 UIComponent 树，递归创建对应 UIEntity 并挂到 Canvas 下。</summary>
        public UIEntity RegisterUIEntity(string daoId, UIComponent component, Transform canvasTransform)
        {
            if (string.IsNullOrEmpty(daoId) || component == null || canvasTransform == null)
            {
                Log("RegisterUIEntity 参数无效", Color.red);
                return null;
            }

            try
            {
                // 整树持久化合并为单次 flush（N 次 SetData → 1 次 fsync）
                using (BeginBatch())
                {
                    StoreComponentTreeRecursive(component);
                }

                var entity = CreateEntityRecursive(component, canvasTransform);
                if (entity == null)
                {
                    Log("创建 UIEntity 失败", Color.red);
                    return null;
                }

                _uiEntityCache[daoId] = entity;
                Log($"RegisterUIEntity 完成: {daoId}（缓存 {_uiEntityCache.Count}）", Color.green);
                return entity;
            }
            catch (Exception ex)
            {
                Log($"RegisterUIEntity 异常: {ex.Message}\n{ex.StackTrace}", Color.red);
                return null;
            }
        }

        /// <summary>把已有 UIEntity 直接注入缓存（供 <see cref="UIEntity"/>.Awake 调用）。</summary>
        public void RegisterUIEntity(string daoId, UIEntity entity)
        {
            if (string.IsNullOrEmpty(daoId) || entity == null) return;
            _uiEntityCache[daoId] = entity;
        }

        /// <summary>查 UIEntity（缓存命中返实例，否则 null）。</summary>
        public UIEntity GetUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId)) return null;
            _uiEntityCache.TryGetValue(daoId, out var entity);
            return entity;
        }

        /// <summary>仅从缓存移除（不销毁 GameObject）—— 供 <see cref="UIEntity"/>.OnDestroy 调用。</summary>
        public void UnregisterUIEntity(string daoId)
        {
            if (!string.IsNullOrEmpty(daoId)) _uiEntityCache.Remove(daoId);
        }

        /// <summary>销毁 UIEntity（GameObject + 缓存条目一并移除）。</summary>
        public void DestroyUIEntity(string daoId)
        {
            if (string.IsNullOrEmpty(daoId)) return;
            if (!_uiEntityCache.TryGetValue(daoId, out var entity)) return;

            if (entity != null && entity.gameObject != null)
                UnityEngine.Object.Destroy(entity.gameObject);
            _uiEntityCache.Remove(daoId);
            Log($"销毁 UI: {daoId}（剩余 {_uiEntityCache.Count}）", Color.yellow);
        }

        // ============================================================
        // 热重载
        // ============================================================
        /// <summary>从磁盘重新加载 UI 配置；异常返 false。</summary>
        public bool HotReloadConfigs()
        {
            try
            {
                LoadData();
                Log("UI 配置热重载完成", Color.green);
                return true;
            }
            catch (Exception ex)
            {
                Log($"UI 配置热重载失败: {ex.Message}", Color.red);
                return false;
            }
        }

        // ============================================================
        // 内部递归
        // ============================================================
        private void StoreComponentTreeRecursive(UIComponent component)
        {
            if (component == null || string.IsNullOrEmpty(component.Id)) return;
            SetData(UI_COMPONENTS_CATEGORY, component.Id, component);
            foreach (var child in component.GetChildren())
                StoreComponentTreeRecursive(child);
        }

        private UIEntity CreateEntityRecursive(UIComponent component, Transform parent)
        {
            var entity = UIEntityFactory.CreateEntity(component, parent);
            if (entity == null) return null;
            foreach (var child in component.GetChildren())
                CreateEntityRecursive(child, entity.transform);
            return entity;
        }
    }
}
