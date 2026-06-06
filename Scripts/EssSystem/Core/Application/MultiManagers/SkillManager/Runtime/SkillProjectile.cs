using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Runtime
{
    /// <summary>
    /// 投射物运行时 —— 沿初始 <see cref="Velocity"/> 飞行，命中可反查 entityId 的目标即结算伤害（3D 物理版）。
    /// <list type="bullet">
    /// <item>纯 Update 推进 transform（不依赖 2D Rigidbody）。</item>
    /// <item><see cref="MaxLifetime"/> 秒后自动销毁。</item>
    /// <item>命中检测：<c>Physics.OverlapSphereNonAlloc</c> 3D 球内 collider。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillProjectile : MonoBehaviour
    {
        public string CasterId;
        public Vector3 Velocity;
        public float Damage;
        public string DamageType = "projectile";
        public float Radius = 0.3f;
        public float MaxLifetime = 4f;
        public bool Pierce;

        private float _aliveTime;
        private System.Collections.Generic.HashSet<string> _hit;
        private static readonly Collider[] _buffer = new Collider[16];

        private void Update()
        {
            transform.position += Velocity * Time.deltaTime;

            _aliveTime += Time.deltaTime;
            if (_aliveTime >= MaxLifetime) { Destroy(gameObject); return; }

            var count = Physics.OverlapSphereNonAlloc(transform.position, Radius, _buffer, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var targetId = SkillEntityProxy.IdFrom(col);
                if (string.IsNullOrEmpty(targetId) || targetId == CasterId) continue;

                if (Pierce)
                {
                    _hit ??= new System.Collections.Generic.HashSet<string>();
                    if (!_hit.Add(targetId)) continue;
                }

                if (SkillEntityProxy.IsDead(targetId)) continue;
                SkillEntityProxy.Damage(targetId, Damage, CasterId, DamageType, transform.position);

                if (!Pierce) { Destroy(gameObject); return; }
            }
        }
    }
}
