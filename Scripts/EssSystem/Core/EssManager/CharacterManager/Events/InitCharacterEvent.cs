using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace CharacterManager.Events
{
    public class InitCharacterEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            CharacterService.Instance.InitAll();
        }

        public override void Init()
        {
            EventName = "InitCharacterEvent";
            base.Init();
        }
    }
}