using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IContactDamage"/> 默认实现 —— 用 <see cref="EntityAreaScanner"/> 周期扫描，
    /// 对范围内可伤害目标调 <see cref="EntityService.TryDamage"/> 结算。
    /// <para>不依赖 Unity 触发器回调，所以能力本身不需要 MonoBehaviour 桥接。</para>
    /// </summary>
    public class ContactDamageComponent : IContactDamage, ITickableCapability
    {
        public float DamagePerTick { get; }
        public float TickInterval { get; }
        public float Radius { get; }
        public string DamageType { get; }
        public LayerMask LayerMask { get; }

        private Entity _owner;
        private float _timer;

        public ContactDamageComponent(
            float damagePerTick,
            float radius,
            float tickInterval = 1f,
            string damageType = "ContactDamage",
            LayerMask layerMask = default)
        {
            DamagePerTick = Mathf.Max(0f, damagePerTick);
            Radius = Mathf.Max(0f, radius);
            TickInterval = Mathf.Max(0.05f, tickInterval);
            DamageType = damageType;
            LayerMask = layerMask.value == 0 ? (LayerMask)~0 : layerMask;
        }

        public void OnAttach(Entity owner) { _owner = owner; _timer = TickInterval; }
        public void OnDetach(Entity owner) { _owner = null; }

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.CharacterRoot == null) return;
            if (DamagePerTick <= 0f || Radius <= 0f) return;

            _timer -= deltaTime;
            if (_timer > 0f) return;
            _timer = TickInterval;

            var service = EntityService.Instance;
            if (service == null) return;

            var origin = (Vector2)_owner.CharacterRoot.position;
            var targets = EntityAreaScanner.Scan(origin, Radius, LayerMask, _owner);

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (!target.Has<IDamageable>()) continue;
                service.TryDamage(target, DamagePerTick, _owner, DamageType);
            }
        }
    }
}
