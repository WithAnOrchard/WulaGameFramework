using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;
using Demo.Tribe.Resource;

namespace Demo.Tribe.Entities
{
    /// <summary>
    /// 接触伤害模块 —— OnTriggerStay2D / OnCollisionStay2D 时反查 <see cref="EntityHandle"/>，
    /// 通过 <c>handle.TakeDamage</c> 走框架统一伤害流水线（含无敌拦截 + 闪烁 + 击退 + 音效）。
    /// <para>不再直接依赖 <c>TribePlayer</c> —— 任何带 EntityHandle + IDamageable 的实体均可受伤。</para>
    /// <para>需挂在带 <see cref="Collider2D"/> + isTrigger 的同一 GameObject 上。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeCreatureContactDamager : MonoBehaviour
    {
        [SerializeField, Min(0f)]   private float _contactDamage = 8f;
        [SerializeField, Min(0.1f)] private float _damageCooldown = 1f;

        private float _nextDamageTime;

        /// <summary>外部可关闭：例如敌人死亡时停止接触伤害。</summary>
        public bool Enabled { get; set; } = true;

        public void Configure(float damage, float cooldown)
        {
            _contactDamage = Mathf.Max(0f, damage);
            _damageCooldown = Mathf.Max(0.1f, cooldown);
        }

        private void OnTriggerStay2D(Collider2D other)            => TryHit(other);

        // 重力模式下 collider 是 solid，OnTriggerStay2D 不触发 → 走 OnCollisionStay2D。
        private void OnCollisionStay2D(Collision2D collision) => TryHit(collision.collider);

        private void TryHit(Collider2D other)
        {
            if (!Enabled || other == null || Time.time < _nextDamageTime) return;

            // 采集物只允许玩家主动攻击触发采集，怪物接触不能砍掉资源。
            if (other.GetComponentInParent<PickableDropEntity>() != null) return;

            // 怪物之间不互相伤害：对方挂了 TribeCreatureContactDamager（即也是怪物）则跳过
            var otherDamager = other.GetComponentInParent<TribeCreatureContactDamager>();
            if (otherDamager != null && otherDamager != this) return;

            var handle = other.GetComponentInParent<EntityHandle>();
            if (handle == null || !handle.CanBeAttacked) return;
            handle.TakeDamage(_contactDamage, "EnemyContact", transform.position);
            _nextDamageTime = Time.time + _damageCooldown;
        }
    }
}
