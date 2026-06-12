using System.Collections.Generic;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    public interface IEntityResources : IEntityCapability
    {
        bool Has(string resourceId);
        float GetCurrent(string resourceId);
        float GetMax(string resourceId);
        float GetRegen(string resourceId);
        void Define(string resourceId, float maxValue, float currentValue = -1f, float regenPerSecond = 0f);
        void Set(string resourceId, float currentValue);
        void SetMax(string resourceId, float maxValue, bool refill = false);
        bool Consume(string resourceId, float amount);
        float Restore(string resourceId, float amount);
        IReadOnlyDictionary<string, EntityResourceState> Snapshot { get; }
    }
}
