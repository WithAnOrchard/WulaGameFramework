using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Base.Singleton;
using EssSystem.Core.Base.Util;
using UnityEngine;
// ApplicationLifecycle 已在 EssSystem.Core.Util using 范围内

namespace EssSystem.Core.Base.Event
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

        // C-E6: 共享空参数 List，避免每次 invoke 都 new。只读、不允许外部修改。
        private static readonly List<object> _emptyData = new();

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
            // C-E3: TryGetValue 一次查找代替 ContainsKey + indexer 双查找。
            if (!_eventListeners.TryGetValue(eventName, out var list))
            {
                list = new List<EventDelegate>();
                _eventListeners[eventName] = list;
            }
            list.Add(listener);
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
            // C-E3: TryGetValue 取代 ContainsKey + indexer。
            if (!_eventListeners.TryGetValue(eventName, out var list)) return;
            list.Remove(listener);
            // A-E12: 列表空后从字典里拿掉，避免后续 TriggerEvent 走空拷贝。
            if (list.Count == 0) _eventListeners.Remove(eventName);
            Log($"为事件 {eventName} 移除了监听器", Color.blue);
        }

        /// <summary>
        ///     触发事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        /// <returns>事件结果</returns>
        public List<object> TriggerEvent(string eventName, List<object> data = null)
        {
            // TODO#1: 应用退出期间静默跳过所有事件分发，避免访问已销毁的 Unity Object
            if (ApplicationLifecycle.IsQuitting) return _emptyData;

            if (string.IsNullOrEmpty(eventName))
            {
                LogError("事件名称不能为空或null");
                return _emptyData;   // 重用只读空表，免一次 alloc
            }

            // C-E3 + D-E4: TryGetValue 一次查找；没监听器不再 LogWarning——
            // 广播 fire-and-forget 没人订阅是合法状态。调用方可用 HasListener 显式检查。
            if (!_eventListeners.TryGetValue(eventName, out var registeredListeners) || registeredListeners.Count == 0)
            {
                Log($"事件 {eventName} 没有监听器（静默跳过）", Color.gray);
                return _emptyData;
            }

            Log($"触发事件: {eventName}", Color.magenta);

            // A-E1: 从 ListPool 租用拷贝 代替每次 ToList。
            var listenersSnapshot = ListPool<EventDelegate>.Rent();
            listenersSnapshot.AddRange(registeredListeners);

            var tempData = data ?? EventDataPool.Rent();
            // A-E2: results 延迟创建——只有某个 listener 返回非空才 new。
            List<object> results = null;

            try
            {
                for (int i = 0; i < listenersSnapshot.Count; i++)
                {
                    var listener = listenersSnapshot[i];
                    if (listener == null) continue;
                    try
                    {
                        var result = listener.Invoke(eventName, tempData);
                        if (result != null && result.Count > 0)
                        {
                            results ??= new List<object>(result.Count);
                            results.AddRange(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"事件监听器中发生错误: {ex.Message}");
                    }
                }
            }
            finally
            {
                ListPool<EventDelegate>.Return(listenersSnapshot);
                if (data == null) EventDataPool.Return(tempData);
            }

            return results ?? _emptyData;
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
                        Delegate = CreateDelegate(method),
                        Parameters = method.GetParameters()   // C-E5
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
                        Delegate = CreateDelegate(method),
                        Parameters = method.GetParameters()   // C-E5
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
                listenerInfo.Target = ResolveTarget(listenerInfo.MethodInfo, listenerInfo.Target);

                // 实例方法但找不到 Target —— 通常意味着该 Manager / Service 类型存在于程序集中
                // （所以扫描时注册了 listener），但当前场景没有挂相应组件。静默跳过。
                if (listenerInfo.Target == null && !listenerInfo.MethodInfo.IsStatic)
                    return null;   // C-E6: 返回 null 让 TriggerEvent 跳过拼接

                // C-E5 + C-E6: 参数数组仅在 slow 路径才 new；data 为 null 时复用共享空表。
                var dataNonNull = data ?? _emptyData;
                var parameters = listenerInfo.Parameters;
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    var pt = parameters[i].ParameterType;
                    if (pt == typeof(string)) args[i] = eventName;
                    else if (pt == typeof(List<object>)) args[i] = dataNonNull;
                    else args[i] = null;
                }

                var result = listenerInfo.Delegate != null
                    ? listenerInfo.Delegate(listenerInfo.Target, args)
                    : listenerInfo.MethodInfo.Invoke(listenerInfo.Target, args);

                // 保持原语义：List<object> 直接返；其它包一层；null 返 null 跳过拼接。
                if (result == null) return null;
                if (result is List<object> resultList) return resultList;
                return new List<object> { result };
            }
            catch (Exception ex)
            {
                LogError($"调用EventListener方法 {listenerInfo.MethodInfo.Name} 时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///     触发指定的Event方法
        /// </summary>
        public List<object> TriggerEventMethod(string eventName, List<object> data = null)
        {
            // TODO#1: 应用退出期间静默跳过所有事件分发，避免访问已销毁的 Unity Object
            if (ApplicationLifecycle.IsQuitting) return _emptyData;

            // C-E3: TryGetValue 一次查找。
            if (!_eventMethods.TryGetValue(eventName, out var eventInfo))
            {
                LogWarning($"未找到Event方法: {eventName}");
                return _emptyData;
            }

            try
            {
                eventInfo.Target = ResolveTarget(eventInfo.MethodInfo, eventInfo.Target);

                // C-E5 + C-E6: 参数数组仅在 slow 路径才 new；data 为 null 时复用共享空表。
                var dataNonNull = data ?? _emptyData;
                var parameters = eventInfo.Parameters;
                var args = new object[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].ParameterType == typeof(List<object>) ? dataNonNull : null;
                }

                var result = eventInfo.Delegate != null
                    ? eventInfo.Delegate(eventInfo.Target, args)
                    : eventInfo.MethodInfo.Invoke(eventInfo.Target, args);

                if (result == null) return _emptyData;
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
                return _emptyData;
            }
        }

        /// <summary>
        ///     创建方法委托 — B-E8 优化：优先走 Expression.Compile 生成强类型 IL（比 MethodInfo.Invoke 快约 5-10x），
        ///     失败时冷静兑底反射。IL2CPP / AOT 环境 Expression.Compile 会抛，会被 try/catch 捕获后走兑底。
        /// </summary>
        private static Func<object, object[], object> CreateDelegate(MethodInfo method)
        {
            try
            {
                var targetParam = Expression.Parameter(typeof(object), "target");
                var argsParam = Expression.Parameter(typeof(object[]), "args");
                var ps = method.GetParameters();
                var callArgs = new Expression[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var idx = Expression.ArrayIndex(argsParam, Expression.Constant(i));
                    callArgs[i] = Expression.Convert(idx, ps[i].ParameterType);
                }
                Expression call = method.IsStatic
                    ? Expression.Call(method, callArgs)
                    : Expression.Call(Expression.Convert(targetParam, method.DeclaringType), method, callArgs);

                Expression body = method.ReturnType == typeof(void)
                    ? Expression.Block(call, Expression.Constant(null, typeof(object)))
                    : (method.ReturnType.IsValueType
                        ? (Expression)Expression.Convert(call, typeof(object))
                        : call);

                return Expression.Lambda<Func<object, object[], object>>(body, targetParam, argsParam).Compile();
            }
            catch (Exception)
            {
                // AOT / IL2CPP 下 Expression.Compile 不可用，兑底反射。
                return (target, args) => method.Invoke(target, args);
            }
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
            public ParameterInfo[] Parameters { get; set; }   // C-E5: 扫描期缓存参数信息，免每次 invoke 都 GetParameters()。
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
            public ParameterInfo[] Parameters { get; set; }   // C-E5: 扫描期缓存参数信息。
        }
    }
}
