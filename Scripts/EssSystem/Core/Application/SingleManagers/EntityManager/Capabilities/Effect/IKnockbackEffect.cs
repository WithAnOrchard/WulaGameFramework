using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 击退效果能力 —— Entity 受伤时触发物理击退。
    /// </summary>
    public interface IKnockbackEffect : IEntityCapability
    {
        /// <summary>触发击退</summary>
        /// <param name="damageSource">伤害来源位置，用于计算击退方向</param>
        void OnKnockback(Vector3 damageSource);
    }
}
