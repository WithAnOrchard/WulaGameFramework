using System;

namespace EssSystem.Core.Application.MultiManagers.BuildingManager.Dao.Config
{
    /// <summary>
    /// 建造单条材料需求：itemId 取自 <c>InventoryManager</c> 物品模板表；<see cref="Amount"/> 是需要 deliver 的总数。
    /// <see cref="DisplayName"/> 是 HUD 上显示的简称，留空时回落到 <see cref="ItemId"/>。
    /// </summary>
    [Serializable]
    public class BuildingCost
    {
        public string ItemId;
        public int Amount;
        public string DisplayName;

        public BuildingCost() { }

        public BuildingCost(string itemId, int amount, string displayName = null)
        {
            ItemId = itemId;
            Amount = System.Math.Max(0, amount);
            DisplayName = displayName;
        }

        public string Display => string.IsNullOrEmpty(DisplayName) ? ItemId : DisplayName;
    }
}
