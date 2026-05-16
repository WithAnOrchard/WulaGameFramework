using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IAura"/> 默认实现 —— 用 <see cref="EntityAreaScanner"/> 周期扫描，
    /// 对范围内所有 <see cref="IDamageable"/> 调用 <c>Heal</c>
    /// 或 <c>TryDamage</c>（取决于 <see cref="HealPerTick"/> 正负）。
    /// </summary>
    public class AuraComponent : IAura, ITickableCapability
    {
        public float HealPerTick { get; }
        public float TickInterval { get; }
        public float Radius { get; }
        public LayerMask LayerMask { get; }
        public bool IncludeSelf { get; }

        private Entity _owner;
        private float _timer;

        public AuraComponent(
            float healPerTick,
            float radius,
            float tickInterval = 1f,
            LayerMask layerMask = default,
            bool includeSelf = false)
        {
            HealPerTick = healPerTick;
            Radius = Mathf.Max(0f, radius);
            TickInterval = Mathf.Max(0.05f, tickInterval);
            LayerMask = layerMask.value == 0 ? (LayerMask)~0 : layerMask;
            IncludeSelf = includeSelf;
        }

        public void OnAttach(Entity owner) { _owner = owner; _timer = TickInterval; }
        public void OnDetach(Entity owner) { _owner = null; }

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.CharacterRoot == null) return;
            if (Mathf.Approximately(HealPerTick, 0f) || Radius <= 0f) return;

            _timer -= deltaTime;
            if (_timer > 0f) return;
            _timer = TickInterval;

            var origin = (Vector2)_owner.CharacterRoot.position;
            var targets = EntityAreaScanner.Scan(origin, Radius, LayerMask, _owner, IncludeSelf);

            var service = EntityService.Instance;
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var dmg = target.Get<IDamageable>();
                if (dmg == null) continue;

                if (HealPerTick >= 0f) dmg.Heal(HealPerTick, _owner);
                else if (service != null) service.TryDamage(target, -HealPerTick, _owner, "Aura");
            }
        }
    }
}
