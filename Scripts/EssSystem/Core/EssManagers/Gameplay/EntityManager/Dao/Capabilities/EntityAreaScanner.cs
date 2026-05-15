using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// OverlapCircle 扫描工具 —— 统一"以某 Entity 为圆心、扫描周围 EntityHandle"的重复逻辑。
    /// <para><see cref="ContactDamageComponent"/> 和 <see cref="AuraComponent"/> 可直接调用 <see cref="Scan"/>，
    /// 避免各自维护 buffer / filter / null 检查 / 自身过滤等样板代码。</para>
    /// <para>内部使用共享静态 buffer（容量 32），单线程安全（Unity 主线程）。</para>
    /// </summary>
    public static class EntityAreaScanner
    {
        private static readonly Collider2D[] _buffer = new Collider2D[32];
        // 复用列表避免每次 Scan new List：调用方必须在同一帧内使用完毕，不要持有引用。
        private static readonly List<Entity> _results = new(16);

        /// <summary>
        /// 扫描 <paramref name="origin"/> 为圆心、<paramref name="radius"/> 半径内的所有 Entity。
        /// </summary>
        /// <param name="origin">世界坐标圆心。</param>
        /// <param name="radius">扫描半径。</param>
        /// <param name="layerMask">物理层过滤。</param>
        /// <param name="self">自身 Entity（用于排除自伤），传 null 则不排除。</param>
        /// <param name="includeSelf">是否包含自身（仅 <paramref name="self"/> 非 null 时生效）。</param>
        /// <returns>本帧内有效的 Entity 列表（只读，下次调用会覆盖）。</returns>
        public static IReadOnlyList<Entity> Scan(Vector2 origin, float radius, LayerMask layerMask, Entity self = null, bool includeSelf = false)
        {
            _results.Clear();

            var filter = new ContactFilter2D { useLayerMask = true, layerMask = layerMask, useTriggers = true };
            var hitCount = Physics2D.OverlapCircle(origin, radius, filter, _buffer);

            for (var i = 0; i < hitCount; i++)
            {
                var col = _buffer[i];
                if (col == null) continue;
                var handle = col.GetComponentInParent<EntityHandle>();
                if (handle == null || handle.Entity == null) continue;
                if (!includeSelf && handle.Entity == self) continue;
                // 去重：同一 Entity 可能有多个 Collider 命中
                if (_results.Contains(handle.Entity)) continue;
                _results.Add(handle.Entity);
            }

            return _results;
        }
    }
}
