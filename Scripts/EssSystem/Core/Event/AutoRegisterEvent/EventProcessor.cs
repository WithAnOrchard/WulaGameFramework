using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using EssSystem.Core.Manager;

namespace EssSystem.Core.Event.AutoRegisterEvent
{
    /// <summary>
    /// Event处理器，负责扫描和处理Event标注
    /// </summary>
    public class EventProcessor : Manager<EventProcessor>
    {
        /// <summary>
        /// 存储所有Event方法信息
        /// </summary>
        private Dictionary<string, EventMethodInfo> _eventMethods;

        /// <summary>
        /// 存储所有EventListener方法信息
        /// </summary>
        private Dictionary<string, List<ListenerMethodInfo>> _listenerMethods;

        /// <summary>
        /// Event方法信息
        /// </summary>
        private class EventMethodInfo
        {
            public MethodInfo MethodInfo { get; set; }
            public EventAttribute Attribute { get; set; }
            public object Target { get; set; }
        }

        /// <summary>
        /// 监听器方法信息
        /// </summary>
        private class ListenerMethodInfo
        {
            public MethodInfo MethodInfo { get; set; }
            public EventListenerAttribute Attribute { get; set; }
            public object Target { get; set; }
        }

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
        /// 扫描所有Event标注
        /// </summary>
        private void ScanEventAttributes()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (Assembly assembly in assemblies)
            {
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
            
            Log($"扫描完成，发现 {_eventMethods.Count} 个Event方法和 {_listenerMethods.Values.Sum(listeners => listeners.Count)} 个监听器", Color.cyan);
        }

        /// <summary>
        /// 扫描单个程序集
        /// </summary>
        private void ScanAssembly(Assembly assembly)
        {
            Type[] types = assembly.GetTypes();
            
            foreach (Type type in types)
            {
                // 扫描Event方法
                ScanEventMethods(type);
                
                // 扫描EventListener方法
                ScanEventListenerMethods(type);
            }
        }

        /// <summary>
        /// 扫描Event方法
        /// </summary>
        private void ScanEventMethods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (MethodInfo method in methods)
            {
                EventAttribute[] attributes = (EventAttribute[])method.GetCustomAttributes(typeof(EventAttribute), false);
                
                if (attributes.Length > 0)
                {
                    EventAttribute attr = attributes[0];
                    string eventName = string.IsNullOrEmpty(attr.EventName) ? method.Name : attr.EventName;
                    
                    _eventMethods[eventName] = new EventMethodInfo
                    {
                        MethodInfo = method,
                        Attribute = attr,
                        Target = method.IsStatic ? null : GetOrCreateInstance(type)
                    };
                    
                    Log($"发现Event方法: {type.Name}.{method.Name} -> {eventName}", Color.yellow);
                }
            }
        }

        /// <summary>
        /// 扫描EventListener方法
        /// </summary>
        private void ScanEventListenerMethods(Type type)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (MethodInfo method in methods)
            {
                EventListenerAttribute[] attributes = (EventListenerAttribute[])method.GetCustomAttributes(typeof(EventListenerAttribute), false);
                
                foreach (EventListenerAttribute attr in attributes)
                {
                    if (!_listenerMethods.ContainsKey(attr.EventName))
                    {
                        _listenerMethods[attr.EventName] = new List<ListenerMethodInfo>();
                    }
                    
                    _listenerMethods[attr.EventName].Add(new ListenerMethodInfo
                    {
                        MethodInfo = method,
                        Attribute = attr,
                        Target = method.IsStatic ? null : GetOrCreateInstance(type)
                    });
                    
                    Log($"发现EventListener方法: {type.Name}.{method.Name} -> {attr.EventName}", Color.yellow);
                }
            }
        }

        /// <summary>
        /// 获取或创建实例
        /// </summary>
        private object GetOrCreateInstance(Type type)
        {
            // 如果是MonoBehaviour，尝试从场景中获取
            if (typeof(MonoBehaviour).IsAssignableFrom(type))
            {
                MonoBehaviour instance = FindObjectOfType(type) as MonoBehaviour;
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
        /// 如果 <paramref name="type"/> 继承自 SingletonNormal&lt;T&gt;，返回其 .Instance 单例；否则返回 null
        /// </summary>
        private static object TryGetSingletonInstance(Type type)
        {
            Type walker = type.BaseType;
            while (walker != null)
            {
                if (walker.IsGenericType &&
                    walker.GetGenericTypeDefinition() == typeof(SingletonNormal<>))
                {
                    PropertyInfo prop = walker.GetProperty("Instance",
                        BindingFlags.Public | BindingFlags.Static);
                    return prop?.GetValue(null);
                }
                walker = walker.BaseType;
            }
            return null;
        }

        /// <summary>
        /// 将监听器注册到EventManager
        /// </summary>
        private void RegisterListenersToEventManager()
        {
            foreach (var kvp in _listenerMethods)
            {
                string eventName = kvp.Key;
                List<ListenerMethodInfo> listeners = kvp.Value;
                
                // 按优先级排序
                listeners.Sort((a, b) => a.Attribute.Priority.CompareTo(b.Attribute.Priority));
                
                foreach (var listenerInfo in listeners)
                {
                    EventManager.Instance.AddListener(eventName, (eventName, data) => 
                    {
                        return InvokeEventListener(listenerInfo, eventName, data);
                    });
                }
            }
        }

        /// <summary>
        /// 调用EventListener方法
        /// </summary>
        private List<object> InvokeEventListener(ListenerMethodInfo listenerInfo, string eventName, List<object> data)
        {
            try
            {
                ParameterInfo[] parameters = listenerInfo.MethodInfo.GetParameters();
                object[] args = new object[parameters.Length];
                
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType == typeof(string))
                    {
                        args[i] = eventName;
                    }
                    else if (parameters[i].ParameterType == typeof(List<object>))
                    {
                        args[i] = data ?? new List<object>();
                    }
                    else
                    {
                        args[i] = null;
                    }
                }
                
                object result = listenerInfo.MethodInfo.Invoke(listenerInfo.Target, args);
                
                if (result is List<object> resultList)
                {
                    return resultList;
                }
                
                return new List<object> { result };
            }
            catch (Exception ex)
            {
                LogError($"调用EventListener方法 {listenerInfo.MethodInfo.Name} 时出错: {ex.Message}");
                return new List<object>();
            }
        }

        /// <summary>
        /// 触发指定的Event方法
        /// </summary>
        public List<object> TriggerEventMethod(string eventName, List<object> data = null)
        {
            if (_eventMethods.ContainsKey(eventName))
            {
                EventMethodInfo eventInfo = _eventMethods[eventName];
                
                try
                {
                    ParameterInfo[] parameters = eventInfo.MethodInfo.GetParameters();
                    object[] args = new object[parameters.Length];
                    
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType == typeof(List<object>))
                        {
                            args[i] = data ?? new List<object>();
                        }
                        else
                        {
                            args[i] = null;
                        }
                    }
                    
                    object result = eventInfo.MethodInfo.Invoke(eventInfo.Target, args);
                    
                    if (result is List<object> resultList)
                    {
                        return resultList;
                    }
                    
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
        /// 获取所有已注册的Event方法名称
        /// </summary>
        public IEnumerable<string> GetEventNames()
        {
            return _eventMethods.Keys;
        }

        /// <summary>
        /// 获取所有已注册的监听器事件名称
        /// </summary>
        public IEnumerable<string> GetListenerEventNames()
        {
            return _listenerMethods.Keys;
        }
    }
}
