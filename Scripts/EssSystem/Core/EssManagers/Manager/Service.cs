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

        // M4: Inspector 脉冲 — 仅在数据变动后重建 InspectorInfo，避免每 0.25s LINQ + new 量产 GC。
        [NonSerialized] private bool _inspectorDirty = true;

        /// <summary>I4: 子类直接 mutate _dataStorage（如 ReloadData / Clear）后调用，让下次 Inspector 重建。
        /// SetData/RemoveData 内部已自动标 dirty，只在绕过这两个 API 时才需手动调。</summary>
        protected void MarkInspectorDirty() => _inspectorDirty = true;

        // M6: 批量写盘 — BeginBatch() 期间 SetData/RemoveData 仅标 dirty，Dispose 时一次性 flush。
        private int _batchDepth = 0;
        private readonly HashSet<string> _pendingDirtyCategories = new();

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
            FlushPendingWrites();   // M6: 退出前 确保 batch 中未写盘的都落盘
            SaveLoggingSettings();
            SaveCategoryData("Settings");
            _dataStorage.Clear();
        }

        // ============================================================
        // M6: 批量写盘 API
        // ============================================================

        /// <summary>进入批量写盘作用域。返回的 IDisposable Dispose 时 flush。支持嵌套。
        /// <para>用法：<code>using (svc.BeginBatch()) { svc.SetData(...); svc.SetData(...); } // 一次性写盘</code></para></summary>
        public IDisposable BeginBatch() => new BatchScope(this);

        /// <summary>手动 flush 累积的 dirty categories（batch 期间或调过之后）。</summary>
        public void FlushPendingWrites()
        {
            if (_pendingDirtyCategories.Count == 0) return;
            foreach (var category in _pendingDirtyCategories) SaveCategoryData(category);
            _pendingDirtyCategories.Clear();
        }

        /// <summary>SetData/RemoveData 后调。根据是否处于 batch 作用域路由到立即写盘或延后。
        /// <para>C2: transient category 跳过写盘路径 仅标 inspector dirty —— 让子类可以放心用 SetData 写运行时只读数据（如 GameObject 引用）不会意外被序列化。</para></summary>
        private void OnCategoryDataChanged(string category)
        {
            _inspectorDirty = true;
            if (IsTransientCategory(category)) return;   // C2
            if (_batchDepth > 0)
                _pendingDirtyCategories.Add(category);
            else
                SaveCategoryData(category);
        }

        private sealed class BatchScope : IDisposable
        {
            private Service<T> _svc;
            public BatchScope(Service<T> svc) { _svc = svc; svc._batchDepth++; }
            public void Dispose()
            {
                if (_svc == null) return;
                _svc._batchDepth--;
                if (_svc._batchDepth == 0) _svc.FlushPendingWrites();
                _svc = null;
            }
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

        /// <summary>触发 Service 初始化事件。
        /// <para>EventProcessor 不可用时静默跳过 —— 早期 Editor OnValidate / RuntimeInitializeOnLoad
        /// 期可能在 EventProcessor 创建之前就触发 Service 单例，属正常时序，不打 warning。</para></summary>
        private void TriggerServiceInitializedEvent()
        {
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEvent(EVT_INITIALIZED, new List<object> { this });
            Log($"触发Service初始化事件: {GetType().Name}", Color.yellow);
        }

        #endregion

        #region Data Access

        /// <summary>设置数据。默认立即写盘；batch 作用域内仅标 dirty，退出 batch 后一次性 flush。</summary>
        public void SetData(string category, string key, object value)
        {
            if (!_dataStorage.ContainsKey(category)) _dataStorage[category] = new Dictionary<string, object>();
            _dataStorage[category][key] = value;
            OnCategoryDataChanged(category);
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

        /// <summary>移除数据，分类空后一并移除。仅在真删时才写盘。</summary>
        public bool RemoveData(string category, string key)
        {
            if (!_dataStorage.TryGetValue(category, out var categoryData)) return false;
            var removed = categoryData.Remove(key);
            if (!removed) return false;                                  // M2: 没真删就别写盘
            if (categoryData.Count == 0) _dataStorage.Remove(category);
            OnCategoryDataChanged(category);
            return true;
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
            _inspectorDirty = true;   // M4: 加载文件后下次 Inspector 重建
        }

        /// <summary>保存指定分类到磁盘；分类不存在则删除对应文件。
        /// <para>C2: transient category 静默跳过写盘（防御子类直接调用该方法）。</para></summary>
        protected virtual void SaveCategoryData(string category)
        {
            if (IsTransientCategory(category)) return;   // C2
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

        /// <summary>序列化值：直接存 JsonUtility 产出的 JSON 字符串；string / 简单类型原样返回。
        /// <para>M3: 不再走 MiniJson 把 JSON 反解成 Dict 中转，DeserializeValue 的 string 分支可直接还原。</para></summary>
        protected virtual object SerializeValue(object value)
        {
            if (value == null) return null;
            // 简单类型 / 字符串原样存，避免 JsonUtility 把 "abc" 包成 {"value":"abc"} 或对 int 抛异常
            if (value is string || value.GetType().IsPrimitive || value is decimal) return value;
            return JsonUtility.ToJson(value);
        }

        /// <summary>反序列化值：根据 typeName 还原原始类型。
        /// <para>M1: 通过 <see cref="LegacyTypeResolver"/> 查表，类被重命名/搬迁也能命中（前提是新类挂 [FormerName]）。</para>
        /// <para>支持两种 Value 形式：JSON 字符串（新版 SerializeValue 输出）/ Dictionary（兼容老存档）。</para></summary>
        protected virtual object DeserializeValue(string typeName, object value)
        {
            if (string.IsNullOrEmpty(typeName) || typeName == "null" || value == null) return null;
            try
            {
                var type = LegacyTypeResolver.Resolve(typeName);
                if (type == null)
                {
                    LogWarning($"未找到类型: {typeName} —— 如曾搬迁/重命名，请在新类上加 [FormerName(\"{typeName.Split(',')[0].Trim()}\")]");
                    return null;
                }
                return value switch
                {
                    string str => JsonUtility.FromJson(str, type),
                    Dictionary<string, object> dict => JsonUtility.FromJson(MiniJson.Serialize(dict, pretty: true), type),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                LogError($"反序列化失败: {typeName}, {ex.Message}");
                return null;
            }
        }

        /// <summary>保存全部分类 — IServicePersistence 接口实现，供 DataService 调用。
        /// <para>子类可重写 <see cref="IsTransientCategory"/> 来排除"仅运行时"分类（如 EntityService 的实例字典），
        /// 避免它们被序列化到磁盘后污染下次 Play 启动状态。</para></summary>
        public virtual void SaveAllCategories()
        {
            _pendingDirtyCategories.Clear();   // M6: 全量保存会覆盖 batch dirty，清揉免得重复写
            foreach (var category in _dataStorage.Keys)
            {
                if (IsTransientCategory(category)) continue;
                SaveCategoryData(category);
            }
        }

        /// <summary>子类返回 true 的分类不会被 <see cref="SaveAllCategories"/> 持久化。</summary>
        protected virtual bool IsTransientCategory(string category) => false;

        #endregion

        #region Inspector Info

        /// <summary>重建 <see cref="InspectorInfo" /> 摘要 — 由关联 Manager 每帧调用。
        /// <para>M4: 除非数据变动过（_inspectorDirty=true），否则直接返回，避免重复重建。</para></summary>
        public virtual void UpdateInspectorInfo()
        {
            if (!_inspectorDirty && InspectorInfo != null) return;

            InspectorInfo ??= new ServiceDataInspectorInfo();
            InspectorInfo.ServiceName = GetType().Name;
            InspectorInfo.TotalCategories = _dataStorage.Count;

            var cats = InspectorInfo.Categories;
            cats.Clear();
            var totalData = 0;
            foreach (var kvp in _dataStorage)
            {
                var dataItems = new List<ServiceDataInspectorInfo.DataInfo>(kvp.Value.Count);
                foreach (var d in kvp.Value)
                {
                    dataItems.Add(new ServiceDataInspectorInfo.DataInfo
                    {
                        Key = d.Key,
                        TypeName = d.Value?.GetType().Name ?? "null",
                        ValueSummary = TruncateForInspector(d.Value?.ToString())
                    });
                }
                cats.Add(new ServiceDataInspectorInfo.CategoryInfo
                {
                    CategoryName = kvp.Key,
                    DataCount = kvp.Value.Count,
                    DataItems = dataItems
                });
                totalData += kvp.Value.Count;
            }
            InspectorInfo.TotalDataCount = totalData;
            _inspectorDirty = false;
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