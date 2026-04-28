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
    /// <summary>Service 数据 Inspector 信息 — 用于在 Manager Inspector 中显示。</summary>
    [Serializable]
    public class ServiceDataInspectorInfo
    {
        public string ServiceName;
        public int TotalCategories;
        public int TotalDataCount;
        public List<CategoryInfo> Categories = new();

        [Serializable]
        public class CategoryInfo
        {
            public string CategoryName;
            public int DataCount;
            public List<DataInfo> DataItems = new();
        }

        [Serializable]
        public class DataInfo
        {
            public string Key;
            public string TypeName;
            public string ValueSummary;
        }
    }

    /// <summary>非泛型持久化接口 — 供 DataService 统一调用，无需反射。</summary>
    public interface IServicePersistence
    {
        void SaveAllCategories();
    }

    /// <summary>Service 抽象基类（纯 C# 单例），提供分类数据存储 + JSON 持久化。</summary>
    public abstract class Service<T> : SingletonNormal<T>, IDisposable, IServicePersistence where T : class, new()
    {
        /// <summary>Service 初始化完成事件名（供 DataService 监听，实现自动注册）。</summary>
        public const string EVT_INITIALIZED = "OnServiceInitialized";

        protected Service() => Initialize();

        /// <summary>供 Inspector 显示的数据摘要（不参与序列化）。</summary>
        [NonSerialized] public ServiceDataInspectorInfo InspectorInfo;

        /// <summary>是否启用日志打印。</summary>
        public bool EnableLogging { get; set; } = true;

        #region Data Storage

        /// <summary>按分类存储的数据字典。</summary>
        protected readonly Dictionary<string, Dictionary<string, object>> _dataStorage = new();

        /// <summary>数据存储根路径（默认 <c>{persistentDataPath}/ServiceData/{TypeName}</c>）。</summary>
        protected virtual string DataRootPath => Path.Combine(Application.persistentDataPath, "ServiceData", GetType().Name);

        #endregion

        #region Lifecycle

        protected virtual void Initialize()
        {
            EnsureDataDirectory();
            LoadData();
            LoadLoggingSettings();
            TriggerServiceInitializedEvent();
        }

        public void Dispose()
        {
            SaveLoggingSettings();
            SaveCategoryData("Settings");
            _dataStorage.Clear();
        }

        private void LoadLoggingSettings()
        {
            if (HasData("Settings", "EnableLogging"))
                EnableLogging = GetData<bool>("Settings", "EnableLogging");
            else
                SaveLoggingSettings();
        }

        private void SaveLoggingSettings() => SetData("Settings", "EnableLogging", EnableLogging);

        private void EnsureDataDirectory()
        {
            if (!Directory.Exists(DataRootPath)) Directory.CreateDirectory(DataRootPath);
        }

        /// <summary>触发 Service 初始化事件。</summary>
        private void TriggerServiceInitializedEvent()
        {
            if (!EventProcessor.HasInstance)
            {
                LogWarning($"EventProcessor 未初始化，跳过事件触发: {GetType().Name}");
                return;
            }
            EventProcessor.Instance.TriggerEvent(EVT_INITIALIZED, new List<object> { this });
            Log($"触发Service初始化事件: {GetType().Name}", Color.yellow);
        }

        #endregion

        #region Data Access

        /// <summary>设置数据并立即保存该分类。</summary>
        public void SetData(string category, string key, object value)
        {
            if (!_dataStorage.ContainsKey(category)) _dataStorage[category] = new Dictionary<string, object>();
            _dataStorage[category][key] = value;
            SaveCategoryData(category);
        }

        /// <summary>获取数据（泛型）。类型不匹配时试图转换，失败返回 default。</summary>
        public TData GetData<TData>(string category, string key)
        {
            if (!_dataStorage.TryGetValue(category, out var categoryData) ||
                !categoryData.TryGetValue(key, out var value) || value == null)
                return default;

            if (value is TData typed) return typed;

            try { return (TData)value; }
            catch (InvalidCastException)
            {
                LogWarning($"类型转换失败: 存储 {value.GetType().Name} → 请求 {typeof(TData).Name}, Key: {key}");
                return default;
            }
        }

        /// <summary>获取数据（object）。</summary>
        public object GetData(string category, string key) =>
            _dataStorage.TryGetValue(category, out var c) && c.TryGetValue(key, out var v) ? v : null;

        /// <summary>检查数据是否存在。</summary>
        public bool HasData(string category, string key) =>
            _dataStorage.TryGetValue(category, out var c) && c.ContainsKey(key);

        /// <summary>移除数据，分类空后一并移除。</summary>
        public bool RemoveData(string category, string key)
        {
            if (!_dataStorage.TryGetValue(category, out var categoryData)) return false;
            var removed = categoryData.Remove(key);
            if (categoryData.Count == 0) _dataStorage.Remove(category);
            SaveCategoryData(category);
            return removed;
        }

        /// <summary>获取分类下的所有键。</summary>
        public IEnumerable<string> GetKeys(string category) =>
            _dataStorage.TryGetValue(category, out var c) ? c.Keys : Enumerable.Empty<string>();

        /// <summary>获取所有分类名。</summary>
        public IEnumerable<string> GetCategories() => _dataStorage.Keys;

        /// <summary>获取分类下的全部数据（不存在返回空字典）。</summary>
        public Dictionary<string, object> GetCategoryData(string category) =>
            _dataStorage.TryGetValue(category, out var c) ? c : new Dictionary<string, object>();

        #endregion

        #region Persistence

        /// <summary>从磁盘加载全部分类文件。</summary>
        protected virtual void LoadData()
        {
            EnsureDataDirectory();
            foreach (var file in Directory.GetFiles(DataRootPath, "*.json"))
            {
                try { LoadCategoryFile(file); }
                catch (Exception ex) { LogError($"加载分类数据失败: {file}, {ex.Message}"); }
            }
        }

        private void LoadCategoryFile(string file)
        {
            var parsed = MiniJson.Deserialize(File.ReadAllText(file)) as Dictionary<string, object>;
            var category = Path.GetFileNameWithoutExtension(file);
            _dataStorage[category] = new Dictionary<string, object>();

            if (parsed == null || !parsed.TryGetValue("Items", out var itemsObj) || itemsObj is not List<object> items) return;
            foreach (var item in items)
            {
                if (item is not Dictionary<string, object> dict) continue;
                var key = dict["Key"] as string;
                var type = dict["Type"] as string;
                var value = dict.TryGetValue("Value", out var v) ? v : null;
                _dataStorage[category][key] = DeserializeValue(type, value);
            }
        }

        /// <summary>保存指定分类到磁盘；分类不存在则删除对应文件。</summary>
        protected virtual void SaveCategoryData(string category)
        {
            EnsureDataDirectory();
            var filePath = Path.Combine(DataRootPath, $"{category}.json");

            if (!_dataStorage.TryGetValue(category, out var categoryData))
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                return;
            }

            var wrapper = new CategoryData
            {
                Items = categoryData.Select(kvp => new DataItem
                {
                    Key = kvp.Key,
                    Type = kvp.Value?.GetType().AssemblyQualifiedName ?? "null",
                    Value = SerializeValue(kvp.Value)
                }).ToList()
            };

            File.WriteAllText(filePath, MiniJson.Serialize(wrapper, pretty: true));
        }

        /// <summary>序列化值：复杂类型转为字典，简单类型原样返回。</summary>
        protected virtual object SerializeValue(object value)
        {
            if (value == null) return null;
            var parsed = MiniJson.Deserialize(JsonUtility.ToJson(value)) as Dictionary<string, object>;
            return parsed ?? value;
        }

        /// <summary>反序列化值：根据 typeName 还原原始类型。</summary>
        protected virtual object DeserializeValue(string typeName, object value)
        {
            if (string.IsNullOrEmpty(typeName) || typeName == "null" || value == null) return null;
            try
            {
                var type = Type.GetType(typeName);
                if (type == null) return null;
                return value switch
                {
                    Dictionary<string, object> dict => JsonUtility.FromJson(MiniJson.Serialize(dict, pretty: true), type),
                    string str => JsonUtility.FromJson(str, type),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                LogError($"反序列化失败: {typeName}, {ex.Message}");
                return null;
            }
        }

        /// <summary>保存全部分类 — IServicePersistence 接口实现，供 DataService 调用。</summary>
        public void SaveAllCategories()
        {
            foreach (var category in _dataStorage.Keys) SaveCategoryData(category);
        }

        #endregion

        #region Inspector Info

        /// <summary>重建 <see cref="InspectorInfo" /> 摘要 — 由关联 Manager 每帧调用。</summary>
        public virtual void UpdateInspectorInfo()
        {
            InspectorInfo = new ServiceDataInspectorInfo
            {
                ServiceName = GetType().Name,
                TotalCategories = _dataStorage.Count,
                TotalDataCount = _dataStorage.Values.Sum(d => d.Count),
                Categories = _dataStorage.Select(kvp => new ServiceDataInspectorInfo.CategoryInfo
                {
                    CategoryName = kvp.Key,
                    DataCount = kvp.Value.Count,
                    DataItems = kvp.Value.Select(d => new ServiceDataInspectorInfo.DataInfo
                    {
                        Key = d.Key,
                        TypeName = d.Value?.GetType().Name ?? "null",
                        ValueSummary = TruncateForInspector(d.Value?.ToString())
                    }).ToList()
                }).ToList()
            };
        }

        private static string TruncateForInspector(string s)
        {
            if (s == null) return "null";
            return s.Length > 50 ? s.Substring(0, 50) + "..." : s;
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