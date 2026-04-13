using System;

namespace EssSystem.Core.Event
{
    /// <summary>
    /// 事件基类 - 所有事件的基础类
    /// </summary>
    public abstract class Event
    {
        /// <summary>
        /// 事件名称
        /// </summary>
        public string EventName { get; protected set; }
        
        /// <summary>
        /// 事件数据
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        protected Event(string eventName, object data = null)
        {
            EventName = eventName;
            Data = data;
        }
    }

    /// <summary>
    /// 泛型事件类 - 类型安全的事件类
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    public abstract class Event<T> : Event
    {
        /// <summary>
        /// 类型化的事件数据
        /// </summary>
        public new T Data
        {
            get => (T)base.Data;
            set => base.Data = value;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="data">事件数据</param>
        protected Event(string eventName, T data = default) : base(eventName, data)
        {
        }
    }
}
