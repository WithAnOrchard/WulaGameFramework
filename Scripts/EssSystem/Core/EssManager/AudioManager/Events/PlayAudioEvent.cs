using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //播放音频事件 params: audioSourceId, soundKey, volume[, pitch]
    public class PlayAudioEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 3) return;

            var audioSourceId = o[0].ToString();
            var soundKey = o[1].ToString();
            var volume = float.Parse(o[2].ToString());
            var pitch = o.Count > 3 ? float.Parse(o[3].ToString()) : 1f;

            AudioService.Instance.PlaySound(audioSourceId, soundKey, volume, pitch);
        }

        public override void Init()
        {
            EventName = "PlayAudioEvent";
            base.Init();
        }
    }
}