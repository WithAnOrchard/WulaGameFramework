using System;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    [Serializable]
    public class EntityResourceState
    {
        public float Current;
        public float Max;
        public float RegenPerSecond;

        public EntityResourceState(float max, float current = -1f, float regenPerSecond = 0f)
        {
            Max = UnityEngine.Mathf.Max(0f, max);
            Current = current < 0f ? Max : UnityEngine.Mathf.Clamp(current, 0f, Max);
            RegenPerSecond = regenPerSecond;
        }
    }
}
