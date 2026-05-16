using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Brain.Default
{
    /// <summary>
    /// 范围感知器 —— <see cref="Physics2D.OverlapCircleAll"/> 检测附近带 <see cref="EntityHandle"/> 的实体。
    /// <para>
    /// 检测到的 Entity 写入 <see cref="BrainContext.NearbyEntities"/>。
    /// 支持 LayerMask 过滤。
    /// </para>
    /// </summary>
    public class RangeSensor : ISensor
    {
        public float Interval { get; }
        public float Radius { get; }
        public LayerMask LayerMask { get; }

        private static readonly Collider2D[] _buffer = new Collider2D[64];

        public RangeSensor(float radius, float interval = 0.4f, LayerMask layerMask = default)
        {
            Radius = Mathf.Max(0.1f, radius);
            Interval = Mathf.Max(0f, interval);
            LayerMask = layerMask;
        }

        public void Sense(BrainContext ctx)
        {
            if (ctx.Self == null) return;
            var pos = ctx.Self.CharacterRoot != null
                ? (Vector2)ctx.Self.CharacterRoot.position
                : (Vector2)ctx.Self.WorldPosition;

            int count;
            if (LayerMask == default)
                count = Physics2D.OverlapCircleNonAlloc(pos, Radius, _buffer);
            else
                count = Physics2D.OverlapCircleNonAlloc(pos, Radius, _buffer, LayerMask);

            for (var i = 0; i < count; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle == null || handle.Entity == null) continue;
                if (handle.Entity == ctx.Self) continue; // 排除自身
                ctx.NearbyEntities.Add(handle.Entity);
            }
        }
    }
}
