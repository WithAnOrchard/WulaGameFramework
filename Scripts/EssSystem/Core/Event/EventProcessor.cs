using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Singleton;
using EssSystem.Core.Util;
using UnityEngine;

namespace EssSystem.Core.Event
{
    /// <summary>
    ///     事件委托 - 事件监听器的委托类型
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="data">事件数据</param>
    /// <returns>事件结果</returns>
    public delegate List<object> EventDelegate(string eventName, List<object> data);

    /// <summary>
    ///     事件数据对象池（性能优化）
    /// </summary>
    public static class EventDataPool
    {
        private const int MAX_POOL_SIZE = 50;
        private static readonly Stack<List<object>> _pool = new();

        public static List<object> Rent()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    var list = _pool.Pop();
                    list.Clear();
                    return list;
                }
            }

            return new List<object>();
        }

        public static void Return(List<object> list)
        {
            if (list == null) return;
            lock (_pool)
            {
                if (_pool.Count < MAX_POOL_SIZE && list.Capacity < 100) _pool.Push(list);
            }
        }
    }

    /// <summary>
    ///     事件处理器 — 统一的事件中心（合并了原 EventManager 的事件总线 + 声明式自动注册）
    /// </summary>
    [Manager(-30)]
    public class EventProcessor : Manager<EventProcessor>
    {
        /// <summary>
        ///     存储所有Event方法信息
        /// </summary>
        private Dictionary<string, EventMethodInfo> _eventMethods;

        /// <summary>
        ///     存储所有EventListener方法信息
        /// </summary>
        private Dictionary<string, List<ListenerMethodInfo>> _listenerMethods;

        /// <summary>
        ///     事件监听器字典（合并自 EventManager）
        /// </summary>
        private Dictionary<string, List<EventDelegate>> _eventListeners;

        protected override void Initialize()
        {
            base.Initialize();

            // 事件总线必须最先初始化（扫描阶段可能触发 Service 创建 → AddListener/TriggerEvent）
            _eventListeners = new Dictionary<string, List<EventDelegate>>();

            _eventMethods = new Dictionary<string, EventMethodInfo>();
            _listenerMethods = new Dictionary<string, List<ListenerMethodInfo>>();

            // 扫描所有标注
            ScanEventAttributes();

            Log("EventProcessor 初始化完成！", Color.green);
        }

        #region Event Bus (merged from EventManager)

        /// <summary>
        ///     添加事件监听器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="listener">监听器方法</param>
        public void AddListener(string eventName, EventDelegate listener)
        {
            if (!_eventListeners.ContainsKey(eventName))
                _eventListeners[eventName] = new List<EventDelegate>();

            _eventListeners[eventName].Add(listener);
            Log($"为事件 {eventName} 添加了监听器", Color.blue);
        }

        /// <summary>
        ///     是否存在指定事件的监听器（用于在广播 fire-and-forget 事件前避免触发 "没有监听器" 警告）。
        /// </summary>
        public bool HasListener(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return false;
            return _eventListeners != null
                && _eventListeners.TryGetValue(eventName, out var list)
                && list != null && list.Count > 0;
        }

        /// <summary>
        ///     移除事件监听器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="listener">监听器方法</param>
        public void RemoveListener(string eventName, EventDelegate listener)
        {
            if (_eventListeners.ContainsKey(eventName))
            {
                _eventListeners[eventName].Remove(listener);
                Log($"为事件 {eventName} 移除了监听器", Color.blue);
            }
        }

        /// <summary>
        ///     触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        /// <returns>事件结果</returns>
        public List<object> TriggerEvent(string eventName, List<object> data = null)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                LogError("事件名称不能为空或null");
                return new List<object>();
            }

            if (!_eventListeners.ContainsKey(eventName))
            {
                LogWarning($"事件 {eventName} 没有监听器");
                return new List<object>();
            }

            Log($"触发事件: {eventName}", Color.magenta);

            var listeners = _eventListeners[eventName].ToList();
            var results = new List<object>();
            List<object> tempData = data;

            // 如果 data 为 null，从对象池租用临时 List
            if (tempData == null)
            {
                tempData = EventDataPool.Rent();
            }

            foreach (var listener in listeners)
                try
                {
                    var result = listener?.Invoke(eventName, tempData);
                    if (result != null) results.AddRange(result);
                }
                catch (Exception ex)
                {
                    LogError($"事件监听器中发生错误: {ex.Message}");
                }

            // 返回租用的临时 List
            if (data == null && tempData != null)
            {
                EventDataPool.Return(tempData);
            }

            return results;
        }

        #endregion

        #region Attribute Scanning

        /// <summary>
        ///     扫描所有Event标注 — 仅扫描用户代码相关的程序集，跳过系统/引擎程序集以加速启动
        /// </summary>
        private void ScanEventAttributes()
        {
            var skipped = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (AssemblyUtils.IsSystemAssembly(assembly))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    ScanAssembly(assembly);
                }
                catch (Exception ex)
                {
                    LogWarning($"扫描程序集 {assembly.GetName().Name} 时出错: {ex.Message}");
                }
            }

            // 注册所有 [EventListener] 监听器到事件总线
            RegisterListeners();

            Log(
                $"扫描完成，跳过系统程序集 {skipped} 个，发现 {_eventMethods.Count} 个Event方法和 {_listenerMethods.Values.Sum(listeners => listeners.Count)} 个监听器",
                Color.cyan);
        }

        /// <summary>
        ///     扫描单个程序集
        /// </summary>
        private void ScanAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                // 扫描Event方法
                ScanEventMethods(type);

                // 扫描EventListener方法
                ScanEventListenerMethods(type);
            }
        }

        /// <summary>
        ///     扫描Event方法
        /// </summary>
        private void ScanEventMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static);

            foreach (var method in methods)
            {
                var attributes = (EventAttribute[])method.GetCustomAttributes(typeof(EventAttribute), false);

                if (attributes.Length > 0)
                {
                    var attr = attributes[0];
                    var eventName = string.IsNullOrEmpty(attr.EventName) ? method.Name : attr.EventName;

                    _eventMethods[eventName] = new EventMethodInfo
                    {
                        MethodInfo = method,
                        Attribute = attr,
                        Target = method.IsStatic ? null : GetOrCreateInstance(type),
                        Delegate = CreateDelegate(method)
                    };

                    Log($"发现Event方法: {type.Name}.{method.Name} -> {eventName}", Color.yellow);
                }
            }
        }

        /// <summary>
        ///     扫描EventListener方法
        /// </summary>
        private void ScanEventListenerMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                          BindingFlags.Static);

            foreach (var method in methods)
            {
                var attributes =
                    (EventListenerAttribute[])method.GetCustomAttributes(typeof(EventListenerAttribute), false);

                foreach (var attr in attributes)
                {
                    if (!_listenerMethods.ContainsKey(attr.EventName))
                        _listenerMethods[attr.EventName] = new List<ListenerMethodInfo>();

                    _listenerMethods[attr.EventName].Add(new ListenerMethodInfo
                    {
                        MethodInfo = method,
                        Attribute = attr,
                        Target = method.IsStatic ? null : GetOrCreateInstance(type),
                        Delegate = CreateDelegate(method)
                    });

                    Log($"发现EventListener方法: {type.Name}.{method.Name} -> {attr.EventName}", Color.yellow);
                }
            }
        }

        /// <summary>
        ///     获取或创建实例
        /// </summary>
        private object GetOrCreateInstance(Type type)
        {
            // 如果是MonoBehaviour，尝试从场景中获取（Unity 2022+ 推荐 API）
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                var instance = FindFirstObjectByType(type, FindObjectsInactive.Include) as MonoBehaviour;
                if (instance != null) return instance;

                // 检查是否是 Manager 类型，Manager 通常由用户手动添加到场景
                var isManagerType = false;
                var currentType = type.BaseType;
                while (currentType != null && currentType != typeof(MonoBehaviour))
                {
                    if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(EssSystem.Core.EssManagers.Manager.Manager<>))
                    {
                        isManagerType = true;
                        break;
                    }
                    currentType = currentType.BaseType;
                }

                // Manager 类型不警告，因为它们可能还未添加到场景
                if (!isManagerType)
                {
                    LogWarning($"未找到 {type.Name} 的MonoBehaviour实例，请确保场景中存在该组件");
                }

                return null;
            }

            // 如果继承自 SingletonNormal<T>（包括所有 Service<T>），走 .Instance 拿单例
            // 否则 Activator 会创建一个游离实例，和 Singleton 的实例分裂，导致事件处理器绑定到错误实例
            var singletonInstance = TryGetSingletonInstance(type);
            if (singletonInstance != null) return singletonInstance;

            // 其他普通类才 new 一个
            try
            {
                return Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                LogError($"创建 {type.Name} 实例失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     如果 <paramref name="type" /> 继承自 SingletonNormal&lt;T&gt;，返回其 .Instance 单例；否则返回 null
        /// </summary>
        private static object TryGetSingletonInstance(Type type)
        {
            var walker = type.BaseType;
            while (walker != null)
            {
                if (walker.IsGenericType &&
                    walker.GetGenericTypeDefinition() == typeof(SingletonNormal<>))
                {
                    var prop = walker.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    return prop?.GetValue(null);
                }

                walker = walker.BaseType;
            }

            return null;
        }

        /// <summary>
        ///     将 [EventListener] 标注的监听器注册到事件总线
        /// </summary>
        private void RegisterListeners()
        {
            foreach (var kvp in _listenerMethods)
            {
                var eventName = kvp.Key;
                var listeners = kvp.Value;

                // 按优先级排序
                listeners.Sort((a, b) => a.Attribute.Priority.CompareTo(b.Attribute.Priority));

                foreach (var listenerInfo in listeners)
                    AddListener(eventName,
                        (en, d) => { return InvokeEventListener(listenerInfo, en, d); });
            }
        }

        #endregion

        /// <summary>
        ///     延迟解析 Target — 扫描期低优先级 Manager 可能尚未 Awake，这里在调用时重试。
        /// </summary>
        private object ResolveTarget(MethodInfo method, object cachedTarget)
        {
            if (cachedTarget != null || method.IsStatic) return cachedTarget;
            return GetOrCreateInstance(method.DeclaringType);
        }

        /// <summary>
        ///     调用EventListener方法
        /// </summary>
        private List<object> InvokeEventListener(ListenerMethodInfo listenerInfo, string eventName, List<object> data)
        {
            try
            {
                var parameters = listenerInfo.MethodInfo.GetParameters();
                var args = new object[parameters.Length];

                for (var i = 0; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == typeof(string))
                        args[i] = eventName;
                    else if (parameters[i].ParameterType == typeof(List<object>))
                        args[i] = data ?? new List<object>();
                    else
                        args[i] = null;

                listenerInfo.Target = ResolveTarget(listenerInfo.MethodInfo, listenerInfo.Target);

                var result = listenerInfo.Delegate != null
                    ? listenerInfo.Delegate(listenerInfo.Target, args)
                    : listenerInfo.MethodInfo.Invoke(listenerInfo.Target, args);

                if (result is List<object> resultList) return resultList;

                return new List<object> { result };
            }
            catch (Exception ex)
            {
                LogError($"调用EventListener方法 {listenerInfo.MethodInfo.Name} 时出错: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        ///     触发指定的Event方法
        /// </summary>
        public List<object> TriggerEventMethod(string eventName, List<object> data = null)
        {
            if (_eventMethods.ContainsKey(eventName))
            {
                var eventInfo = _eventMethods[eventName];

                try
                {
                    var parameters = eventInfo.MethodInfo.GetParameters();
                    var args = new object[parameters.Length];

                    for (var i = 0; i < parameters.Length; i++)
                        if (parameters[i].ParameterType == typeof(List<object>))
                            args[i] = data ?? new List<object>();
                        else
                            args[i] = null;

                    eventInfo.Target = ResolveTarget(eventInfo.MethodInfo, eventInfo.Target);

                    var result = eventInfo.Delegate != null
                        ? eventInfo.Delegate(eventInfo.Target, args)
                        : eventInfo.MethodInfo.Invoke(eventInfo.Target, args);

                    if (result is List<object> resultList) return resultList;

                    return new List<object> { result };
                }
                catch (Exception ex)
                {
                    // MethodInfo.Invoke 会把真实异常包成 TargetInvocationException，只打 ex.Message 会丢掉根因。
                    // 这里优先展开 InnerException，并附完整 ToString()（含堆栈）。
                    var root = ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null
                        ? tie.InnerException : ex;
                    LogError($"调用Event方法 {eventInfo.MethodInfo.Name} 时出错: {root.GetType().Name}: {root.Message}\n{root}");
                    return new List<object>();
                }
            }

            LogWarning($"未找到Event方法: {eventName}");
            return new List<object>();
        }

        /// <summary>
        ///     创建方法委托（性能优化）
        /// </summary>
        private Func<object, object[], object> CreateDelegate(MethodInfo method)
        {
            // 由于方法签名不匹配（Event 方法通常是 List<object> 参数，而委托是 object[]），
            // 直接使用 Delegate.CreateDelegate 会失败。这里保留反射调用作为降级方案。
            return (target, args) => method.Invoke(target, args);
        }

        /// <summary>
        ///     获取所有已注册的Event方法名称
        /// </summary>
        public IEnumerable<string> GetEventNames()
        {
            return _eventMethods.Keys;
        }

        /// <summary>
        ///     获取所有已注册的监听器事件名称
        /// </summary>
        public IEnumerable<string> GetListenerEventNames()
        {
            return _listenerMethods.Keys;
        }

        /// <summary>
        ///     Event方法信息
        /// </summary>
        private class EventMethodInfo
        {
            public MethodInfo MethodInfo { get; set; }
            public EventAttribute Attribute { get; set; }
            public object Target { get; set; }
            public Func<object, object[], object> Delegate { get; set; }
        }

        /// <summary>
        ///     监听器方法信息
        /// </summary>
        private class ListenerMethodInfo
        {
            public MethodInfo MethodInfo { get; set; }
            public EventListenerAttribute Attribute { get; set; }
            public object Target { get; set; }
            public Func<object, object[], object> Delegate { get; set; }
        }
    }
}
