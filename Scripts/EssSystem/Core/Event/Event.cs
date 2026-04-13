using System;

namespace EssSystem.Core.Event
{
    public abstract class Event
    {
        public string EventName { get; protected set; }
        public object Data { get; set; }

        protected Event(string eventName, object data = null)
        {
            EventName = eventName;
            Data = data;
        }
    }

    public abstract class Event<T> : Event
    {
        public new T Data
        {
            get => (T)base.Data;
            set => base.Data = value;
        }

        protected Event(string eventName, T data = default) : base(eventName, data)
        {
        }
    }
}
