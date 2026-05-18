using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 多重射击 / 扇形投射 —— 一次性发射 <see cref="ProjectileCount"/> 个 <see cref="SkillProjectile"/>，
    /// 在 <see cref="SpreadAngleDeg"/> 总角度内均分。
    /// <list type="bullet">
    /// <item>典型用法：游侠的"三连射"、巫师的"火球散射"。</item>
    /// <item>奇数枚时中间一枚正对方向，偶数枚则左右对称（中线两侧各一半）。</item>
    /// </list>
    /// </summary>
    public class MultiShotEffect : ISkillEffect
    {
        public int ProjectileCount = 3;
        public float SpreadAngleDeg = 30f;
        public float Speed = 12f;
        public float Damage = 6f;
        public float DamagePerLevel;
        public float Radius = 0.3f;
        public float MaxLifetime = 3f;
        public bool Pierce;
        public string DamageType = "projectile";

        public MultiShotEffect() { }

        public MultiShotEffect(int projectileCount, float spreadAngleDeg, float speed, float damage,
            float damagePerLevel = 0f, bool pierce = false, string damageType = "projectile")
        {
            ProjectileCount = Mathf.Max(1, projectileCount);
            SpreadAngleDeg = spreadAngleDeg;
            Speed = speed;
            Damage = damage;
            DamagePerLevel = damagePerLevel;
            Pierce = pierce;
            DamageType = damageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var caster = ctx?.Caster;
            if (caster?.CharacterRoot == null) return;

            // 基准方向
            Vector2 baseDir;
            if (ctx.Direction.sqrMagnitude > 0.001f)
            {
                baseDir = ((Vector2)ctx.Direction).normalized;
            }
            else
            {
                baseDir = caster.CharacterRoot.localScale.x >= 0f ? Vector2.right : Vector2.left;
            }

            var damage = Damage + DamagePerLevel * (ctx.Level - 1);
            var startAngle = -SpreadAngleDeg * 0.5f;
            var step = ProjectileCount > 1 ? SpreadAngleDeg / (ProjectileCount - 1) : 0f;
            var origin = caster.CharacterRoot.position;

            for (var i = 0; i < ProjectileCount; i++)
            {
                var angle = startAngle + step * i;
                var rad = angle * Mathf.Deg2Rad;
                var cos = Mathf.Cos(rad);
                var sin = Mathf.Sin(rad);
                // 2D 旋转
                var dir = new Vector2(baseDir.x * cos - baseDir.y * sin, baseDir.x * sin + baseDir.y * cos);

                var go = new GameObject($"Projectile_{ctx.Definition?.Id}_{i}");
                go.transform.position = origin;
                var p = go.AddComponent<SkillProjectile>();
                p.Caster = caster;
                p.Velocity = (Vector3)(dir * Speed);
                p.Damage = damage;
                p.DamageType = DamageType;
                p.Radius = Radius;
                p.MaxLifetime = MaxLifetime;
                p.Pierce = Pierce;
            }
        }
    }
}
