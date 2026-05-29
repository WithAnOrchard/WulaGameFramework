using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EssSystem.Core.Base
{
    /// <summary>
    /// Manager 注册表（Phase 3 架构优化）
    /// 
    /// 职责分离：
    /// - AbstractGameManager：生命周期管理（Awake/OnDestroy）
    /// - ManagerRegistry：Manager 的发现、注册、查询
    /// 
    /// 功能：
    /// - 集中管理所有 Manager 的元数据
    /// - 支持按优先级查询
    /// - 支持按类型查询
    /// - 提供统计信息
    /// </summary>
    public class ManagerRegistry
    {
        /// <summary>Manager 元数据</summary>
        public struct ManagerMetadata
        {
            public MonoBehaviour Component;
            public Type Type;
            public int Priority;
            public string Name;
            public bool IsInitialized;
        }

        private readonly List<ManagerMetadata> _managers = new();
        private readonly Dictionary<Type, ManagerMetadata> _managersByType = new();
        private readonly Dictionary<string, ManagerMetadata> _managersByName = new();

        /// <summary>注册 Manager</summary>
        public void Register(MonoBehaviour component, int priority)
        {
            if (component == null) return;

            var type = component.GetType();
            var name = type.Name;

            // 检查重复注册
            if (_managersByType.ContainsKey(type))
            {
                Debug.LogWarning($"[ManagerRegistry] Manager {name} 已注册，跳过重复注册");
                return;
            }

            var metadata = new ManagerMetadata
            {
                Component = component,
                Type = type,
                Priority = priority,
                Name = name,
                IsInitialized = false
            };

            _managers.Add(metadata);
            _managersByType[type] = metadata;
            _managersByName[name] = metadata;
        }

        /// <summary>标记 Manager 已初始化</summary>
        public void MarkInitialized(Type type)
        {
            if (_managersByType.TryGetValue(type, out var metadata))
            {
                metadata.IsInitialized = true;
                _managersByType[type] = metadata;
            }
        }

        /// <summary>获取所有 Manager（按优先级排序）</summary>
        public IEnumerable<ManagerMetadata> GetAllManagers()
        {
            return _managers.OrderBy(m => m.Priority);
        }

        /// <summary>按类型获取 Manager</summary>
        public ManagerMetadata? GetManager(Type type)
        {
            return _managersByType.TryGetValue(type, out var metadata) ? metadata : null;
        }

        /// <summary>按名称获取 Manager</summary>
        public ManagerMetadata? GetManager(string name)
        {
            return _managersByName.TryGetValue(name, out var metadata) ? metadata : null;
        }

        /// <summary>获取指定优先级范围的 Manager</summary>
        public IEnumerable<ManagerMetadata> GetManagersByPriorityRange(int minPriority, int maxPriority)
        {
            return _managers
                .Where(m => m.Priority >= minPriority && m.Priority <= maxPriority)
                .OrderBy(m => m.Priority);
        }

        /// <summary>获取基础 Manager（优先级 < 0）</summary>
        public IEnumerable<ManagerMetadata> GetBaseManagers()
        {
            return GetManagersByPriorityRange(int.MinValue, -1);
        }

        /// <summary>获取业务 Manager（优先级 >= 0）</summary>
        public IEnumerable<ManagerMetadata> GetBusinessManagers()
        {
            return GetManagersByPriorityRange(0, int.MaxValue);
        }

        /// <summary>检查 Manager 是否已注册</summary>
        public bool IsRegistered(Type type)
        {
            return _managersByType.ContainsKey(type);
        }

        /// <summary>检查 Manager 是否已初始化</summary>
        public bool IsInitialized(Type type)
        {
            return _managersByType.TryGetValue(type, out var metadata) && metadata.IsInitialized;
        }

        /// <summary>获取 Manager 数量</summary>
        public int GetManagerCount()
        {
            return _managers.Count;
        }

        /// <summary>获取已初始化的 Manager 数量</summary>
        public int GetInitializedCount()
        {
            return _managers.Count(m => m.IsInitialized);
        }

        /// <summary>获取统计信息</summary>
        public Dictionary<string, object> GetStats()
        {
            var baseManagers = GetBaseManagers().ToList();
            var businessManagers = GetBusinessManagers().ToList();

            return new Dictionary<string, object>
            {
                { "TotalCount", _managers.Count },
                { "InitializedCount", GetInitializedCount() },
                { "BaseManagerCount", baseManagers.Count },
                { "BusinessManagerCount", businessManagers.Count },
                { "AverageInitializationTime", 0f }
            };
        }

        /// <summary>清空注册表</summary>
        public void Clear()
        {
            _managers.Clear();
            _managersByType.Clear();
            _managersByName.Clear();
        }

        /// <summary>获取所有 Manager 的详细信息</summary>
        public List<Dictionary<string, object>> GetDetailedInfo()
        {
            var details = new List<Dictionary<string, object>>();

            foreach (var metadata in GetAllManagers())
            {
                details.Add(new Dictionary<string, object>
                {
                    { "Name", metadata.Name },
                    { "Type", metadata.Type.FullName },
                    { "Priority", metadata.Priority },
                    { "IsInitialized", metadata.IsInitialized },
                    { "Category", metadata.Priority < 0 ? "Base" : "Business" }
                });
            }

            return details;
        }
    }
}
