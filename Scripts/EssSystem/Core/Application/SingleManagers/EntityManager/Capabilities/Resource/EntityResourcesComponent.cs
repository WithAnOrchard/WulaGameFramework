using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    public class EntityResourcesComponent : IEntityResources, ITickableCapability
    {
        private readonly Dictionary<string, EntityResourceState> _resources = new();
        private Entity _owner;

        public IReadOnlyDictionary<string, EntityResourceState> Snapshot => _resources;

        public EntityResourcesComponent()
        {
            Define(EntityResourceIds.Mana, 0f, 0f);
        }

        public bool Has(string resourceId) =>
            !string.IsNullOrEmpty(resourceId) && _resources.ContainsKey(resourceId);

        public float GetCurrent(string resourceId) =>
            TryGet(resourceId, out var state) ? state.Current : 0f;

        public float GetMax(string resourceId) =>
            TryGet(resourceId, out var state) ? state.Max : 0f;

        public float GetRegen(string resourceId) =>
            TryGet(resourceId, out var state) ? state.RegenPerSecond : 0f;

        public void Define(string resourceId, float maxValue, float currentValue = -1f, float regenPerSecond = 0f)
        {
            if (string.IsNullOrEmpty(resourceId)) return;
            _resources[resourceId] = new EntityResourceState(maxValue, currentValue, regenPerSecond);
        }

        public void Set(string resourceId, float currentValue)
        {
            if (!TryGet(resourceId, out var state)) return;
            state.Current = Mathf.Clamp(currentValue, 0f, state.Max);
        }

        public void SetMax(string resourceId, float maxValue, bool refill = false)
        {
            if (string.IsNullOrEmpty(resourceId)) return;
            if (!TryGet(resourceId, out var state))
            {
                Define(resourceId, maxValue, refill ? maxValue : 0f);
                return;
            }

            var oldMax = state.Max;
            state.Max = Mathf.Max(0f, maxValue);
            if (refill)
                state.Current = state.Max;
            else if (oldMax > 0f && state.Current > state.Max)
                state.Current = state.Max;
            else
                state.Current = Mathf.Clamp(state.Current, 0f, state.Max);
        }

        public bool Consume(string resourceId, float amount)
        {
            if (amount <= 0f) return true;
            if (!TryGet(resourceId, out var state)) return false;
            if (state.Current + 0.0001f < amount) return false;
            state.Current = Mathf.Max(0f, state.Current - amount);
            return true;
        }

        public float Restore(string resourceId, float amount)
        {
            if (amount <= 0f || !TryGet(resourceId, out var state)) return 0f;
            var before = state.Current;
            state.Current = Mathf.Min(state.Max, state.Current + amount);
            return state.Current - before;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            foreach (var state in _resources.Values)
            {
                if (Mathf.Approximately(state.RegenPerSecond, 0f)) continue;
                state.Current = Mathf.Clamp(state.Current + state.RegenPerSecond * deltaTime, 0f, state.Max);
            }
        }

        public void OnAttach(Entity owner) => _owner = owner;
        public void OnDetach(Entity owner) => _owner = null;

        private bool TryGet(string resourceId, out EntityResourceState state)
        {
            state = null;
            return !string.IsNullOrEmpty(resourceId) && _resources.TryGetValue(resourceId, out state);
        }
    }
}
