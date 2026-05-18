using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 召唤实体效果 —— 围绕施法者位置生成 <see cref="Count"/> 个 <see cref="ConfigId"/> 实体。
    /// <list type="bullet">
    /// <item>走 <see cref="EntityManager.EVT_CREATE_ENTITY"/> 标准入口（bare-string 兼容），生成的实体由 EntityService 完整管理。</item>
    /// <item>位置围绕施法者 <see cref="SkillEffectContext.Position"/>（若为 0 则取 caster.WorldPosition），
    ///   按等角度环形分布在 <see cref="Radius"/> 半径上；只有 1 个时直接在中心点。</item>
    /// <item>实例 ID 由 <see cref="InstanceIdPrefix"/> + 时间戳 + index 组成，避免冲突。</item>
    /// </list>
    /// <para>典型用法：法师召唤骷髅、史莱姆分裂、图腾召唤怪物。</para>
    /// </summary>
    public class SummonEntityEffect : ISkillEffect
    {
        /// <summary>EntityService 已注册的 EntityConfig Id（如 "skeleton", "slime"）。</summary>
        public string ConfigId;

        /// <summary>本次召唤数量（不算等级加成）。</summary>
        public int Count = 1;

        /// <summary>每级额外召唤数（向上取整）。</summary>
        public int CountPerLevel;

        /// <summary>环形分布半径（世界单位）。Count=1 时忽略。</summary>
        public float Radius = 1.5f;

        /// <summary>Y 偏移（地面之上）。</summary>
        public float YOffset;

        /// <summary>实例 ID 前缀。</summary>
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
            if (string.IsNullOrEmpty(ConfigId) || !EventProcessor.HasInstance) return;
            if (ctx?.Caster == null) return;

            var center = ctx.Position != Vector3.zero ? ctx.Position : ctx.Caster.WorldPosition;
            center.y += YOffset;

            var total = Count + CountPerLevel * (ctx.Level - 1);
            if (total <= 0) return;

            var stamp = (long)(Time.time * 1000f);
            for (var i = 0; i < total; i++)
            {
                Vector3 spawnPos;
                if (total == 1)
                {
                    spawnPos = center;
                }
                else
                {
                    var angle = (Mathf.PI * 2f) * i / total;
                    spawnPos = center + new Vector3(
                        Mathf.Cos(angle) * Radius, Mathf.Sin(angle) * Radius * 0.3f, 0f);
                    // Y 方向乘 0.3 → 椭圆分布，更适合横版（避免叠在头顶）
                }

                var instanceId = $"{InstanceIdPrefix}_{stamp}_{i}";
                EventProcessor.Instance.TriggerEventMethod(
                    EntityManager.EVT_CREATE_ENTITY,
                    new List<object> { ConfigId, instanceId, null, spawnPos });
            }
        }
    }
}
