using System;

namespace EssSystem.Core.Event
{
    /// <summary>
    /// Event标注属性，用于将方法自动注册为Event
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class EventAttribute : Attribute
    {
        /// <summary>
        /// 事件名称
        /// </summary>
        public string EventName { get; }

        /// <summary>
        /// 是否自动触发事件
        /// </summary>
        public bool AutoTrigger { get; set; } = false;

        /// <summary>
        /// 事件优先级
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="eventName">事件名称，如果为空则使用方法名</param>
        public EventAttribute(string eventName = null)
        {
            EventName = eventName;
        }
    }

    /// <summary>
    /// Event监听器标注属性，用于将方法自动注册为Event监听器
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class EventListenerAttribute : Attribute
    {
        /// <summary>
        /// 监听的事件名称
        /// </summary>
        public string EventName { get; }

        /// <summary>
        /// 监听器优先级
        /// </summary>
        public int Priority { get; set; } = 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="eventName">监听的事件名称</param>
        public EventListenerAttribute(string eventName)
        {
            EventName = eventName;
        }
    }
}
