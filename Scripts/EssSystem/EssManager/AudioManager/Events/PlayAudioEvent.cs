using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    public class PlayAudioEvent : TriggerEvent
    {

        public override void Action(List<object> o)
        {
            if (o.Count == 3)
            {
                // AudioManager.Instance.PlaySound(o[0].ToString(),o[1].ToString(),int.Parse(o[2].ToString()),false);
            }
            else
            {
                // AudioManager.Instance.PlaySound(o[0].ToString(),o[1].ToString(),int.Parse(o[2].ToString()),true);
            }
           
           
        }

        public override void Init()
        {
            EventName="PlayAudioEvent";
            base.Init();
        } 
    }
}