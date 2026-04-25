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
            base.Initialize();
            // 设置数据文件夹路径
            _dataFolder = Path.Combine(Application.persistentDataPath, DATA_FOLDER);

            // 确保数据文件夹存在
            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            // 初始化 Service 实例列表
            _serviceInstances = new List<object>();

            // 注册 Service 初始化事件监听器
            if (EventManager.HasInstance)
            {
                EventManager.Instance.AddListener("OnServiceInitialized", OnServiceInitialized);
            }

            // DataService 自己注册自己
            _serviceInstances.Add(this);

            // 加载所有已保存的 Service 数据
            LoadAllServiceData();

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
        /// 获取指定 Service 的数据文件路径
        /// </summary>
        /// <param name="serviceName">Service 名称</param>
        /// <returns>数据文件完整路径</returns>
        private string GetServiceFilePath(string serviceName)
        {
            return Path.Combine(_dataFolder, $"{serviceName}.json");
        }

        /// <summary>
        /// Service 初始化事件处理器
        /// 当有新 Service 初始化时，自动注册并加载其数据
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据，包含 Service 实例</param>
        /// <returns>空列表</returns>
        public List<object> OnServiceInitialized(string eventName, List<object> data)
        {
            if (data.Count > 0 && data[0] != null)
            {
                var serviceInstance = data[0];
                // 如果 Service 尚未注册，则注册并加载数据
                if (!_serviceInstances.Contains(serviceInstance))
                {
                    _serviceInstances.Add(serviceInstance);
                    LoadServiceData(serviceInstance);
                }
            }
            return new List<object>();
        }

        /// <summary>
        /// 加载所有已注册 Service 的数据
        /// </summary>
        private void LoadAllServiceData()
        {
            if (!Directory.Exists(_dataFolder)) return;

            foreach (var serviceInstance in _serviceInstances)
            {
                LoadServiceData(serviceInstance);
            }
        }

        /// <summary>
        /// 加载指定 Service 的数据
        /// </summary>
        /// <param name="serviceInstance">Service 实例</param>
        private void LoadServiceData(object serviceInstance)
        {
            try
            {
                var serviceType = serviceInstance.GetType();
                var serviceFilePath = GetServiceFilePath(serviceType.Name);

                // 如果数据文件不存在，跳过
                if (!File.Exists(serviceFilePath)) return;

                // 读取 JSON 数据
                var jsonData = File.ReadAllText(serviceFilePath);

                // 通过反射获取 Service 的 _dataStorage 字段
                var dataStorageField = serviceType.GetField("_dataStorage",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (dataStorageField != null)
                {
                    var dataStorage = dataStorageField.GetValue(serviceInstance) as
                        Dictionary<string, Dictionary<string, object>>;
                    var parsed = MiniJson.Deserialize(jsonData) as Dictionary<string, object>;

                    // 解析 JSON 并填充到 dataStorage
                    if (dataStorage != null && parsed != null && parsed.ContainsKey("categories"))
                    {
                        var categories = parsed["categories"] as Dictionary<string, object>;
                        if (categories != null)
                        {
                            foreach (var kvp in categories)
                            {
                                if (kvp.Value is Dictionary<string, object> categoryData)
                                {
                                    dataStorage[kvp.Key] = categoryData;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"加载Service数据失败: {serviceInstance.GetType().Name} - {ex.Message}");
            }
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

                    // 构建保存的数据结构
                    var serviceData = new Dictionary<string, object>
                    {
                        ["categories"] = dataStorage ?? new Dictionary<string, Dictionary<string, object>>()
                    };

                    // 序列化为 JSON 并保存到文件
                    var jsonData = MiniJson.Serialize(serviceData, pretty: true);
                    File.WriteAllText(GetServiceFilePath(serviceType.Name), jsonData);
                }
            }
            catch (Exception ex)
            {
                LogWarning($"保存Service数据失败: {serviceInstance.GetType().Name} - {ex.Message}");
            }
        }
    }
}
