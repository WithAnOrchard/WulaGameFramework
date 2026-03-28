using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //播放BGM事件 params: audioSourceId, soundKey, volume
    public class PlayBGMEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 3) return;

            var audioSourceId = o[0].ToString();
            var soundKey = o[1].ToString();
            var volume = float.Parse(o[2].ToString());

            AudioService.Instance.PlayBGM(audioSourceId, soundKey, volume);
        }

        public override void Init()
        {
            EventName = "PlayBGMEvent";
            base.Init();
        }
    }
}