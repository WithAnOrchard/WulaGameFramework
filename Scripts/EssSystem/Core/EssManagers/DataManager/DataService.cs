using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Util;
using UnityEngine;

namespace EssSystem.Core.EssManagers.DataManager
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
        private List<object> _serviceInstances;

        /// <summary>
        /// 初始化数据服务
        /// </summary>
        protected override void Initialize()
        {
            // 设置数据文件夹路径
            _dataFolder = Path.Combine(Application.persistentDataPath, DATA_FOLDER);

            // 确保数据文件夹存在
            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            // 初始化 Service 实例列表
            _serviceInstances = new List<object>();

            // 注册 Service 初始化事件监听器（必须在base.Initialize()之前注册）
            if (EventManager.HasInstance)
            {
                EventManager.Instance.AddListener("OnServiceInitialized", OnServiceInitialized);
            }

            // DataService 自己注册自己
            _serviceInstances.Add(this);

            // 调用基类初始化（会触发OnServiceInitialized事件）
            base.Initialize();

            Log("数据服务初始化完成", Color.green);

            // 注册应用退出事件，自动保存数据
            Application.quitting += OnApplicationQuit;
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
        public List<object> GetServiceInstances()
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
            if (data.Count > 0 && data[0] != null)
            {
                var serviceInstance = data[0];
                var serviceTypeName = serviceInstance.GetType().Name;
                // 如果 Service 尚未注册，则注册
                if (!_serviceInstances.Contains(serviceInstance))
                {
                    _serviceInstances.Add(serviceInstance);
                    Log($"Service 初始化并注册: {serviceTypeName}", Color.blue);
                }
                else
                {
                    LogWarning($"Service 已存在，跳过注册: {serviceTypeName}");
                }
            }
            return new List<object>();
        }

        /// <summary>
        /// 保存所有已注册 Service 的数据
        /// </summary>
        private void SaveAllServiceData()
        {
            foreach (var serviceInstance in _serviceInstances)
            {
                SaveServiceData(serviceInstance);
            }
        }

        /// <summary>
        /// 保存指定 Service 的数据
        /// 通过反射调用Service的SaveCategoryData方法保存每个category
        /// </summary>
        /// <param name="serviceInstance">Service 实例</param>
        private void SaveServiceData(object serviceInstance)
        {
            try
            {
                var serviceType = serviceInstance.GetType();

                // 通过反射获取 Service 的 _dataStorage 字段
                var dataStorageField = serviceType.GetField("_dataStorage",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (dataStorageField != null)
                {
                    var dataStorage = dataStorageField.GetValue(serviceInstance) as
                        Dictionary<string, Dictionary<string, object>>;

                    if (dataStorage != null)
                    {
                        // 获取SaveCategoryData方法
                        var saveCategoryDataMethod = serviceType.GetMethod("SaveCategoryData",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        if (saveCategoryDataMethod != null)
                        {
                            // 对每个category调用SaveCategoryData
                            foreach (var category in dataStorage.Keys)
                            {
                                saveCategoryDataMethod.Invoke(serviceInstance, new object[] { category });
                            }
                            Log($"保存Service数据: {serviceType.Name} ({dataStorage.Count}个category)", Color.green);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"保存Service数据失败: {serviceInstance.GetType().Name} - {ex.Message}");
            }
        }
    }
}
