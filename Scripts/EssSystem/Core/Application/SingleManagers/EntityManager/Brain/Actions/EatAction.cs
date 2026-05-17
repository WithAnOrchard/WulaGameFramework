using System.Collections.Generic;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;

using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions
{
    /// <summary>
    /// 进食动作 —— 从背包消耗食物并降低饥饿值。
    /// <para>
    /// 通过 bare-string <c>"InventoryRemove"</c> 消耗物品，
    /// 成功后调 <see cref="INeeds.Add"/> 降低 Hunger。
    /// 无食物时返回 Failure。
    /// </para>
    /// </summary>
    public class EatAction : IBrainAction
    {
        private readonly string _foodItemId;
        private readonly string _inventoryId;
        private readonly float _hungerReduction;

        /// <param name="foodItemId">食物物品 ID。</param>
        /// <param name="inventoryId">背包容器 ID。</param>
        /// <param name="hungerReduction">进食后饥饿减少量（正值，内部取反）。</param>
        public EatAction(string foodItemId, string inventoryId = "player", float hungerReduction = 0.3f)
        {
            _foodItemId = foodItemId;
            _inventoryId = inventoryId;
            _hungerReduction = hungerReduction;
        }

        public void OnEnter(BrainContext ctx) { }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            if (!EventProcessor.HasInstance) return BrainStatus.Failure;

            // 尝试从背包移除食物
            var result = EventProcessor.Instance.TriggerEventMethod(
                "InventoryRemove",
                new List<object> { _inventoryId, _foodItemId, 1 });

            if (!ResultCode.IsOk(result))
                return BrainStatus.Failure;

            // 减少饥饿
            var needs = ctx.Self.Get<INeeds>();
            needs?.Add("Hunger", -_hungerReduction);

            return BrainStatus.Success;
        }

        public void OnExit(BrainContext ctx) { }
    }
}
