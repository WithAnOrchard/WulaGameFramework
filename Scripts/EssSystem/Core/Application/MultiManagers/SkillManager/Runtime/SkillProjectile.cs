using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Runtime
{
    /// <summary>
    /// 投射物运行时 —— 由 <see cref="Dao.Effects.ProjectileEffect"/> 创建：
    /// 沿初始 <see cref="Velocity"/> 直线飞行，碰到带 <see cref="EntityHandle"/> 的目标即结算伤害然后销毁。
    /// <list type="bullet">
    /// <item>纯 Update 推进 transform，避免依赖 Rigidbody2D；穿透墙体（业务可自行加 LayerMask）。</item>
    /// <item><see cref="MaxLifetime"/> 秒后自动销毁，避免漏出场。</item>
    /// <item>命中过滤：跳过 <see cref="Caster"/>，命中后只结算一次（<see cref="Pierce"/>=true 时穿透继续）。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillProjectile : MonoBehaviour
    {
        public Entity Caster;
        public Vector3 Velocity;
        public float Damage;
        public string DamageType = "projectile";
        public float Radius = 0.3f;
        public float MaxLifetime = 4f;
        public bool Pierce;

        private float _aliveTime;
        private System.Collections.Generic.HashSet<Entity> _hit;

        private void Update()
        {
            transform.position += Velocity * Time.deltaTime;

            _aliveTime += Time.deltaTime;
            if (_aliveTime >= MaxLifetime) { Destroy(gameObject); return; }

            // 命中检测：以本体位置为圆心
            var hits = Physics2D.OverlapCircleAll(transform.position, Radius);
            for (var i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle == null || handle.Entity == null) continue;
                if (handle.Entity == Caster) continue;

                if (Pierce)
                {
                    _hit ??= new System.Collections.Generic.HashSet<Entity>();
                    if (!_hit.Add(handle.Entity)) continue;
                }

                var dmg = handle.Entity.Get<IDamageable>();
                if (dmg == null || dmg.IsDead) continue;
                if (EntityService.HasInstance)
                    EntityService.Instance.TryDamage(handle.Entity, Damage,
                        source: Caster, damageType: DamageType, damageSourcePosition: transform.position);

                if (!Pierce) { Destroy(gameObject); return; }
            }
        }
    }
}
