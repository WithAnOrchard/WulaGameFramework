using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IGroundSensor"/> 默认实现 —— 从 collider 底部附近向下 <see cref="Physics2D.RaycastNonAlloc"/>。
    /// <para>
    /// <b>关键实现细节</b>：Unity 默认 <c>Physics2D.queriesStartInColliders = true</c>，
    /// 如果射线原点正好在玩家 collider 边界，<see cref="Physics2D.Raycast"/> 会把玩家自己当作首个命中，
    /// 而 <see cref="Physics2D.Raycast"/> 只返回首个命中 → 自身被过滤后误判为"未着地"。
    /// 因此本实现：
    /// <list type="number">
    /// <item>原点向上提 <see cref="SkinUp"/>（默认 0.05），稳稳落在玩家 collider 内部；</item>
    /// <item>用 <see cref="Physics2D.RaycastNonAlloc"/> 取沿线所有命中（按距离排序），跳过自身命中后再判定。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class Raycast2DGroundSensorComponent : IGroundSensor
    {
        public bool IsGrounded { get; private set; }

        /// <summary>collider 底部向下检测距离。</summary>
        public float Distance { get; set; }

        /// <summary>原点上提量（深入 collider 内部），用于躲开 queriesStartInColliders 的"自命中"行为。</summary>
        public float SkinUp { get; set; } = 0.05f;

        /// <summary>过滤层级；默认排除 IgnoreRaycast 层。</summary>
        public LayerMask Layers { get; set; } = Physics2D.DefaultRaycastLayers;

        private readonly Transform _root;
        private readonly Collider2D _collider;
        private Entity _owner;

        public Raycast2DGroundSensorComponent(Transform root, Collider2D collider, float distance = 0.1f)
        {
            _root = root;
            _collider = collider;
            Distance = Mathf.Max(0.01f, distance);
        }

        public void OnAttach(Entity owner) { _owner = owner; }
        public void OnDetach(Entity owner) { _owner = null; }

        public bool Refresh()
        {
            if (_root == null || _collider == null) { IsGrounded = false; return false; }

            var b = _collider.bounds;
            var origin = new Vector2(b.center.x, b.min.y + SkinUp);
            var rayLength = SkinUp + Distance;

            // 临时关闭 queriesStartInColliders —— 射线原点在自身 collider 内部时不再自命中，
            // 无需 NonAlloc + 手动过滤自身。恢复全局设置保证其他系统不受影响。
            var prev = Physics2D.queriesStartInColliders;
            Physics2D.queriesStartInColliders = false;
            var hit = Physics2D.Raycast(origin, Vector2.down, rayLength, Layers);
            Physics2D.queriesStartInColliders = prev;

            IsGrounded = hit.collider != null;
            return IsGrounded;
        }
    }
}
