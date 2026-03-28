using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //停止音频事件 params: audioSourceId
    public class StopAudioEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 1) return;
            AudioService.Instance.StopSound(o[0].ToString());
        }

        public override void Init()
        {
            EventName = "StopAudioEvent";
            base.Init();
        }
    }
}