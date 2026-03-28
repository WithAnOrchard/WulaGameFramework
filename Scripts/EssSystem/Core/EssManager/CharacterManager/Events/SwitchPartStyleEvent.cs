using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace CharacterManager.Events
{
    //切换部位样式事件 params: partIndex, direction("next"或"prev")
    public class SwitchPartStyleEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 2) return;

            var partIndex = int.Parse(o[0].ToString());
            var direction = o[1].ToString();
            var next = direction == "next";

            CharacterService.Instance.SwitchPartStyle(partIndex, next);
        }

        public override void Init()
        {
            EventName = "SwitchPartStyleEvent";
            base.Init();
        }
    }
}