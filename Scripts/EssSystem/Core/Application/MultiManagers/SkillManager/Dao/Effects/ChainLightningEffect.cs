using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class ChainLightningEffect : ISkillEffect
    {
        public float BaseDamage = 15f;
        public float DamagePerLevel;
        public int MaxJumps = 4;
        public float JumpRadius = 4f;
        public float FalloffPerJump = 0.8f;
        public string DamageType = "lightning";

        private static readonly Collider[] _buffer = new Collider[64];

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
            if (ctx == null || string.IsNullOrEmpty(ctx.CasterId)) return;
            var first = !string.IsNullOrEmpty(ctx.TargetId) ? ctx.TargetId : PickNearest(ctx.CasterId, ctx.CasterId);
            if (string.IsNullOrEmpty(first)) return;
            var baseDmg = BaseDamage + DamagePerLevel * (ctx.Level - 1);
            var hit = new HashSet<string> { ctx.CasterId };
            var current = first;
            for (var i = 0; i < MaxJumps && !string.IsNullOrEmpty(current); i++)
            {
                if (!hit.Add(current)) break;
                if (SkillEntityProxy.IsDead(current)) break;
                var damage = baseDmg * Mathf.Pow(Mathf.Clamp01(FalloffPerJump), i);
                SkillEntityProxy.Damage(current, damage, ctx.CasterId, DamageType, SkillEntityProxy.Position(current));
                current = PickNearest(current, null, hit);
            }
        }

        private string PickNearest(string fromId, string excludeId, HashSet<string> excluded = null)
        {
            var origin = SkillEntityProxy.Position(fromId);
            var count = Physics.OverlapSphereNonAlloc(origin, JumpRadius, _buffer, ~0, QueryTriggerInteraction.Collide);
            string best = null;
            var bestDist = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var id = SkillEntityProxy.IdFrom(_buffer[i]);
                if (string.IsNullOrEmpty(id) || id == excludeId || (excluded != null && excluded.Contains(id))) continue;
                if (SkillEntityProxy.IsDead(id)) continue;
                var dist = (SkillEntityProxy.Position(id) - origin).sqrMagnitude;
                if (dist < bestDist) { bestDist = dist; best = id; }
            }
            return best;
        }
    }
}
