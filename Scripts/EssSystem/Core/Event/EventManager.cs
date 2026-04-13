using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EssSystem.Core.Event
{
    public delegate List<object> EventDelegate(string eventName, List<object> data);

    public class EventManager : SingletonMono<EventManager>
    {
        private Dictionary<string, List<EventDelegate>> _eventListeners;

        protected override void Initialize()
        {
            base.Initialize();
            _eventListeners = new Dictionary<string, List<EventDelegate>>();
            
            Log("事件管理器初始化完成！", Color.green);
        }

        public void AddListener(string eventName, EventDelegate listener)
        {
            if (!_eventListeners.ContainsKey(eventName))
            {
                _eventListeners[eventName] = new List<EventDelegate>();
            }

            _eventListeners[eventName].Add(listener);
            Log($"为事件 {eventName} 添加了监听器", Color.blue);
        }

        public void RemoveListener(string eventName, EventDelegate listener)
        {
            if (_eventListeners.ContainsKey(eventName))
            {
                _eventListeners[eventName].Remove(listener);
                Log($"为事件 {eventName} 移除了监听器", Color.blue);
            }
        }

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
