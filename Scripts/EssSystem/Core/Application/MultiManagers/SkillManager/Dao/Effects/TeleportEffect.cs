using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 瞬移效果 —— 把施法者瞬间挪到目标位置。
    /// <list type="bullet">
    /// <item><see cref="UseAbsolutePosition"/>=true：直接到 <see cref="SkillEffectContext.Position"/>（PointTarget 模式）。</item>
    /// <item>=false：沿 <see cref="SkillEffectContext.Direction"/> 偏移 <see cref="Distance"/> 单位（Directional 模式）。</item>
    /// </list>
    /// 同时同步 <c>CharacterRoot.position</c> 和 <c>Entity.WorldPosition</c>，避免逻辑/物理位置脱钩。
    /// </summary>
    public class TeleportEffect : ISkillEffect
    {
        public bool UseAbsolutePosition;

        /// <summary>相对位移距离（Directional 模式）。</summary>
        public float Distance = 5f;

        /// <summary>每级额外距离。</summary>
        public float DistancePerLevel;

        public TeleportEffect() { }

        public TeleportEffect(float distance, float distancePerLevel = 0f, bool useAbsolutePosition = false)
        {
            Distance = distance;
            DistancePerLevel = distancePerLevel;
            UseAbsolutePosition = useAbsolutePosition;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var caster = ctx?.Caster;
            if (caster == null) return;

            Vector3 targetPos;
            if (UseAbsolutePosition && ctx.Position != Vector3.zero)
            {
                targetPos = ctx.Position;
            }
            else
            {
                var dist = Distance + DistancePerLevel * (ctx.Level - 1);
                var dir = ctx.Direction;
                if (dir.sqrMagnitude < 0.001f)
                {
                    var face = caster.CharacterRoot != null && caster.CharacterRoot.localScale.x < 0f ? -1f : 1f;
                    dir = new Vector3(face, 0f, 0f);
                }
                else
                {
                    dir = dir.normalized;
                }
                targetPos = (caster.CharacterRoot != null ? caster.CharacterRoot.position : caster.WorldPosition)
                            + dir * dist;
            }

            // 同步逻辑 + 物理位置（横版 RB 也要立即跟，防止下一帧被 ParallaxLayer / 摄像机拉错位）
            caster.WorldPosition = targetPos;
            if (caster.CharacterRoot != null)
            {
                caster.CharacterRoot.position = targetPos;
                var rb = caster.CharacterRoot.GetComponent<Rigidbody2D>();
                if (rb != null) rb.velocity = Vector2.zero;
            }
        }
    }
}
