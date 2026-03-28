using System.Collections.Generic;
using EssSystem.Core.EventManager;
using EssSystem.EssManager.UIManager.Dao;

namespace EssSystem.EssManager.UIManager.Events
{
    //注册一个Canvas数据到Service并加载
    public class RegisterCanvasEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count < 1) return;

            var canvas = o[0] as UICanvas;
            if (canvas == null) return;

            var parent = UIManager.Instance.transform;
            UIService.Instance.LoadCanvas(canvas, parent);
        }

        public override void Init()
        {
            EventName = "RegisterCanvasEvent";
            base.Init();
        }
    }
}