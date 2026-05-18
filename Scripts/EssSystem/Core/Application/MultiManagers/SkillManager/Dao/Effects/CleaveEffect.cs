using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 锥形挥砍 / 喷吐 —— 以施法者为圆心，<see cref="Range"/> 为半径，<see cref="HalfAngleDeg"/> 为半锥角，
    /// 对面朝方向 / <see cref="SkillEffectContext.Direction"/> 内的所有敌人执行 <see cref="SubEffects"/>。
    /// <list type="bullet">
    /// <item>角度判定：dot(direction, casterToTarget) ≥ cos(HalfAngleDeg)。</item>
    /// <item>未指定 <see cref="SkillEffectContext.Direction"/> 时取 CharacterRoot.localScale.x 的朝向。</item>
    /// <item>典型用法：剑士的"横扫千军"、龙的"火焰吐息"、宝可梦的"叶刃"等。</item>
    /// </list>
    /// </summary>
    public class CleaveEffect : ISkillEffect
    {
        public float Range = 3f;
        public float HalfAngleDeg = 45f;

        /// <summary>对锥内每个目标执行的子效果（与 AoeEffect 同理）。</summary>
        public List<ISkillEffect> SubEffects = new();

        public bool IncludeSelf;

        private static readonly Collider2D[] _buffer = new Collider2D[32];

        public CleaveEffect() { }

        public CleaveEffect(float range, float halfAngleDeg = 45f, bool includeSelf = false)
        {
            Range = range;
            HalfAngleDeg = halfAngleDeg;
            IncludeSelf = includeSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Caster?.CharacterRoot == null) return;
            var root = ctx.Caster.CharacterRoot;
            var origin = (Vector2)root.position;

            // 朝向：ctx.Direction 优先，其次面朝
            Vector2 dir;
            if (ctx.Direction.sqrMagnitude > 0.001f)
            {
                dir = ((Vector2)ctx.Direction).normalized;
            }
            else
            {
                dir = root.localScale.x >= 0f ? Vector2.right : Vector2.left;
            }

            var cosThreshold = Mathf.Cos(HalfAngleDeg * Mathf.Deg2Rad);
            var count = Physics2D.OverlapCircleNonAlloc(origin, Range, _buffer);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle?.Entity == null) continue;
                if (!IncludeSelf && handle.Entity == ctx.Caster) continue;

                var toTarget = ((Vector2)handle.Entity.WorldPosition - origin);
                if (toTarget.sqrMagnitude < 0.0001f) continue;
                if (Vector2.Dot(dir, toTarget.normalized) < cosThreshold) continue;

                var subCtx = new SkillEffectContext
                {
                    Caster = ctx.Caster,
                    Target = handle.Entity,
                    Definition = ctx.Definition,
                    Instance = ctx.Instance,
                    Direction = ctx.Direction,
                    Position = ctx.Position,
                };
                for (var s = 0; s < SubEffects.Count; s++)
                    SubEffects[s].Apply(subCtx);
            }
        }
    }
}
