using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;

namespace EssSystem.Core.EssManagers.Gameplay.SkillManager.Dao.Effects
{
    /// <summary>
    /// 范围效果 —— 在指定位置检测半径内所有实体，对每个执行子效果链。
    /// </summary>
    public class AoeEffect : ISkillEffect
    {
        public float Radius;
        public float RadiusPerLevel;

        /// <summary>对范围内每个目标执行的子效果。</summary>
        public List<ISkillEffect> SubEffects = new();

        /// <summary>是否包含施法者自身。</summary>
        public bool IncludeSelf;

        private static readonly Collider2D[] _buffer = new Collider2D[32];

        public AoeEffect(float radius, float radiusPerLevel = 0f, bool includeSelf = false)
        {
            Radius = radius;
            RadiusPerLevel = radiusPerLevel;
            IncludeSelf = includeSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var r = Radius + RadiusPerLevel * (ctx.Level - 1);
            var center = ctx.Position != Vector3.zero ? (Vector2)ctx.Position : (Vector2)ctx.Caster.WorldPosition;

            var count = Physics2D.OverlapCircleNonAlloc(center, r, _buffer);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle == null || handle.Entity == null) continue;
                if (!IncludeSelf && handle.Entity == ctx.Caster) continue;

                // 为每个目标创建子上下文，执行子效果链
                var subCtx = new SkillEffectContext
                {
                    Caster = ctx.Caster,
                    Target = handle.Entity,
                    Definition = ctx.Definition,
                    Instance = ctx.Instance,
                    Direction = ctx.Direction,
                    Position = ctx.Position,
                };
                foreach (var effect in SubEffects)
                    effect.Apply(subCtx);
            }
        }
    }
}
