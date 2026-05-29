using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using UnityEngine;

namespace EssSystem.Core.Base
{
    /// <summary>
    /// 抽象游戏管理器 - 用于初始化和管理当前 GameObject 及其子对象上的所有 Manager
    /// <para>
    /// 功能：
    /// - 自动添加基础 Manager（UIManager、ResourceManager、DataManager）
    /// - 自动发现当前 GameObject 及所有子 GameObject 上的 Manager&lt;T&gt; 组件
    /// - 根据 ManagerAttribute 优先级排序并初始化
    /// - 提供统一的生命周期管理
    /// </para>
    /// </summary>
    public abstract class AbstractGameManager : MonoBehaviour
    {
        /// <summary>
        /// Manager 注册表（Phase 3 架构优化）
        /// </summary>
        private ManagerRegistry _managerRegistry = new();

        /// <summary>
        /// 已管理的 Manager 列表（保留向后兼容）
        /// </summary>
        private readonly List<MonoBehaviour> _managedManagers = new List<MonoBehaviour>();

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _initialized = false;

        /// <summary>
        /// 是否启用异步初始化（Phase 2.2 优化）
        /// </summary>
        [SerializeField] private bool _enableAsyncInitialization = false;

        /// <summary>
        /// 异步初始化的最大帧数（防止卡顿）
        /// </summary>
        [SerializeField] private int _maxFramesPerInitialization = 1;

        /// <summary>
        /// 初始化所有 Manager
        /// </summary>
        protected virtual void Awake()
        {
            EnsureBaseManagers();
            if (_enableAsyncInitialization)
            {
                StartCoroutine(DiscoverAndInitializeManagersAsync());
            }
            else
            {
                DiscoverAndInitializeManagers();
            }
        }

        /// <summary>
        /// 确保基础 Manager 存在（EventProcessor、DataManager、ResourceManager、AudioManager、UIManager）
        /// </summary>
        private void EnsureBaseManagers()
        {
            // 按优先级顺序定义基础 Manager 类型
            var baseManagerTypes = new[]
            {
                ("EventProcessor", -30),
                ("DataManager", -20),
                ("ResourceManager", 0),
                ("AudioManager", 3),
                ("UIManager", 5)
            };

            foreach (var (managerName, priority) in baseManagerTypes)
            {
                var managerType = FindManagerType(managerName);
                if (managerType != null && GetComponentInChildren(managerType) == null)
                {
                    var managerObj = new GameObject(managerName);
                    managerObj.transform.SetParent(transform);
                    managerObj.AddComponent(managerType);
                    Debug.Log($"[AbstractGameManager] 自动添加基础 Manager: {managerName} (优先级: {priority})");
                }
            }
        }

        /// <summary>
        /// 查找 Manager 类型
        /// </summary>
        private Type FindManagerType(string managerName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (AssemblyUtils.IsSystemAssembly(assembly)) continue;

                try
                {
                    var type = assembly.GetTypes()
                        .FirstOrDefault(t => t.Name == managerName && IsManagerType(t));
                    if (type != null) return type;
                }
                catch
                {
                    continue;
                }
            }
            return null;
        }


        /// <summary>
        /// 发现并初始化所有 Manager
        /// </summary>
        private void DiscoverAndInitializeManagers()
        {
            if (_initialized) return;

            // 获取当前 GameObject 及所有子 GameObject 上的所有 MonoBehaviour 组件
            var allComponents = GetComponentsInChildren<MonoBehaviour>();

            // 筛选出 Manager<T> 类型的组件并注册到 ManagerRegistry
            foreach (var component in allComponents)
            {
                if (component == this) continue;

                var componentType = component.GetType();
                if (IsManagerType(componentType))
                {
                    var priority = GetManagerPriority(componentType);
                    _managerRegistry.Register(component, priority);
                    _managedManagers.Add(component);
                }
            }

            // 按优先级获取所有 Manager
            var sortedManagers = _managerRegistry.GetAllManagers();

            // 初始化所有 Manager
            foreach (var metadata in sortedManagers)
            {
                _managerRegistry.MarkInitialized(metadata.Type);
                Debug.Log($"[AbstractGameManager] 发现并管理 Manager: {metadata.Name} (优先级: {metadata.Priority})");
            }

            _initialized = true;
            var stats = _managerRegistry.GetStats();
            Debug.Log($"[AbstractGameManager] 共管理 {stats["TotalCount"]} 个 Manager");
        }

