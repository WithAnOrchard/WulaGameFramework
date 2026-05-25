using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 冲刺效果 —— 给施法者一次水平冲刺速度，可选 <see cref="InvulnerableDuration"/> 期间暂时无敌。
    /// <list type="bullet">
    /// <item>方向：优先 <see cref="SkillEffectContext.Direction"/>.x；若为 0 退回 CharacterRoot.localScale.x 朝向。</item>
    /// <item>速度：直接写 <c>Rigidbody2D.velocity = (dirX × Speed, KeepVerticalVelocity ? vy : VerticalKick)</c>。</item>
    /// <item>无敌：<see cref="InvulnerableDuration"/> &gt; 0 时通过 <see cref="SkillService.ApplyBuff"/> 挂一个零 Tick Buff，
    ///   过期回调还原 <see cref="DamageableComponent.DamageReduction"/> 至原值。</item>
    /// </list>
    /// <para><b>无状态</b>：参数全在字段里，单例可被多施法者共享。</para>
    /// </summary>
    public class DashEffect : ISkillEffect
    {
        public const string BUFF_DASH_INVUL = "dash_invulnerable";

        /// <summary>冲刺水平速度（绝对值）。</summary>
        public float Speed;

        /// <summary>等级加速：每级 +N。</summary>
        public float SpeedPerLevel;

        /// <summary>true = 保留原 Rigidbody2D.velocity.y（重力依然下落）；false = 用 <see cref="VerticalKick"/> 强制写入。</summary>
        public bool KeepVerticalVelocity = true;

        /// <summary>仅在 <see cref="KeepVerticalVelocity"/>=false 时生效。</summary>
        public float VerticalKick;

        /// <summary>冲刺期间无敌时长（秒）；0 = 不给无敌帧。
        /// 实现走 <see cref="DamageableComponent.DamageReduction"/> 满减伤（=1）。</summary>
        public float InvulnerableDuration;

        public DashEffect(float speed, float speedPerLevel = 0f, float invulDuration = 0f,
            bool keepVerticalVelocity = true, float verticalKick = 0f)
        {
            Speed = speed;
            SpeedPerLevel = speedPerLevel;
            InvulnerableDuration = invulDuration;
            KeepVerticalVelocity = keepVerticalVelocity;
            VerticalKick = verticalKick;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var root = ctx?.Caster?.CharacterRoot;
            if (root == null) return;
            var rb = root.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            // 方向：ctx.Direction.x 优先，其次面朝
            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f
                ? Mathf.Sign(ctx.Direction.x)
                : (root.localScale.x >= 0f ? 1f : -1f);

            var speed = Speed + SpeedPerLevel * (ctx.Level - 1);
            var vy = KeepVerticalVelocity ? rb.linearVelocity.y : VerticalKick;
            rb.linearVelocity = new Vector2(dirX * speed, vy);

            // 视觉翻面（如果存在 Visual 子节点）
            var visual = root.Find("Visual");
            if (visual != null)
            {
                var s = visual.localScale;
                s.x = dirX > 0f ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
                visual.localScale = s;
            }

            // 无敌帧：通过临时改 DamageReduction 实现，OnExpire 还原
            if (InvulnerableDuration > 0f && SkillService.HasInstance)
            {
                var dmg = ctx.Caster.Get<IDamageable>() as DamageableComponent;
                if (dmg != null)
                {
                    var origReduction = dmg.DamageReduction;
                    dmg.DamageReduction = 1f;
                    SkillService.Instance.ApplyBuff(ctx.Caster, new BuffInstance
                    {
                        BuffId = BUFF_DASH_INVUL,
                        Source = ctx.Caster,
                        Target = ctx.Caster,
                        Duration = InvulnerableDuration,
                        OnExpire = _ => { if (dmg != null) dmg.DamageReduction = origReduction; },
                    });
                }
            }
        }
    }
}
