using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Singleton;
using UnityEngine;

namespace EssSystem.Core.Event.AutoRegisterEvent
{
    /// <summary>
    ///     Event处理器，负责扫描和处理Event标注
    /// </summary>
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

        protected override void Initialize()
        {
            base.Initialize();
            _eventMethods = new Dictionary<string, EventMethodInfo>();
            _listenerMethods = new Dictionary<string, List<ListenerMethodInfo>>();

            // 扫描所有标注
            ScanEventAttributes();

            Log("Event处理器初始化完成！", Color.green);
        }

        /// <summary>
        ///     扫描所有Event标注 — 仅扫描用户代码相关的程序集，跳过系统/引擎程序集以加速启动
        /// </summary>
        private void ScanEventAttributes()
        {
            var skipped = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (IsSystemAssembly(assembly))
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

            // 注册所有监听器到EventManager
            RegisterListenersToEventManager();

            Log(
                $"扫描完成，跳过系统程序集 {skipped} 个，发现 {_eventMethods.Count} 个Event方法和 {_listenerMethods.Values.Sum(listeners => listeners.Count)} 个监听器",
                Color.cyan);
        }

        /// <summary>
        ///     判断是否为系统/引擎程序集（不包含用户代码）
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

                LogWarning($"未找到 {type.Name} 的MonoBehaviour实例，请确保场景中存在该组件");
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
        ///     将监听器注册到EventManager
        /// </summary>
        private void RegisterListenersToEventManager()
        {
            foreach (var kvp in _listenerMethods)
            {
                var eventName = kvp.Key;
                var listeners = kvp.Value;

                // 按优先级排序
                listeners.Sort((a, b) => a.Attribute.Priority.CompareTo(b.Attribute.Priority));

                foreach (var listenerInfo in listeners)
                    EventManager.Instance.AddListener(eventName,
                        (eventName, data) => { return InvokeEventListener(listenerInfo, eventName, data); });
            }
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

                    var result = eventInfo.Delegate != null
                        ? eventInfo.Delegate(eventInfo.Target, args)
                        : eventInfo.MethodInfo.Invoke(eventInfo.Target, args);

                    if (result is List<object> resultList) return resultList;

                    return new List<object> { result };
                }
                catch (Exception ex)
                {
                    LogError($"调用Event方法 {eventInfo.MethodInfo.Name} 时出错: {ex.Message}");
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