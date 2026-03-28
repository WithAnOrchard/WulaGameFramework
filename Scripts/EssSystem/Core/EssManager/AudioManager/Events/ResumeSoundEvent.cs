using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //恢复音频事件 params: audioSourceId
    public class ResumeSoundEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 1) return;
            AudioService.Instance.ResumeSound(o[0].ToString());
        }

        public override void Init()
        {
            EventName = "ResumeSoundEvent";
            base.Init();
        }
    }
}