using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 链式闪电 —— 从首个目标（<see cref="SkillEffectContext.Target"/> 或施法者周围最近敌人）出发，
    /// 在 <see cref="JumpRadius"/> 半径内寻找下一个未被击中的目标，反复跳跃 <see cref="MaxJumps"/> 次。
    /// <list type="bullet">
    /// <item>每跳伤害衰减：<see cref="BaseDamage"/> × <see cref="FalloffPerJump"/>^n。</item>
    /// <item>每个目标只命中一次，跳完即结束（即使中途断链）。</item>
    /// <item>瞬时结算，无飞行物 / 无 Buff —— 适合"立刻看到效果"的法术爆发节奏。</item>
    /// </list>
    /// </summary>
    public class ChainLightningEffect : ISkillEffect
    {
        public float BaseDamage = 15f;
        public float DamagePerLevel;
        public int MaxJumps = 4;
        public float JumpRadius = 4f;
        public float FalloffPerJump = 0.8f;
        public string DamageType = "lightning";

        private static readonly Collider2D[] _buffer = new Collider2D[32];

        public ChainLightningEffect() { }

        public ChainLightningEffect(float baseDamage, int maxJumps = 4, float jumpRadius = 4f,
            float falloffPerJump = 0.8f, float damagePerLevel = 0f, string damageType = "lightning")
        {
            BaseDamage = baseDamage;
            MaxJumps = maxJumps;
            JumpRadius = jumpRadius;
            FalloffPerJump = falloffPerJump;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (!EntityService.HasInstance || ctx?.Caster == null) return;

            // 起跳目标：优先 ctx.Target；否则取施法者周围最近的非己 IDamageable
            var first = ctx.Target ?? PickNearest(ctx.Caster);
            if (first == null) return;

            var baseDmg = BaseDamage + DamagePerLevel * (ctx.Level - 1);
            var hit = new HashSet<Entity> { ctx.Caster };
            var current = first;
            for (var i = 0; i < MaxJumps && current != null; i++)
            {
                if (!hit.Add(current)) break;
                var dmg = current.Get<IDamageable>();
                if (dmg == null || dmg.IsDead) break;

                var damage = baseDmg * Mathf.Pow(Mathf.Clamp01(FalloffPerJump), i);
                EntityService.Instance.TryDamage(current, damage,
                    source: ctx.Caster, damageType: DamageType,
                    damageSourcePosition: current.CharacterRoot != null
                        ? (Vector3?)current.CharacterRoot.position : null);

                current = PickNextJump(current, hit);
            }
        }

        private Entity PickNearest(Entity caster)
        {
            if (caster.CharacterRoot == null) return null;
            var origin = (Vector2)caster.CharacterRoot.position;
            var count = Physics2D.OverlapCircleNonAlloc(origin, JumpRadius, _buffer);
            Entity best = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle?.Entity == null || handle.Entity == caster) continue;
                var d = handle.Entity.Get<IDamageable>();
                if (d == null || d.IsDead) continue;
                var dist = ((Vector2)handle.Entity.WorldPosition - origin).sqrMagnitude;
                if (dist < bestDist) { bestDist = dist; best = handle.Entity; }
            }
            return best;
        }

        private Entity PickNextJump(Entity from, HashSet<Entity> hit)
        {
            if (from.CharacterRoot == null) return null;
            var origin = (Vector2)from.CharacterRoot.position;
            var count = Physics2D.OverlapCircleNonAlloc(origin, JumpRadius, _buffer);
            Entity best = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle?.Entity == null || hit.Contains(handle.Entity)) continue;
                var d = handle.Entity.Get<IDamageable>();
                if (d == null || d.IsDead) continue;
                var dist = ((Vector2)handle.Entity.WorldPosition - origin).sqrMagnitude;
                if (dist < bestDist) { bestDist = dist; best = handle.Entity; }
            }
            return best;
        }
    }
}
