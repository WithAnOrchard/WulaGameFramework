using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //创建音频源事件 params: audioSourceId
    public class CreateAudioSourceEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 1) return;

            var audioSourceId = o[0].ToString();
            var parent = AudioManager.Instance.transform;
            AudioService.Instance.CreateAudioSource(audioSourceId, parent);
        }

        public override void Init()
        {
            EventName = "CreateAudioSourceEvent";
            base.Init();
        }
    }
}