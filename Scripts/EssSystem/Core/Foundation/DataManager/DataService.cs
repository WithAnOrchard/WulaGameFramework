using System;
using System.Collections.Generic;
using System.IO;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace EssSystem.Core.Foundation.DataManager
{
    /// <summary>
    /// 数据服务 - 负责所有 Service 的数据持久化管理
    /// </summary>
    public class DataService : Service<DataService>
    {
        /// <summary>
        /// 数据文件夹名称
        /// </summary>
        private const string DATA_FOLDER = "ServiceData";

        /// <summary>
        /// 数据文件夹完整路径
        /// </summary>
        private string _dataFolder;

        /// <summary>
        /// 所有已注册的 Service 实例列表
        /// </summary>
        private List<IServicePersistence> _serviceInstances;

        /// <summary>
        /// Service 去重检查集合（O(1) 查询）— Phase 1.1 优化
        /// </summary>
        private HashSet<IServicePersistence> _serviceInstancesSet;

        /// <summary>
        /// 数据文件夹是否已创建 — Phase 1.2 延迟初始化
        /// </summary>
        private bool _dataFolderCreated = false;

        /// <summary>
        /// 初始化数据服务
        /// </summary>
        protected override void Initialize()
        {
            // 设置数据文件夹路径（不立即创建，延迟到实际保存时）— Phase 1.2 优化
            _dataFolder = Path.Combine(UnityEngine.Application.persistentDataPath, DATA_FOLDER);

            // 初始化 Service 实例列表
            _serviceInstances = new List<IServicePersistence>();

            // 初始化 Service 去重检查集合 — Phase 1.1 优化
            _serviceInstancesSet = new HashSet<IServicePersistence>();

            // 注册 Service 初始化事件监听器（必须在base.Initialize()之前注册）
            // 不预注册自己，让后面 base.Initialize() 触发的 EVT_INITIALIZED 走同一条路径加入 — D3 避免 "Service 已存在" 噪音警告。
            if (EventProcessor.HasInstance)
            {
                EventProcessor.Instance.AddListener(EVT_INITIALIZED, OnServiceInitialized);
            }

            // 调用基类初始化（会触发 EVT_INITIALIZED 事件，本机监听器会把 this 加入 _serviceInstances）
            base.Initialize();

            Log("数据服务初始化完成", Color.green);

            // 注册应用退出事件，自动保存数据
            UnityEngine.Application.quitting += OnApplicationQuit;
        }

        /// <summary>
        /// 应用退出时的回调，保存所有 Service 数据
        /// </summary>
        private void OnApplicationQuit()
        {
            SaveAllServiceData();
        }

        /// <summary>
        /// 获取所有已注册的 Service 实例
        /// </summary>
        /// <returns>Service 实例列表</returns>
        public IReadOnlyList<IServicePersistence> GetServiceInstances()
        {
            return _serviceInstances;
        }

        /// <summary>
        /// Service 初始化事件处理器
        /// 当有新 Service 初始化时，自动注册
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据，包含 Service 实例</param>
        /// <returns>空列表</returns>
        public List<object> OnServiceInitialized(string eventName, List<object> data)
        {
            if (data.Count > 0 && data[0] is IServicePersistence service)
            {
                var serviceTypeName = service.GetType().Name;
                // Phase 1.1 优化：使用 HashSet.Add() 替代 Contains() 检查，O(n) → O(1)
                if (_serviceInstancesSet.Add(service))
                {
                    _serviceInstances.Add(service);
                    Log($"Service 初始化并注册: {serviceTypeName}", Color.blue);
                }
                // D3: 不再警告重复注册——重启 Editor PlayMode 或偶发事件重发均不是错误，静默幂等即可。
            }
            return null;   // D4: EventProcessor 的 TriggerEvent 在 result == null 时跳过 AddRange，免一次 alloc。
        }

        /// <summary>
        /// 确保数据文件夹存在 — Phase 1.2 延迟初始化
        /// </summary>
        private void EnsureDataFolder()
        {
            if (_dataFolderCreated) return;

            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            _dataFolderCreated = true;
        }

        /// <summary>
        /// 保存所有已注册 Service 的数据 — Phase 1.3 优化：添加保存统计
        /// </summary>
        private void SaveAllServiceData()
        {
            int successCount = 0;
            int failCount = 0;
            var startTime = Time.realtimeSinceStartup;

            foreach (var serviceInstance in _serviceInstances)
            {
                try
                {
                    EnsureDataFolder();  // Phase 1.2 延迟创建
                    serviceInstance.SaveAllCategories();
                    successCount++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    LogWarning($"保存Service数据失败: {serviceInstance.GetType().Name} - {ex.Message}");
                }
            }

            var duration = Time.realtimeSinceStartup - startTime;
            if (failCount > 0)
            {
                LogWarning($"数据保存完成: {successCount} 成功, {failCount} 失败 (耗时 {duration:F3}s)");
            }
            else
            {
                Log($"所有 {successCount} 个 Service 数据保存成功 (耗时 {duration:F3}s)", Color.green);
            }
        }
    }
}
