using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime
{
    /// <summary>
    /// <b>GameObject ↔ Entity 桥</b>：由 <see cref="EntityService"/> 在创建实体时
    /// 自动挂到承载 GameObject 上，承担两件事：
    /// <list type="number">
    /// <item>让 Unity 物理 / 射线 / 触发回调返回的 <see cref="Collider2D"/> 能通过
    ///       <c>GetComponentInParent&lt;EntityHandle&gt;()</c> 反查到 <see cref="InstanceId"/> / <see cref="Entity"/>。</item>
    /// <item>提供"可被攻击"的便捷入口 <see cref="TakeDamage"/>，封装 <c>EVT_DAMAGE_ENTITY</c>
    ///       事件分发，业务侧不必拼字符串。</item>
    /// </list>
    /// <para>原则：本组件仅是<b>引用持有者</b>，不实现任何游戏逻辑；伤害结算等仍由 <see cref="IDamageable"/> 能力完成。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class EntityHandle : MonoBehaviour
    {
        /// <summary>实体唯一 ID。</summary>
        public string InstanceId { get; private set; }

        /// <summary>关联的运行时实体（可为 null —— 实体已销毁或尚未注册）。</summary>
        public Entity Entity { get; private set; }

        /// <summary>实体是否已挂载 <see cref="IDamageable"/> 能力。</summary>
        public bool CanBeAttacked => Entity != null && Entity.Has<IDamageable>();

        /// <summary>由 <see cref="EntityService"/> 在创建时调用一次绑定；外部不应手动调。</summary>
        public void Bind(string instanceId, Entity entity)
        {
            InstanceId = instanceId;
            Entity = entity;
        }

        /// <summary>解绑：实体销毁时由 Service 调用。</summary>
        public void Unbind()
        {
            InstanceId = null;
            Entity = null;
        }

        /// <summary>
        /// 对本实体施加伤害 —— 通过 §4.1 bare-string 协议触发 <c>EVT_DAMAGE_ENTITY</c>，
        /// 由 EntityManager 路由到 <see cref="IDamageable"/>（含 <see cref="IInvulnerable"/> 拦截）。
        /// </summary>
        /// <param name="amount">伤害量</param>
        /// <param name="damageType">伤害类型（可选）</param>
        /// <param name="sourcePosition">攻击者位置（可选），用于计算击退方向</param>
        /// <returns>是否成功派发事件（不等于"造成了伤害"；实际命中量见 IDamageable 事件流）。</returns>
        public bool TakeDamage(float amount, string damageType = null, Vector3? sourcePosition = null)
        {
            if (string.IsNullOrEmpty(InstanceId) || !EventProcessor.HasInstance) return false;
            var args = new List<object> { InstanceId, Mathf.Max(0f, amount) };
            // args: [instanceId, damage, damageType?, sourcePosition?]
            if (damageType != null || sourcePosition.HasValue) args.Add(damageType);
            if (sourcePosition.HasValue) args.Add(sourcePosition.Value);
            EventProcessor.Instance.TriggerEventMethod("DamageEntity", args);
            return true;
        }
    }
}
