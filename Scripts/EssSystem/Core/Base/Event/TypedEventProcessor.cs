using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Base.Event
{
    /// <summary>
    /// 类型化事件处理器（Phase 2 性能优化）
    /// 
    /// 用于高频事件的性能优化，避免 List&lt;object&gt; 装箱和拆箱开销。
    /// 支持泛型委托，直接传递强类型参数。
    /// 
    /// 使用场景：
    /// - 每帧多次触发的事件（坐标同步、状态更新等）
    /// - 参数类型固定的事件
    /// - 需要最小化 GC 的关键路径
    /// </summary>
    public static class TypedEventProcessor
    {
        /// <summary>无参数事件委托</summary>
        public delegate void EventAction(string eventName);

        /// <summary>单参数事件委托</summary>
        public delegate void EventAction<T>(string eventName, T arg);

        /// <summary>双参数事件委托</summary>
        public delegate void EventAction<T1, T2>(string eventName, T1 arg1, T2 arg2);

        /// <summary>三参数事件委托</summary>
        public delegate void EventAction<T1, T2, T3>(string eventName, T1 arg1, T2 arg2, T3 arg3);

        /// <summary>无参数返回值事件委托</summary>
        public delegate T EventFunc<T>(string eventName);

        /// <summary>单参数返回值事件委托</summary>
        public delegate TResult EventFunc<T, TResult>(string eventName, T arg);

        /// <summary>双参数返回值事件委托</summary>
        public delegate TResult EventFunc<T1, T2, TResult>(string eventName, T1 arg1, T2 arg2);

        private static readonly Dictionary<string, Delegate> _typedListeners = new();
        private static readonly HashSet<string> _silentEvents = new();

        /// <summary>注册类型化事件监听器（无参数）</summary>
        public static void AddListener(string eventName, EventAction listener)
        {
            if (string.IsNullOrEmpty(eventName) || listener == null) return;

            if (!_typedListeners.TryGetValue(eventName, out var existing))
            {
                _typedListeners[eventName] = listener;
            }
            else if (existing is EventAction action)
            {
                _typedListeners[eventName] = (EventAction)Delegate.Combine(action, listener);
            }
        }

        /// <summary>注册类型化事件监听器（单参数）</summary>
        public static void AddListener<T>(string eventName, EventAction<T> listener)
        {
            if (string.IsNullOrEmpty(eventName) || listener == null) return;

            if (!_typedListeners.TryGetValue(eventName, out var existing))
            {
                _typedListeners[eventName] = listener;
            }
            else if (existing is EventAction<T> action)
            {
                _typedListeners[eventName] = (EventAction<T>)Delegate.Combine(action, listener);
            }
        }

        /// <summary>注册类型化事件监听器（双参数）</summary>
        public static void AddListener<T1, T2>(string eventName, EventAction<T1, T2> listener)
        {
            if (string.IsNullOrEmpty(eventName) || listener == null) return;

            if (!_typedListeners.TryGetValue(eventName, out var existing))
            {
                _typedListeners[eventName] = listener;
            }
            else if (existing is EventAction<T1, T2> action)
            {
                _typedListeners[eventName] = (EventAction<T1, T2>)Delegate.Combine(action, listener);
            }
        }

        /// <summary>移除类型化事件监听器（无参数）</summary>
        public static void RemoveListener(string eventName, EventAction listener)
        {
            if (string.IsNullOrEmpty(eventName) || listener == null) return;

            if (_typedListeners.TryGetValue(eventName, out var existing) && existing is EventAction action)
            {
                var combined = (EventAction)Delegate.Remove(action, listener);
                if (combined == null)
                    _typedListeners.Remove(eventName);
                else
                    _typedListeners[eventName] = combined;
            }
        }

        /// <summary>移除类型化事件监听器（单参数）</summary>
        public static void RemoveListener<T>(string eventName, EventAction<T> listener)
        {
            if (string.IsNullOrEmpty(eventName) || listener == null) return;

            if (_typedListeners.TryGetValue(eventName, out var existing) && existing is EventAction<T> action)
            {
                var combined = (EventAction<T>)Delegate.Remove(action, listener);
                if (combined == null)
                    _typedListeners.Remove(eventName);
                else
                    _typedListeners[eventName] = combined;
            }
        }

        /// <summary>移除类型化事件监听器（双参数）</summary>
        public static void RemoveListener<T1, T2>(string eventName, EventAction<T1, T2> listener)
        {
            if (string.IsNullOrEmpty(eventName) || listener == null) return;

            if (_typedListeners.TryGetValue(eventName, out var existing) && existing is EventAction<T1, T2> action)
            {
                var combined = (EventAction<T1, T2>)Delegate.Remove(action, listener);
                if (combined == null)
                    _typedListeners.Remove(eventName);
                else
                    _typedListeners[eventName] = combined;
            }
        }

        /// <summary>触发类型化事件（无参数）</summary>
        public static void TriggerEvent(string eventName)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            if (_typedListeners.TryGetValue(eventName, out var listener) && listener is EventAction action)
            {
                try
                {
                    action.Invoke(eventName);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"类型化事件 {eventName} 执行异常: {ex.Message}");
                }
            }
        }

        /// <summary>触发类型化事件（单参数）</summary>
        public static void TriggerEvent<T>(string eventName, T arg)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            if (_typedListeners.TryGetValue(eventName, out var listener) && listener is EventAction<T> action)
            {
                try
                {
                    action.Invoke(eventName, arg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"类型化事件 {eventName} 执行异常: {ex.Message}");
                }
            }
        }

        /// <summary>触发类型化事件（双参数）</summary>
        public static void TriggerEvent<T1, T2>(string eventName, T1 arg1, T2 arg2)
        {
            if (string.IsNullOrEmpty(eventName)) return;

            if (_typedListeners.TryGetValue(eventName, out var listener) && listener is EventAction<T1, T2> action)
            {
                try
                {
                    action.Invoke(eventName, arg1, arg2);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"类型化事件 {eventName} 执行异常: {ex.Message}");
                }
            }
        }

        /// <summary>触发类型化事件并返回结果（无参数）</summary>
        public static T TriggerEventFunc<T>(string eventName, T defaultValue = default)
        {
            if (string.IsNullOrEmpty(eventName)) return defaultValue;

            if (_typedListeners.TryGetValue(eventName, out var listener) && listener is EventFunc<T> func)
            {
                try
                {
                    return func.Invoke(eventName);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"类型化事件 {eventName} 执行异常: {ex.Message}");
                }
            }
            return defaultValue;
        }

        /// <summary>触发类型化事件并返回结果（单参数）</summary>
        public static TResult TriggerEventFunc<T, TResult>(string eventName, T arg, TResult defaultValue = default)
        {
            if (string.IsNullOrEmpty(eventName)) return defaultValue;

            if (_typedListeners.TryGetValue(eventName, out var listener) && listener is EventFunc<T, TResult> func)
            {
                try
                {
                    return func.Invoke(eventName, arg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"类型化事件 {eventName} 执行异常: {ex.Message}");
                }
            }
            return defaultValue;
        }

        /// <summary>将事件加入静默集，不输出日志</summary>
        public static void SilenceEvent(string eventName)
        {
            if (!string.IsNullOrEmpty(eventName)) _silentEvents.Add(eventName);
        }

        /// <summary>从静默集移除事件</summary>
        public static void UnsilenceEvent(string eventName)
        {
            if (!string.IsNullOrEmpty(eventName)) _silentEvents.Remove(eventName);
        }

        /// <summary>获取已注册的类型化事件数量</summary>
        public static int GetEventCount() => _typedListeners.Count;

        /// <summary>清理所有类型化事件监听器</summary>
        public static void ClearAllListeners()
        {
            _typedListeners.Clear();
            _silentEvents.Clear();
        }
    }
}
