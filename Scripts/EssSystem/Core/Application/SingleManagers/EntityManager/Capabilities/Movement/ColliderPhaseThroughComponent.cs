using UnityEngine;

using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IPhaseThrough"/> 默认实现 —— 通过切换 <see cref="Collider2D.isTrigger"/> 实现穿墙。
    /// <para>穿墙开启 = collider 变 trigger，物体不再与其他实体发生碰撞解算（但仍触发 OnTrigger 事件）。</para>
    /// </summary>
    public class ColliderPhaseThroughComponent : IPhaseThrough
    {
        public bool PhasingThrough { get; private set; }

        private readonly Collider2D _collider;
        private readonly bool _originalIsTrigger;
        private Entity _owner;

        public ColliderPhaseThroughComponent(Collider2D collider, bool initialPhasing = false)
        {
            _collider = collider;
            _originalIsTrigger = collider != null && collider.isTrigger;
            PhasingThrough = initialPhasing;
            Apply();
        }

        public void OnAttach(Entity owner) { _owner = owner; }
        public void OnDetach(Entity owner) { _owner = null; if (_collider != null) _collider.isTrigger = _originalIsTrigger; }

        public void SetPhasing(bool phasing)
        {
            if (PhasingThrough == phasing) return;
            PhasingThrough = phasing;
            Apply();
        }

        private void Apply()
        {
            if (_collider == null) return;
            _collider.isTrigger = PhasingThrough || _originalIsTrigger;
        }
    }
}
