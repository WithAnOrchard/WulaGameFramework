using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IInvulnerable"/> 的最简实现：常开/常关 + 原因标签。限时无敌可业务层每帧切 <see cref="Active"/>。
    /// </summary>
    public class InvulnerableComponent : IInvulnerable
    {
        public bool Active { get; set; }
        public string Reason { get; set; }

        public InvulnerableComponent(string reason = "Default", bool active = true)
        {
            Reason = reason;
            Active = active;
        }

        public void OnAttach(Entity owner) { }
        public void OnDetach(Entity owner) { }
    }
}
