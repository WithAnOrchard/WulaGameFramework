using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EssSystem.Core.Singleton;
using EssSystem.Core.Event;
using EssSystem.Core.Util;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Manager
{
    /// <summary>
    ///     Service 数据 Inspector 信息 - 用于在 Manager Inspector 中显示 Service 数据
    /// </summary>
    [Serializable]
    public class ServiceDataInspectorInfo
    {
        public string ServiceName;
        public int TotalCategories;
        public int TotalDataCount;
        public List<CategoryInfo> Categories;

        public ServiceDataInspectorInfo()
        {
            Categories = new List<CategoryInfo>();
        }

        [Serializable]
        public class CategoryInfo
        {
            public string CategoryName;
            public int DataCount;
            public List<DataInfo> DataItems;

            public CategoryInfo()
            {
                DataItems = new List<DataInfo>();
            }
        }

        [Serializable]
        public class DataInfo
        {
            public string Key;
            public string TypeName;
            public string ValueSummary;

            public DataInfo()
            {
            }

            public DataInfo(string key, object value)
            {
                Key = key;
                TypeName = value?.GetType().Name ?? "null";
                ValueSummary = GetValueSummary(value);
            }

            private static string GetValueSummary(object value)
            {
                if (value == null) return "null";

                // 限制摘要长度
                string str = value.ToString();
                if (str.Length > 50) str = str.Substring(0, 47) + "...";

                return str;
            }
        }
    }

    /// <summary>
    ///     Service抽象类，继承自Singleton，提供数据存储功能
    /// </summary>
    /// <typeparam name="T">Service类型</typeparam>
    public abstract class Service<T> : SingletonNormal<T>, IDisposable where T : class, new()
    {
        /// <summary>
        ///     构造函数，自动调用初始化
        /// </summary>
        protected Service()
        {
            Initialize();
        }

        /// <summary>
        ///     用于 Inspector 显示的数据摘要（只读，不参与序列化）
        /// </summary>
        [System.NonSerialized]
        public ServiceDataInspectorInfo InspectorInfo;

        /// <summary>
        ///     是否启用日志打印
        /// </summary>
        private bool _enableLogging = true;

        /// <summary>
        ///     是否启用日志打印（公共属性）
        /// </summary>
        public bool EnableLogging
        {
            get => _enableLogging;
            set => _enableLogging = value;
        }

        /// <summary>
        ///     日志输出方法（受EnableLogging控制）
        /// </summary>
        protected override void Log(string message, Color color = default)
        {
            // if (!_enableLogging) return;
            base.Log(message, color);
        }

        #region Data Storage

        /// <summary>
        ///     数据存储字典 - 按分类存储数据
        /// </summary>
        protected readonly Dictionary<string, Dictionary<string, object>> _dataStorage = new Dictionary<string, Dictionary<string, object>>();

        /// <summary>
        ///     数据存储根路径（Service文件夹）
        /// </summary>
        protected virtual string DataRootPath => Path.Combine(Application.persistentDataPath, "ServiceData", GetType().Name);

        #endregion

        #region Lifecycle

        /// <summary>
        ///     初始化 Service
        /// </summary>
        protected virtual void Initialize()
        {
            EnsureDataDirectory();
            LoadData();
            LoadLoggingSettings();
            TriggerServiceInitializedEvent();
        }

        /// <summary>
        ///     清理 Service
        /// </summary>
        public void Dispose()
        {
            SaveLoggingSettings();
            SaveCategoryData("Settings");
            _dataStorage.Clear();
        }

        /// <summary>
        ///     加载日志设置
        /// </summary>
        private void LoadLoggingSettings()
        {
            if (HasData("Settings", "EnableLogging"))
            {
                var settings = GetData<bool>("Settings", "EnableLogging");
                _enableLogging = settings;
            }
            else
            {
                _enableLogging = true;
                SaveLoggingSettings();
            }
        }

        /// <summary>
        ///     保存日志设置
        /// </summary>
        private void SaveLoggingSettings()
        {
            SetData("Settings", "EnableLogging", _enableLogging);
        }

        /// <summary>
        ///     确保数据目录存在
        /// </summary>
        private void EnsureDataDirectory()
        {
            if (!Directory.Exists(DataRootPath))
            {
                Directory.CreateDirectory(DataRootPath);
            }
        }

        /// <summary>
        ///     触发 Service 初始化完成事件
        /// </summary>
        private void TriggerServiceInitializedEvent()
        {
            // 通过 EventManager 触发 Service 初始化事件
            if (EventManager.HasInstance)
            {
                var eventData = EventDataPool.Rent();
                eventData.Add(this);
                EventManager.Instance.TriggerEvent("OnServiceInitialized", eventData);
                EventDataPool.Return(eventData);
                Log($"触发Service初始化事件: {GetType().Name}", Color.yellow);
            }
            else
            {
                LogWarning($"EventManager未初始化，无法触发Service初始化事件: {GetType().Name}");
            }
        }

        #endregion

        #region Data Access

        /// <summary>
        ///     设置数据
        /// </summary>
        public void SetData(string category, string key, object value)
        {
            if (!_dataStorage.ContainsKey(category))
            {
                _dataStorage[category] = new Dictionary<string, object>();
            }

            _dataStorage[category][key] = value;
            SaveCategoryData(category);
        }

        /// <summary>
        ///     获取数据
        /// </summary>
        public TData GetData<TData>(string category, string key)
        {
            if (_dataStorage.TryGetValue(category, out var categoryData) &&
                categoryData.TryGetValue(key, out var value))
            {
                if (value == null)
                {
                    return default;
                }

                // 检查类型是否匹配
                if (value is TData typedValue)
                {
                    return typedValue;
                }

                // 尝试安全转换
                try
                {
                    return (TData)value;
                }
                catch (InvalidCastException)
                {
                    LogWarning($"类型转换失败: 存储类型 {value.GetType().Name}, 请求类型 {typeof(TData).Name}, Key: {key}");
                    return default;
                }
            }
            return default;
        }

        /// <summary>
        ///     获取数据（泛型）
        /// </summary>
        public object GetData(string category, string key)
        {
            if (_dataStorage.TryGetValue(category, out var categoryData) &&
                categoryData.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        ///     检查数据是否存在
        /// </summary>
        public bool HasData(string category, string key)
        {
            return _dataStorage.TryGetValue(category, out var categoryData) &&
                   categoryData.ContainsKey(key);
        }

        /// <summary>
        ///     移除数据
        /// </summary>
        public bool RemoveData(string category, string key)
        {
            if (_dataStorage.TryGetValue(category, out var categoryData))
            {
                var removed = categoryData.Remove(key);
                if (categoryData.Count == 0)
                {
                    _dataStorage.Remove(category);
                }
                SaveCategoryData(category);
                return removed;
            }
            return false;
        }

        /// <summary>
        ///     获取分类下的所有键
        /// </summary>
        public IEnumerable<string> GetKeys(string category)
        {
            if (_dataStorage.TryGetValue(category, out var categoryData))
            {
                return categoryData.Keys;
            }
            return Enumerable.Empty<string>();
        }

        /// <summary>
        ///     获取所有分类
        /// </summary>
        public IEnumerable<string> GetCategories()
        {
            return _dataStorage.Keys;
        }

        /// <summary>
        ///     获取分类下的所有数据
        /// </summary>
        public Dictionary<string, object> GetCategoryData(string category)
        {
            if (_dataStorage.TryGetValue(category, out var categoryData))
            {
                return categoryData;
            }
            return new Dictionary<string, object>();
        }

        #endregion

        #region Persistence

        /// <summary>
        ///     加载数据
        /// </summary>
        protected virtual void LoadData()
        {
            EnsureDataDirectory();

            // 加载每个分类的数据文件
            var categoryFiles = Directory.GetFiles(DataRootPath, "*.json");
            foreach (var file in categoryFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var parsed = MiniJson.Deserialize(json) as Dictionary<string, object>;
                    var categoryName = Path.GetFileNameWithoutExtension(file);

                    _dataStorage[categoryName] = new Dictionary<string, object>();

                    if (parsed != null && parsed.ContainsKey("Items"))
                    {
                        var items = parsed["Items"] as List<object>;
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                if (item is Dictionary<string, object> itemDict)
                                {
                                    var key = itemDict["Key"] as string;
                                    var type = itemDict["Type"] as string;
                                    var value = itemDict.ContainsKey("Value") ? itemDict["Value"] : null;
                                    _dataStorage[categoryName][key] = DeserializeValue(type, value);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError($"加载分类数据失败: {file}, 错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        ///     保存分类数据
        /// </summary>
        protected virtual void SaveCategoryData(string category)
        {
            EnsureDataDirectory();

            if (!_dataStorage.TryGetValue(category, out var categoryData))
            {
                // 删除分类文件
                var filePath = Path.Combine(DataRootPath, $"{category}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                return;
            }

            var categoryDataWrapper = new CategoryData
            {
                Items = new List<DataItem>()
            };

            foreach (var kvp in categoryData)
            {
                categoryDataWrapper.Items.Add(new DataItem
                {
                    Key = kvp.Key,
                    Type = kvp.Value?.GetType().AssemblyQualifiedName ?? "null",
                    Value = SerializeValue(kvp.Value)
                });
            }

            // 使用MiniJson序列化以支持嵌套对象
            var json = MiniJson.Serialize(categoryDataWrapper, pretty: true);
            var categoryFilePath = Path.Combine(DataRootPath, $"{category}.json");
            File.WriteAllText(categoryFilePath, json);
        }

        /// <summary>
        ///     序列化值（返回对象以便JSON格式化）
        /// </summary>
        protected virtual object SerializeValue(object value)
        {
            if (value == null) return null;

            // 对于简单类型直接返回，对于复杂类型尝试序列化为字典
            var json = JsonUtility.ToJson(value);
            var parsed = MiniJson.Deserialize(json) as Dictionary<string, object>;
            return parsed ?? value;
        }

        /// <summary>
        ///     反序列化值
        /// </summary>
        protected virtual object DeserializeValue(string typeName, object value)
        {
            if (string.IsNullOrEmpty(typeName) || typeName == "null" || value == null) return null;

            try
            {
                var type = Type.GetType(typeName);
                if (type != null)
                {
                    // 如果value是字典，先序列化为JSON再反序列化
                    if (value is Dictionary<string, object> dict)
                    {
                        var json = MiniJson.Serialize(dict, pretty: true);
                        return JsonUtility.FromJson(json, type);
                    }
                    // 如果value已经是字符串，直接反序列化
                    else if (value is string str)
                    {
                        return JsonUtility.FromJson(str, type);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"反序列化失败: {typeName}, 错误: {ex.Message}");
            }

            return null;
        }

        #endregion

        #region Inspector Info

        /// <summary>
        ///     更新 Inspector 信息
        /// </summary>
        public virtual void UpdateInspectorInfo()
        {
            InspectorInfo = new ServiceDataInspectorInfo
            {
                ServiceName = GetType().Name,
                TotalCategories = _dataStorage.Count,
                TotalDataCount = _dataStorage.Values.Sum(d => d.Count),
                Categories = new List<ServiceDataInspectorInfo.CategoryInfo>()
            };

            foreach (var kvp in _dataStorage)
            {
                var categoryInfo = new ServiceDataInspectorInfo.CategoryInfo
                {
                    CategoryName = kvp.Key,
                    DataCount = kvp.Value.Count,
                    DataItems = new List<ServiceDataInspectorInfo.DataInfo>()
                };

                foreach (var dataKvp in kvp.Value)
                {
                    var value = dataKvp.Value;
                    string valueSummary = value?.ToString() ?? "null";
                    if (valueSummary.Length > 50)
                    {
                        valueSummary = valueSummary.Substring(0, 50) + "...";
                    }

                    categoryInfo.DataItems.Add(new ServiceDataInspectorInfo.DataInfo
                    {
                        Key = dataKvp.Key,
                        TypeName = value?.GetType().Name ?? "null",
                        ValueSummary = valueSummary
                    });
                }

                InspectorInfo.Categories.Add(categoryInfo);
            }
        }

        #endregion

        #region Serialization Helpers

        [Serializable]
        private class CategoryData
        {
            public List<DataItem> Items = new List<DataItem>();
        }

        [Serializable]
        private class DataItem
        {
            public string Key;
            public string Type;
            public object Value;
        }

        #endregion
    }
}