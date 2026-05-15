using System.Collections.Generic;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Brain.Actions
{
    /// <summary>
    /// 杩涢鍔ㄤ綔 鈥斺€?浠庤儗鍖呮秷鑰楅鐗╁苟闄嶄綆楗ラタ鍊笺€?    /// <para>
    /// 閫氳繃 bare-string <c>"InventoryRemove"</c> 娑堣€楃墿鍝侊紝
    /// 鎴愬姛鍚庤皟 <see cref="INeeds.Add"/> 闄嶄綆 Hunger銆?    /// 鏃犻鐗╂椂杩斿洖 Failure銆?    /// </para>
    /// </summary>
    public class EatAction : IBrainAction
    {
        private readonly string _foodItemId;
        private readonly string _inventoryId;
        private readonly float _hungerReduction;

        /// <param name="foodItemId">椋熺墿鐗╁搧 ID銆?/param>
        /// <param name="inventoryId">鑳屽寘瀹瑰櫒 ID銆?/param>
        /// <param name="hungerReduction">杩涢鍚庨ゥ楗垮噺灏戦噺锛堟鍊硷紝鍐呴儴鍙栧弽锛夈€?/param>
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

            // 鍑忓皯楗ラタ
            var needs = ctx.Self.Get<INeeds>();
            needs?.Add("Hunger", -_hungerReduction);

            return BrainStatus.Success;
        }

        public void OnExit(BrainContext ctx) { }
    }
}
