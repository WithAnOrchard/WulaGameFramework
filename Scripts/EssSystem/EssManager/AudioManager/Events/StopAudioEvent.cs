using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.AudioManager.Events
{
    public class StopAudioEvent: TriggerEvent
    {

        public override void Action(List<object> o)
        {
            if(o.Count!=1){return;}
            // EssManager.AudioManager.AudioManager.Instance.StopSound(o[0].ToString());
           
        }

        public override void Init()
        {
            EventName="StopAudioEvent";
            base.Init();
        } 
    }
}