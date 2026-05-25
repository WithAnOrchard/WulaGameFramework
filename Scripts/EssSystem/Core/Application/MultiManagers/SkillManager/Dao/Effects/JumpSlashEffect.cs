using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 跳斩效果 —— "起跳 → 滞空 → 落地砸地"三段动作，全部走 SkillManager 的 BuffInstance 调度，无需额外 MonoBehaviour。
    /// <list type="bullet">
    /// <item>立即给施法者一次向上 + 朝向方向的小幅水平推进（<see cref="JumpUp"/>, <see cref="JumpForward"/>）。</item>
    /// <item>滞空 <see cref="AirTime"/> 秒；BuffInstance 到期触发"砸地"：把 caster 强制下砸（<see cref="SlamDownVelocity"/>），
    ///   以 caster 当前位置为中心做 <see cref="ImpactRadius"/> AOE 伤害（基础值 <see cref="BaseDamage"/>，按等级 +<see cref="DamagePerLevel"/>）。</item>
    /// <item>排除施法者自身。命中走 <see cref="EntityService.TryDamage"/> 标准管线（受 IInvulnerable / DamageReduction 影响）。</item>
    /// </list>
    /// <para>无状态，可单例共享给多个 SkillDefinition。</para>
    /// </summary>
    public class JumpSlashEffect : ISkillEffect
    {
        public const string BUFF_JUMP_SLASH = "jump_slash_pending";

        public float JumpUp = 9f;
        public float JumpForward = 4f;
        public float AirTime = 0.45f;

        public float SlamDownVelocity = -16f;
        public float ImpactRadius = 2.5f;
        public float BaseDamage = 12f;
        public float DamagePerLevel;
        public string DamageType = "jump_slash";

        private static readonly Collider2D[] _buffer = new Collider2D[32];

        public JumpSlashEffect() { }

        public JumpSlashEffect(float jumpUp, float jumpForward, float airTime,
            float slamDownVelocity, float impactRadius, float baseDamage,
            float damagePerLevel = 0f, string damageType = "jump_slash")
        {
            JumpUp = jumpUp;
            JumpForward = jumpForward;
            AirTime = airTime;
            SlamDownVelocity = slamDownVelocity;
            ImpactRadius = impactRadius;
            BaseDamage = baseDamage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
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

            // ① 起跳 + 向前推
            rb.linearVelocity = new Vector2(dirX * JumpForward, JumpUp);

            // ② 滞空：BuffInstance 到期触发砸地（OnExpire 调度）
            if (!SkillService.HasInstance) return;
            var caster = ctx.Caster;
            var level = ctx.Level;
            var damage = BaseDamage + DamagePerLevel * (level - 1);
            var radius = ImpactRadius;
            var slam = SlamDownVelocity;
            var dmgType = DamageType;

            SkillService.Instance.ApplyBuff(caster, new BuffInstance
            {
                BuffId = BUFF_JUMP_SLASH,
                Source = caster,
                Target = caster,
                Duration = Mathf.Max(0.05f, AirTime),
                OnExpire = _ => Slam(caster, rb, root, slam, radius, damage, dmgType),
            });
        }

        private static void Slam(Entity caster, Rigidbody2D rb, Transform root,
            float slamVelocity, float radius, float damage, string damageType)
        {
            if (rb == null || root == null) return;

            // 强制砸地：横向归零，竖直给一个大向下速度
            rb.linearVelocity = new Vector2(0f, slamVelocity);

            // AOE 伤害：以当前位置为中心
            if (!EntityService.HasInstance || damage <= 0f) return;
            var center = (Vector2)root.position;
            var count = Physics2D.OverlapCircleNonAlloc(center, radius, _buffer);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle == null || handle.Entity == null) continue;
                if (handle.Entity == caster) continue;
                var dmg = handle.Entity.Get<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;
                EntityService.Instance.TryDamage(handle.Entity, damage,
                    source: caster, damageType: damageType, damageSourcePosition: center);
            }
        }
    }
}
