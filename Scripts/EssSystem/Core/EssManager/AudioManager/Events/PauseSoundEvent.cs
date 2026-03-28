using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //暂停音频事件 params: audioSourceId
    public class PauseSoundEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 1) return;
            AudioService.Instance.PauseSound(o[0].ToString());
        }

        public override void Init()
        {
            EventName = "PauseSoundEvent";
            base.Init();
        }
    }
}