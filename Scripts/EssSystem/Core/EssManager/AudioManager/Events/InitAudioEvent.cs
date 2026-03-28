using System.Collections.Generic;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager.Events
{
    //初始化音频系统事件
    public class InitAudioEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            //确保AudioService实例已创建并DataSpaces初始化
            var _ = AudioService.Instance;
        }

        public override void Init()
        {
            EventName = "InitAudioEvent";
            base.Init();
        }
    }
}