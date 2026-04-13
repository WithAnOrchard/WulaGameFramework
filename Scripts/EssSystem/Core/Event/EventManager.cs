using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EssSystem.Core.Event
{
    /// <summary>
    /// 事件委托 - 事件监听器的委托类型
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="data">事件数据</param>
    /// <returns>事件结果</returns>
    public delegate List<object> EventDelegate(string eventName, List<object> data);

    /// <summary>
    /// 事件管理器 - Unity MonoBehaviour单例
    /// </summary>
    public class EventManager : SingletonMono<EventManager>
    {
        /// <summary>
        /// 事件监听器字典，键为事件名称
        /// </summary>
        private Dictionary<string, List<EventDelegate>> _eventListeners;

        /// <summary>
        /// 初始化事件管理器
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            _eventListeners = new Dictionary<string, List<EventDelegate>>();
            
            Log("事件管理器初始化完成！", Color.green);
        }

        /// <summary>
        /// 添加事件监听器
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="listener">监听器方法</param>
        public void AddListener(string eventName, EventDelegate listener)
        {
            if (!_eventListeners.ContainsKey(eventName))
            {
                _eventListeners[eventName] = new List<EventDelegate>();
            }

            _eventListeners[eventName].Add(listener);
            Log($"为事件 {eventName} 添加了监听器", Color.blue);
        }

        /// <summary>
        /// 移除事件监听器
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
        /// 触发事件
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
            
            foreach (var listener in listeners)
            {
                try
                {
                    var result = listener?.Invoke(eventName, data ?? new List<object>());
                    if (result != null)
                    {
                        results.AddRange(result);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"事件监听器中发生错误: {ex.Message}");
                }
            }

            return results;
        }
    }
}
