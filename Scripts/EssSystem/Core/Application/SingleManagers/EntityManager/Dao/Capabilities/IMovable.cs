using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities
{
    public interface IMovable : IEntityCapability
    {
        float MoveSpeed { get; }
        Vector3 Velocity { get; set; }
        void Move(Vector3 direction, float deltaTime);
    }
}
