using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.Core.Manager;
using EssSystem.Core.Util;
using UnityEngine;

namespace EssSystem.Core.DataManager
{
    public class DataService : Service<DataService>
    {
        private const string DATA_FOLDER = "ServiceData";
        private const string BACKUP_FOLDER = "ServiceDataBackup";
        private string _dataFolder;
        private string _backupFolder;

        /// <summary>
        ///     所有Service实例
        /// </summary>
        private List<object> _serviceInstances;

        protected override void Initialize()
        {
            base.Initialize();
            _dataFolder = Path.Combine(Application.persistentDataPath, DATA_FOLDER);
            _backupFolder = Path.Combine(Application.persistentDataPath, BACKUP_FOLDER);

            // 确保文件夹存在
            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            if (!Directory.Exists(_backupFolder))
                Directory.CreateDirectory(_backupFolder);

            _serviceInstances = new List<object>();

            DiscoverAllServices();
            LoadAllServiceData();

            Log("数据服务初始化完成！", Color.green);
            Application.quitting += OnApplicationQuit;
        }

        private void OnApplicationQuit()
        {
            SaveAllServiceData();
        }

        /// <summary>
        /// 获取 Service 的数据文件路径
        /// </summary>
        private string GetServiceFilePath(string serviceName)
        {
            return Path.Combine(_dataFolder, $"{serviceName}.json");
        }

        /// <summary>
        /// 获取 Service 的备份文件路径
        /// </summary>
        private string GetServiceBackupPath(string serviceName)
        {
            return Path.Combine(_backupFolder, $"{serviceName}.json");
        }

        private void DiscoverAllServices()
        {
            try
            {
                var skippedAssemblies = 0;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (IsSystemAssembly(assembly))
                    {
                        skippedAssemblies++;
                        continue;
                    }

                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var type in types)
                    {
                        if (type == null || !type.IsClass || type.IsAbstract) continue;
                        // 跳过 DataService 自身，否则会把自己也注册进去并递归处理自己的 _dataStorage
                        if (type == typeof(DataService)) continue;

                        var baseType = type.BaseType;
                        while (baseType != null && baseType.IsGenericType)
                        {
                            if (baseType.GetGenericTypeDefinition() == typeof(Service<>))
                            {
                                var getInstanceMethod = baseType
                                    .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetGetMethod();
                                if (getInstanceMethod != null)
                                {
                                    var instance = getInstanceMethod.Invoke(null, null);
                                    if (instance != null && !_serviceInstances.Contains(instance))
                                    {
                                        _serviceInstances.Add(instance);
                                        Log($"发现Service: {type.Name}", Color.cyan);
                                    }
                                }

                                break;
                            }

                            baseType = baseType.BaseType;
                        }
                    }
                }

