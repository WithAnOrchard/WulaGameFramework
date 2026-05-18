using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 击退效果 —— 给目标 Rigidbody2D 一次冲量；远离施法者方向。
    /// <list type="bullet">
    /// <item>方向：从施法者指向目标的水平向量（X-only，避免击飞过头）。</item>
    /// <item>力度：<see cref="Force"/> + 等级缩放。</item>
    /// <item>UpKick：竖直附加速度（小幅抬高，看起来更"飞起来"）。</item>
    /// </list>
    /// 不调 TryDamage —— 与 <see cref="DamageEffect"/> 配套使用（先伤害后击退）。
    /// </summary>
    public class KnockbackEffect : ISkillEffect
    {
        public float Force = 10f;
        public float ForcePerLevel;
        public float UpKick = 3f;

        public KnockbackEffect() { }

        public KnockbackEffect(float force, float upKick = 3f, float forcePerLevel = 0f)
        {
            Force = force;
            UpKick = upKick;
            ForcePerLevel = forcePerLevel;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx?.Target?.CharacterRoot == null) return;
            var rb = ctx.Target.CharacterRoot.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            var casterPos = ctx.Caster != null && ctx.Caster.CharacterRoot != null
                ? ctx.Caster.CharacterRoot.position : ctx.Position;
            var dx = ctx.Target.CharacterRoot.position.x - casterPos.x;
            var sign = dx >= 0f ? 1f : -1f;

            var force = Force + ForcePerLevel * (ctx.Level - 1);
            rb.velocity = new Vector2(sign * force, Mathf.Max(rb.velocity.y, UpKick));
        }
    }
}
