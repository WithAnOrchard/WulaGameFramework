using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.EventManager;
using UnityEngine;

namespace EssSystem.EssEvents
{
    public class ExchangeSpriteEvent : TriggerEvent
    {
        public override void Action(List<object> o)
        {
            if (o.Count != 2) return;
            var target = GameObject.Find(o[0] as string);
            var spriteRenderer = target.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) return;
            spriteRenderer.sprite = ResourceLoaderManager.Instance.GetSprite(o[1] as string);
        }

        public override void Init()
        {
            EventName = "ExchangeSpriteEvent";
            base.Init();
        }
    }
}