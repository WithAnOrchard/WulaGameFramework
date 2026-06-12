using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    public class StatsRegenerationComponent : IEntityCapability, ITickableCapability
    {
        private Entity _owner;

        public void Tick(float deltaTime)
        {
            if (_owner == null || deltaTime <= 0f) return;
            var stats = _owner.Get<IStats>();
            if (stats == null) return;

            var hpRegen = Mathf.Max(0f, stats.GetDerived(DerivedStat.HpRegen));
            if (hpRegen > 0f)
            {
                var damageable = _owner.Get<IDamageable>();
                if (damageable != null && !damageable.IsDead && damageable.CurrentHp < damageable.MaxHp)
                    damageable.Heal(hpRegen * deltaTime, _owner);
            }

            var mpRegen = Mathf.Max(0f, stats.GetDerived(DerivedStat.MpRegen));
            if (mpRegen > 0f)
            {
                var resources = _owner.Get<IEntityResources>();
                if (resources != null && resources.Has(EntityResourceIds.Mana))
                    resources.Restore(EntityResourceIds.Mana, mpRegen * deltaTime);
            }
        }

        public void OnAttach(Entity owner) => _owner = owner;
        public void OnDetach(Entity owner) => _owner = null;
    }
}
