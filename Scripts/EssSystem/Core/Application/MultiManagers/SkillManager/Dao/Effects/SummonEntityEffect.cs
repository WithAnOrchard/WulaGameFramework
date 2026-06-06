using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class SummonEntityEffect : ISkillEffect
    {
        public string ConfigId;
        public int Count = 1;
        public int CountPerLevel;
        public float Radius = 1.5f;
        public float YOffset;
        public string InstanceIdPrefix = "summon";

        public SummonEntityEffect() { }
        public SummonEntityEffect(string configId, int count = 1, float radius = 1.5f,
            int countPerLevel = 0, float yOffset = 0f, string instanceIdPrefix = "summon")
        {
            ConfigId = configId;
            Count = Mathf.Max(1, count);
            CountPerLevel = countPerLevel;
            Radius = radius;
            YOffset = yOffset;
            InstanceIdPrefix = instanceIdPrefix;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ConfigId) || !EventProcessor.HasInstance) return;
            var center = ctx.Position != Vector3.zero ? ctx.Position : SkillEntityProxy.Position(ctx.CasterId);
            center.y += YOffset;
            var total = Count + CountPerLevel * (ctx.Level - 1);
            if (total <= 0) return;

            var stamp = (long)(Time.time * 1000f);
            for (var i = 0; i < total; i++)
            {
                Vector3 spawnPos;
                if (total == 1) spawnPos = center;
                else
                {
                    var angle = (Mathf.PI * 2f) * i / total;
                    spawnPos = center + new Vector3(Mathf.Cos(angle) * Radius, Mathf.Sin(angle) * Radius * 0.3f, 0f);
                }

                EventProcessor.Instance.TriggerEventMethod("CreateEntity",
                    new List<object> { ConfigId, $"{InstanceIdPrefix}_{stamp}_{i}", null, spawnPos });
            }
        }
    }
}
