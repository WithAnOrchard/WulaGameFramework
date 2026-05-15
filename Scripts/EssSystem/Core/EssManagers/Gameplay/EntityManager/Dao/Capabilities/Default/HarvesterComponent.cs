using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IHarvester"/> 默认实现 —— 周期通过 <c>InventoryManager.EVT_ADD_ITEM</c>（bare-string §4.1）
    /// 给指定容器加物品。失败（容器满 / 不存在）静默；业务侧可订阅 <c>InventoryChanged</c> 广播追踪结果。
    /// </summary>
    public class HarvesterComponent : IHarvester, ITickableCapability
    {
        public string ItemId { get; }
        public int Amount { get; }
        public float Interval { get; }
        public string TargetInventoryId { get; }

        private float _timer;

        public HarvesterComponent(string itemId, int amount, float interval, string targetInventoryId = "player")
        {
            ItemId = itemId;
            Amount = Mathf.Max(1, amount);
            Interval = Mathf.Max(0.1f, interval);
            TargetInventoryId = string.IsNullOrEmpty(targetInventoryId) ? "player" : targetInventoryId;
        }

        public void OnAttach(Entity owner) { _timer = Interval; }
        public void OnDetach(Entity owner) { }

        public void Tick(float deltaTime)
        {
            if (string.IsNullOrEmpty(ItemId)) return;
            _timer -= deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;

            if (!EventProcessor.HasInstance) return;
            // [inventoryId, itemIdOrItem, amount]
            EventProcessor.Instance.TriggerEventMethod(
                "InventoryAddItem",       // = InventoryManager.EVT_ADD_ITEM
                new List<object> { TargetInventoryId, ItemId, Amount });
        }
    }
}