        /// <summary>
        /// 异步发现并初始化所有 Manager（Phase 2.2 优化）
        /// </summary>
        private IEnumerator DiscoverAndInitializeManagersAsync()
        {
            if (_initialized) yield break;

            // 获取当前 GameObject 及所有子 GameObject 上的所有 MonoBehaviour 组件
            var allComponents = GetComponentsInChildren<MonoBehaviour>();

            // 筛选出 Manager<T> 类型的组件并注册到 ManagerRegistry
            int processedThisFrame = 0;

            foreach (var component in allComponents)
            {
                if (component == this) continue;

                var componentType = component.GetType();
                if (IsManagerType(componentType))
                {
                    var priority = GetManagerPriority(componentType);
                    _managerRegistry.Register(component, priority);
                    _managedManagers.Add(component);

                    processedThisFrame++;
                    if (processedThisFrame >= _maxFramesPerInitialization)
                    {
                        yield return null;
                        processedThisFrame = 0;
                    }
                }
            }

            // 按优先级获取所有 Manager
            var sortedManagers = _managerRegistry.GetAllManagers();

            // 异步初始化所有 Manager
            foreach (var metadata in sortedManagers)
            {
                _managerRegistry.MarkInitialized(metadata.Type);
                Debug.Log($"[AbstractGameManager] 发现并管理 Manager: {metadata.Name} (优先级: {metadata.Priority})");
                yield return null;
            }

            _initialized = true;
            var stats = _managerRegistry.GetStats();
            Debug.Log($"[AbstractGameManager] 异步初始化完成，共管理 {stats["TotalCount"]} 个 Manager");
        }

        /// <summary>
        /// 判断类型是否为 Manager&lt;T&gt;
        /// </summary>
        private bool IsManagerType(Type type)
        {
            if (type == null || type == typeof(AbstractGameManager)) return false;

            // 检查是否继承自 Manager<T>
            var currentType = type;
            while (currentType != null && currentType != typeof(MonoBehaviour))
            {
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(EssSystem.Core.Base.Manager.Manager<>))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }

            return false;
        }

        /// <summary>
        /// 获取 Manager 的优先级
        /// </summary>
        private int GetManagerPriority(Type type)
        {
            var attribute = type.GetCustomAttribute<EssSystem.Core.Base.Manager.ManagerAttribute>();
            return attribute?.order ?? 0;
        }

        /// <summary>
        /// 获取所有已管理的 Manager
        /// </summary>
        public IEnumerable<MonoBehaviour> GetManagedManagers()
        {
            return _managedManagers;
        }

        /// <summary>
        /// 根据类型获取已管理的 Manager
        /// </summary>
        public T GetManager<T>() where T : MonoBehaviour
        {
            return _managedManagers.FirstOrDefault(m => m is T) as T;
        }

        /// <summary>
        /// 检查是否管理了指定类型的 Manager
        /// </summary>
        public bool HasManager<T>() where T : MonoBehaviour
        {
            return _managedManagers.Any(m => m is T);
        }

        /// <summary>
        /// 获取 Manager 注册表（Phase 3 架构优化）
        /// </summary>
        public ManagerRegistry GetManagerRegistry()
        {
            return _managerRegistry;
        }

        /// <summary>
        /// 获取 Manager 统计信息（Phase 3 架构优化）
        /// </summary>
        public Dictionary<string, object> GetManagerStats()
        {
            return _managerRegistry.GetStats();
        }

        /// <summary>
        /// 获取所有 Manager 的详细信息（Phase 3 架构优化）
        /// </summary>
        public List<Dictionary<string, object>> GetManagerDetails()
        {
            return _managerRegistry.GetDetailedInfo();
        }

        /// <summary>
        /// 清理所有 Manager
        /// </summary>
        protected virtual void OnDestroy()
        {
            _managedManagers.Clear();
        }

        /// <summary>
        /// Manager 信息结构
        /// </summary>
        private struct ManagerInfo
        {
            public MonoBehaviour Component;
            public Type Type;
            public int Priority;
        }
    }
}
