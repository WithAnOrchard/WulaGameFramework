using System;

namespace EssSystem.Core.Event.AutoRegisterEvent
{
    /// <summary>
    ///     Event特性 - 用于将方法自动注册为事件
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class EventAttribute : Attribute
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="eventName">事件名称，如果为空则使用方法名</param>
        public EventAttribute(string eventName = null)
        {
            EventName = eventName;
        }

        /// <summary>
        ///     事件名称
        /// </summary>
        public string EventName { get; }

        /// <summary>
        ///     是否自动触发事件
        /// </summary>
        public bool AutoTrigger { get; set; } = false;

        /// <summary>
        ///     事件优先级
        /// </summary>
        public int Priority { get; set; } = 0;
    }

    /// <summary>
    ///     Event监听器特性 - 用于将方法自动注册为事件监听器
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class EventListenerAttribute : Attribute
    {
        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="eventName">监听的事件名称</param>
        public EventListenerAttribute(string eventName)
        {
            EventName = eventName;
        }

        /// <summary>
        ///     监听的事件名称
        /// </summary>
        public string EventName { get; }

        /// <summary>
        ///     监听器优先级
        /// </summary>
        public int Priority { get; set; } = 0;
    }
}