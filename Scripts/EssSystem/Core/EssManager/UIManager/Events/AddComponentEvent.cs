using System;
using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.UIManager.Events
{
    public class AddComponentEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            throw new NotImplementedException();
        }

        public override void Init()
        {
            EventName = "AddComponentEvent";
            base.Init();
        }
    }
}