                Log($"发现了{_serviceInstances.Count}个Service实例（跳过系统程序集 {skippedAssemblies} 个）", Color.green);
            }
            catch (Exception ex)
            {
                LogError($"发现Service失败: {ex.Message}");
            }
        }

        /// <summary>
        ///     判断是否为系统/引擎程序集（不包含用户代码） — 与 EventProcessor 保持一致
        /// </summary>
        private static bool IsSystemAssembly(Assembly asm)
        {
            var name = asm.GetName().Name;
            if (string.IsNullOrEmpty(name)) return true;
            return name.StartsWith("System.", StringComparison.Ordinal)
                   || name.StartsWith("Microsoft.", StringComparison.Ordinal)
                   || name.StartsWith("Unity.", StringComparison.Ordinal)
                   || name.StartsWith("UnityEngine", StringComparison.Ordinal)
                   || name.StartsWith("UnityEditor", StringComparison.Ordinal)
                   || name.StartsWith("Mono.", StringComparison.Ordinal)
                   || name.StartsWith("nunit.", StringComparison.Ordinal)
                   || name == "mscorlib"
                   || name == "netstandard"
                   || name == "System";
        }

        private void LoadAllServiceData()
        {
            try
            {
                if (!Directory.Exists(_dataFolder))
                {
                    Log("未找到数据文件夹，重新开始", Color.blue);
                    return;
                }

                foreach (var serviceInstance in _serviceInstances)
                    try
                    {
                        var serviceType = serviceInstance.GetType();
                        var serviceName = serviceType.Name;
                        var serviceFilePath = GetServiceFilePath(serviceName);

                        if (!File.Exists(serviceFilePath))
                        {
                            Log($"Service {serviceName} 数据文件不存在，跳过", Color.yellow);
                            continue;
                        }

                        var jsonData = File.ReadAllText(serviceFilePath);
                        var serviceData = ConvertFromHighlyReadableFormat(jsonData);

                        var dataStorageField = serviceType.GetField("_dataStorage",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataStorageField != null)
                        {
                            var dataStorage =
                                dataStorageField.GetValue(serviceInstance) as
                                    Dictionary<string, Dictionary<string, object>>;
                            if (dataStorage != null && serviceData.Count > 0)
                            {
                                var serviceDict = serviceData[0] as Dictionary<string, object>;
                                if (serviceDict != null && serviceDict.ContainsKey("categories"))
                                {
                                    var categories = serviceDict["categories"] as Dictionary<string, object>;
                                    if (categories != null)
                                        foreach (var kvp in categories)
                                            if (kvp.Value is Dictionary<string, object> categoryData)
                                                dataStorage[kvp.Key] = categoryData;

                                    Log($"已为Service加载数据: {serviceName}", Color.green);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"为Service加载数据失败: {serviceInstance.GetType().Name} - {ex.Message}");
                    }
            }
            catch (Exception ex)
            {
                LogError($"加载所有Service数据失败: {ex.Message}");
            }
        }

        private void SaveAllServiceData()
        {
            try
            {
                var savedCount = 0;

                foreach (var serviceInstance in _serviceInstances)
                    try
                    {
                        var serviceType = serviceInstance.GetType();
                        var serviceName = serviceType.Name;

                        var dataStorageField = serviceType.GetField("_dataStorage",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataStorageField != null)
                        {
                            var dataStorage =
                                dataStorageField.GetValue(serviceInstance) as
                                    Dictionary<string, Dictionary<string, object>>;
                            if (dataStorage != null && dataStorage.Count > 0)
                            {
                                var serviceData = new Dictionary<string, object>
                                {
                                    ["service_name"] = serviceName,
                                    ["categories"] = dataStorage
                                };

                                var serviceFilePath = GetServiceFilePath(serviceName);
                                SaveServiceDataToFile(serviceFilePath, new List<object> { serviceData });
                                savedCount++;
                                Log($"已保存Service数据: {serviceName} ({dataStorage.Count}个分类)", Color.blue);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"保存Service数据失败: {serviceInstance.GetType().Name} - {ex.Message}");
                    }

                if (savedCount > 0)
                {
                    Log($"已为{savedCount}个Service保存数据", Color.green);
                }
                else
                {
                    Log("没有Service数据需要保存", Color.yellow);
                }
            }
            catch (Exception ex)
            {
                LogError($"保存所有Service数据失败: {ex.Message}");
            }
        }

        private void SaveServiceDataToFile(string filePath, List<object> data)
        {
            try
            {
                var jsonData = ConvertToHighlyReadableFormat(data);
                File.WriteAllText(filePath, jsonData);
                Log($"数据已保存到: {filePath}", Color.green);
            }
            catch (Exception ex)
            {
                LogError($"保存数据失败: {ex.Message}");
            }
        }

        [Event("SaveServiceCategory")]
        public List<object> SaveServiceCategory(List<object> data)
        {
            try
            {
                if (data.Count < 3)
                {
                    LogWarning("保存Service分类需要Service名称、分类名称和数据");
                    return new List<object> { "参数无效" };
                }

                var serviceName = data[0] as string;
                var categoryName = data[1] as string;
                var categoryData = data[2] as Dictionary<string, object>;

                if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(categoryName) || categoryData == null)
                {
                    LogWarning("Service分类数据格式无效");
                    return new List<object> { "格式无效" };
                }

                if (!IsDataSerializable(categoryData)) LogWarning($"分类 '{categoryName}' 包含不可序列化对象");

                // 查找目标Service
                var targetService = _serviceInstances.FirstOrDefault(s => s.GetType().Name == serviceName);
                if (targetService == null)
                {
                    LogWarning($"未找到Service '{serviceName}'");
                    return new List<object> { "未找到Service" };
                }

                // 获取Service数据存储
                var dataStorageField = targetService.GetType()
                    .GetField("_dataStorage", BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataStorageField != null)
                {
                    var dataStorage =
                        dataStorageField.GetValue(targetService) as Dictionary<string, Dictionary<string, object>>;
                    if (dataStorage != null)
                    {
                        if (!dataStorage.ContainsKey(categoryName))
                            dataStorage[categoryName] = new Dictionary<string, object>();

                        foreach (var kvp in categoryData) dataStorage[categoryName][kvp.Key] = kvp.Value;

                        // 保存当前 Service 数据到单独文件
                        var serviceData = new Dictionary<string, object>
                        {
                            ["service_name"] = serviceName,
                            ["categories"] = dataStorage
                        };
                        var serviceFilePath = GetServiceFilePath(serviceName);
                        CreateServiceBackup(serviceName);
                        SaveServiceDataToFile(serviceFilePath, new List<object> { serviceData });
                        return new List<object> { "成功", serviceFilePath };
                    }
                }

                return new List<object> { "保存失败" };
            }
            catch (Exception ex)
            {
                LogError($"保存Service分类失败: {ex.Message}");
                return new List<object> { "保存失败", ex.Message };
            }
        }

        [Event("GetServiceDataById")]
        public List<object> GetServiceDataById(List<object> data)
        {
            try
            {
                if (data.Count < 3)
                {
                    LogWarning("获取Service数据需要Service名称、分类名称和数据ID");
                    return new List<object> { "参数无效" };
                }

                var serviceName = data[0] as string;
                var categoryName = data[1] as string;
                var dataId = data[2] as string;

                if (string.IsNullOrEmpty(serviceName) || string.IsNullOrEmpty(categoryName) ||
                    string.IsNullOrEmpty(dataId))
                {
                    LogWarning("Service数据参数格式无效");
                    return new List<object> { "格式无效" };
                }

                // 查找目标Service
                var targetService = _serviceInstances.FirstOrDefault(s => s.GetType().Name == serviceName);
                if (targetService == null)
                {
                    LogWarning($"未找到Service '{serviceName}'");
                    return new List<object> { "未找到Service" };
                }

                // 获取Service数据存储
                var dataStorageField = targetService.GetType()
                    .GetField("_dataStorage", BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataStorageField != null)
                {
                    var dataStorage =
                        dataStorageField.GetValue(targetService) as Dictionary<string, Dictionary<string, object>>;
                    if (dataStorage != null && dataStorage.ContainsKey(categoryName))
                    {
                        var category = dataStorage[categoryName];
                        if (category.ContainsKey(dataId))
                        {
                            var result = category[dataId];
                            Log($"成功获取Service '{serviceName}' 分类 '{categoryName}' 数据 '{dataId}'");
                            return new List<object> { "成功", result };
                        }

                        LogWarning($"分类 '{categoryName}' 中未找到数据 '{dataId}'");
                        return new List<object> { "数据不存在" };
                    }

                    LogWarning($"Service '{serviceName}' 中未找到分类 '{categoryName}'");
                    return new List<object> { "分类不存在" };
                }

                LogWarning($"无法访问Service '{serviceName}' 的数据存储");
                return new List<object> { "访问失败" };
            }
            catch (Exception ex)
            {
                LogError($"获取Service数据失败: {ex.Message}");
                return new List<object> { "获取失败", ex.Message };
            }
        }

        private bool IsDataSerializable(object data)
        {
            try
            {
                if (data == null) return true;

                //  check for [Serializable] attribute
                var hasSerializableAttribute = data.GetType().IsDefined(typeof(SerializableAttribute), false);

                if (!hasSerializableAttribute && !data.GetType().IsPrimitive && !(data is string) &&
                    !(data is DateTime)) LogWarning($"'{data.GetType().Name}' 缺乏 [Serializable] 属性");

                return hasSerializableAttribute || data.GetType().IsPrimitive || data is string || data is DateTime;
            }
            catch
            {
                return false;
            }
        }

        // 使用 MiniJson 序列化 Dictionary<string, object> + List<object>（JsonUtility 不支持）
        private string ConvertToHighlyReadableFormat(List<object> data)
        {
            var formattedData = new Dictionary<string, object>
            {
                ["metadata"] = new Dictionary<string, object>
                {
                    ["version"] = "1.0",
                    ["created_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["data_count"] = data.Count
                },
                ["data"] = data
            };
            return MiniJson.Serialize(formattedData, pretty: true);
        }

        private List<object> ConvertFromHighlyReadableFormat(string jsonData)
        {
            try
            {
                var parsed = MiniJson.Deserialize(jsonData) as Dictionary<string, object>;
                if (parsed == null)
                {
                    LogWarning("存档反序列化返回 null — 可能是文件损坏");
                    return new List<object>();
                }

                return parsed.TryGetValue("data", out var d)
                    ? d as List<object> ?? new List<object>()
                    : new List<object>();
            }
            catch (Exception ex)
            {
                LogError($"解析JSON数据失败: {ex.Message}");
                return new List<object>();
            }
        }

        private void CreateServiceBackup(string serviceName)
        {
            try
            {
                var serviceFilePath = GetServiceFilePath(serviceName);
                var backupPath = GetServiceBackupPath(serviceName);

                if (File.Exists(serviceFilePath))
                {
                    File.Copy(serviceFilePath, backupPath, true);
                    Log($"Service {serviceName} 备份已创建", Color.yellow);
                }
            }
            catch (Exception ex)
            {
                LogWarning($"创建Service备份失败: {ex.Message}");
            }
        }

        private void RestoreServiceFromBackup(string serviceName)
        {
            try
            {
                var serviceFilePath = GetServiceFilePath(serviceName);
                var backupPath = GetServiceBackupPath(serviceName);

                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, serviceFilePath, true);
                    Log($"Service {serviceName} 数据已从备份恢复", Color.yellow);
                }
                else
                {
                    LogWarning($"未找到Service {serviceName} 备份文件");
                }
            }
            catch (Exception ex)
            {
                LogError($"从Service备份恢复失败: {ex.Message}");
            }
        }

        [Event("SaveData")]
        public List<object> SaveDataToLocal(List<object> data)
        {
            try
            {
                LogWarning("SaveData 事件已废弃，请使用 SaveServiceCategory 保存单个 Service 数据");
                return new List<object> { "方法已废弃" };
            }
            catch (Exception ex)
            {
                LogError($"保存数据失败: {ex.Message}");
                return new List<object> { "保存失败", ex.Message };
            }
        }
    }
}