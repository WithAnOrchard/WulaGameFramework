using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EssSystem.Core.Util;
using UnityEngine;

namespace EssSystem.Core
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
        /// 已管理的 Manager 列表
        /// </summary>
        private readonly List<MonoBehaviour> _managedManagers = new List<MonoBehaviour>();

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _initialized = false;

        /// <summary>
        /// 初始化所有 Manager
        /// </summary>
        protected virtual void Awake()
        {
            EnsureBaseManagers();
            DiscoverAndInitializeManagers();
        }

        /// <summary>
        /// 确保基础 Manager 存在（EventProcessor、DataManager、ResourceManager、UIManager）
        /// </summary>
        private void EnsureBaseManagers()
        {
            // 按优先级顺序定义基础 Manager 类型
            var baseManagerTypes = new[]
            {
                ("EventProcessor", -30),
                ("DataManager", -20),
                ("ResourceManager", 0),
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

            // 筛选出 Manager<T> 类型的组件
            var managers = new List<ManagerInfo>();
            foreach (var component in allComponents)
            {
                if (component == this) continue; // 跳过自身

                var componentType = component.GetType();
                if (IsManagerType(componentType))
                {
                    var priority = GetManagerPriority(componentType);
                    managers.Add(new ManagerInfo
                    {
                        Component = component,
                        Type = componentType,
                        Priority = priority
                    });
                }
            }

            // 按优先级排序（数值越小越先执行）
            managers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            // 初始化所有 Manager
            foreach (var managerInfo in managers)
            {
                _managedManagers.Add(managerInfo.Component);
                Debug.Log($"[AbstractGameManager] 发现并管理 Manager: {managerInfo.Type.Name} (优先级: {managerInfo.Priority})");
            }

            _initialized = true;
            Debug.Log($"[AbstractGameManager] 共管理 {_managedManagers.Count} 个 Manager");
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
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(EssSystem.Core.EssManagers.Manager.Manager<>))
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
            var attribute = type.GetCustomAttribute<EssSystem.Core.EssManagers.Manager.ManagerAttribute>();
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
