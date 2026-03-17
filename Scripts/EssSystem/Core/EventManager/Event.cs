using System.Collections.Generic;

namespace EssSystem.Core.EventManager
{
    //事件触发机制
    public abstract class TriggerEvent 
    {
        public string EventName;

        public TriggerEvent(string eventName)
        {
            this.EventName = eventName;
        }

        protected TriggerEvent()
        {
            
        }

        public virtual void Init()
        {
            EventManager.Instance.Subscribe(EventName,Action);
        }

        public abstract void Action(List<object> o);
    }
}