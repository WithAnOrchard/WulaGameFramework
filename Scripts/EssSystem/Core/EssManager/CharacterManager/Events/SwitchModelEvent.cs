using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace CharacterManager.Events
{
    //切换模型事件 params: direction ("next" 或 "prev")
    public class SwitchModelEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 1) return;

            var direction = o[0].ToString();
            if (direction == "next")
                CharacterService.Instance.NextModel();
            else
                CharacterService.Instance.PrevModel();
        }

        public override void Init()
        {
            EventName = "SwitchModelEvent";
            base.Init();
        }
    }
}