using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 投射物效果 —— 在施法者位置生成 <see cref="SkillProjectile"/>，沿 <see cref="SkillEffectContext.Direction"/>
    /// 直线飞行；命中带 EntityHandle 的实体即调 <c>EntityService.TryDamage</c>。
    /// <list type="bullet">
    /// <item>无 Sprite：调用方按需追加视觉子节点（colors / icons / 拖尾）。如需可视化，可在外层包一个 BuildXxx + 自挂 SpriteRenderer。</item>
    /// <item><see cref="Pierce"/>=true：穿透多个目标各结算一次。</item>
    /// </list>
    /// </summary>
    public class ProjectileEffect : ISkillEffect
    {
        public float Speed = 12f;
        public float Damage = 8f;
        public float DamagePerLevel;
        public string DamageType = "projectile";

        public float Radius = 0.3f;
        public float MaxLifetime = 4f;
        public bool Pierce;

        public ProjectileEffect() { }

        public ProjectileEffect(float speed, float damage, float damagePerLevel = 0f,
            string damageType = "projectile", float radius = 0.3f, float maxLifetime = 4f, bool pierce = false)
        {
            Speed = speed;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            DamageType = damageType;
            Radius = radius;
            MaxLifetime = maxLifetime;
            Pierce = pierce;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var caster = ctx?.Caster;
            if (caster?.CharacterRoot == null) return;

            var dirX = Mathf.Abs(ctx.Direction.x) > 0.01f
                ? Mathf.Sign(ctx.Direction.x)
                : (caster.CharacterRoot.localScale.x >= 0f ? 1f : -1f);
            var dir = ctx.Direction.sqrMagnitude > 0.001f
                ? ctx.Direction.normalized
                : new Vector3(dirX, 0f, 0f);

            var go = new GameObject($"Projectile_{ctx.Definition?.Id}");
            go.transform.position = caster.CharacterRoot.position;
            var p = go.AddComponent<SkillProjectile>();
            p.Caster = caster;
            p.Velocity = dir * Speed;
            p.Damage = Damage + DamagePerLevel * (ctx.Level - 1);
            p.DamageType = DamageType;
            p.Radius = Radius;
            p.MaxLifetime = MaxLifetime;
            p.Pierce = Pierce;
        }
    }
}
