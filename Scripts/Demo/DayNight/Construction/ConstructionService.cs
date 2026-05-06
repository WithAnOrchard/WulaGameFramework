using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using Demo.DayNight.Construction.Dao;

namespace Demo.DayNight.Construction
{
    /// <summary>建造系统 Service —— 持久化 <see cref="ConstructionPlacement"/> 列表 + 广播放置/移除。</summary>
    public class ConstructionService : Service<ConstructionService>
    {
        // ─── 数据分类 ────────────────────────────────────────────
        public const string CAT_PLACEMENTS = "Placements";

        // ─── Event 名常量（广播）────────────────────────────────
        /// <summary>放置成功 **广播**。参数 <c>[string instanceId, string typeId, Vector3 position]</c>。</summary>
        public const string EVT_PLACED = "OnConstructionPlaced";

        /// <summary>移除成功 **广播**。参数 <c>[string instanceId]</c>。</summary>
        public const string EVT_REMOVED = "OnConstructionRemoved";

        protected override void Initialize()
        {
            base.Initialize();
            Log($"ConstructionService 初始化完成 (loaded={CountPlacements()})", Color.green);
        }

        // ─── Public API ──────────────────────────────────────────
        /// <summary>放置一个工事。<paramref name="instanceId"/> 为空时自动生成。返回最终 instanceId 或 null。</summary>
        public string Place(string typeId, Vector3 position, float rotation = 0f, string instanceId = null)
        {
            if (string.IsNullOrEmpty(typeId))
            {
                LogWarning("Place: typeId 为空");
                return null;
            }
            if (string.IsNullOrEmpty(instanceId))
                instanceId = $"build:{typeId}:{System.Guid.NewGuid():N}";

            var placement = new ConstructionPlacement
            {
                InstanceId = instanceId,
                TypeId = typeId,
                Position = position,
                Rotation = rotation
            };
            SetData(CAT_PLACEMENTS, instanceId, placement);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_PLACED,
                    new List<object> { instanceId, typeId, position });
            return instanceId;
        }

        /// <summary>按 id 移除（持久化中删除）。</summary>
        public bool Remove(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            if (!HasData(CAT_PLACEMENTS, instanceId)) return false;
            RemoveData(CAT_PLACEMENTS, instanceId);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_REMOVED,
                    new List<object> { instanceId });
            return true;
        }

        public ConstructionPlacement Get(string instanceId) =>
            string.IsNullOrEmpty(instanceId) ? null : GetData<ConstructionPlacement>(CAT_PLACEMENTS, instanceId);

        public IEnumerable<ConstructionPlacement> GetAll()
        {
            if (!_dataStorage.TryGetValue(CAT_PLACEMENTS, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is ConstructionPlacement p) yield return p;
        }

        public int CountPlacements()
        {
            return _dataStorage.TryGetValue(CAT_PLACEMENTS, out var dict) ? dict.Count : 0;
        }
    }
}
