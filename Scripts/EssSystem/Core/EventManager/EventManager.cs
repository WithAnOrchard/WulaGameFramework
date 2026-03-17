using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EssSystem.Core.Singleton;
using UnityEngine;

namespace EssSystem.Core.EventManager
{
    //事件机制，所有的controller都需要使用EventManager来实现目的,而实际功能逻辑需要写在manager
    public class EventManager : Singleton<EventManager>
    {

        public void InitEvents()
        {
            var subClasses =  Assembly.GetExecutingAssembly().GetTypes();
            foreach (var subClass in subClasses)
            {
                if (subClass.BaseType.Name.Equals("TriggerEvent"))
                {
                    Debug.Log(subClass.Name);
                   object instance=Activator.CreateInstance(subClass);
                   MethodInfo method = instance.GetType().GetMethod("Init");
                   method.Invoke(instance, null);
                }
            }

        }
        
        protected override void Init(bool logMessage = true)
        {
            Debug.Log("正在加载事件");
            this.LogMessage = logMessage;
            InitEvents();

        }

        // 事件字典：使用委托存储监听方法
        private Dictionary<string, Action<List<object>>> _eventDictionary = new Dictionary<string, Action<List<object>>>();

        
        public void Subscribe(string eventName, Action<List<object>> listener)
        {
            if (string.IsNullOrEmpty(eventName))
            {
                Debug.LogError("事件名不能为空！");
                return;
            }

            if (_eventDictionary.TryGetValue(eventName, out Action<List<object>> thisEvent))
            {
                thisEvent += listener;
                _eventDictionary[eventName] = thisEvent;
            }
            else
            {
                thisEvent += listener;
                _eventDictionary.Add(eventName, thisEvent);
            }
        }


        // 取消订阅
        public void Unsubscribe(string eventName, Action<object> listener)
        {
            if (_eventDictionary.TryGetValue(eventName, out Action<List<object>> thisEvent))
            {
                thisEvent -= listener;
                if (thisEvent == null)
                {
                    _eventDictionary.Remove(eventName);
                }
                else
                {
                    _eventDictionary[eventName] = thisEvent;
                }
            }
        }

        public void TriggerString(string eventData)
        {
            List<String> param = eventData.Split(".").ToList();
            string eventName = param[0];
            param.RemoveAt(0);
            EventManager.Instance.TriggerEvent(eventName,param.Cast<object>().ToList());
        }
        
        // 触发事件
        public void TriggerEvent(string eventName, List<object> eventData = null)
        {
            if (_eventDictionary.TryGetValue(eventName, out Action<List<object>> thisEvent))
            {
                Log("触发事件" + eventName + "数据为" + eventData);
                thisEvent?.Invoke(eventData);
            }
            else
            {
                Debug.LogWarning($"未注册的事件: {eventName}");
            }
        }


        // 新增泛型方法
        public void Subscribe<T>(string eventName, Action<T> listener)
        {
            Subscribe(eventName, data =>
            {
                if (data is T typedData) listener(typedData);
            });
        }


        // 清空所有事件监听（场景切换时调用）
        public void ClearAllEvents()
        {
            _eventDictionary.Clear();
            Debug.Log("所有事件监听已清空");
        }

        private void OnDestroy()
        {
            // 对象销毁时自动清理相关事件
            if (this == Instance)
            {
                ClearAllEvents();
            }
        }
    }
}