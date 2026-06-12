using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 农场模板。描述一种农场的固定数据，例如网格大小、可种作物、建造消耗和升级规则。
    /// 运行期实例见 <see cref="FarmInstance"/>。
    /// </summary>
    [Serializable]
    public class FarmConfig
    {
        /// <summary>唯一 Id，例如 "farm_basic"。</summary>
        public string Id;

        /// <summary>显示名称。</summary>
        public string DisplayName;

        /// <summary>初始网格行数。</summary>
        public int InitialRows = 2;

        /// <summary>初始网格列数。</summary>
        public int InitialCols = 3;

        /// <summary>允许种植的 CropConfig Id 白名单。为空时接受任意已注册作物。</summary>
        public List<string> AllowedCropIds = new List<string>();

        /// <summary>建造时消耗的物品列表，后续由 InventoryManager 扣除。</summary>
        public List<BuildCost> BuildCosts = new List<BuildCost>();

        /// <summary>升级表。索引表示目标等级，从 1 开始，0 表示初始等级。</summary>
        public List<FarmUpgradeStep> Upgrades = new List<FarmUpgradeStep>();

        /// <summary>交互子场景 Id。为空时表示原地种植，不进入子场景。</summary>
        public string InteriorSceneInstanceId;
    }

    /// <summary>建造或升级时的物品消耗条目。</summary>
    [Serializable]
    public class BuildCost
    {
        public string ItemId;
        public int Amount = 1;
    }

    /// <summary>农场单次升级带来的容量扩展和解锁内容。</summary>
    [Serializable]
    public class FarmUpgradeStep
    {
        /// <summary>追加的行数。</summary>
        public int AddRows;

        /// <summary>追加的列数。</summary>
        public int AddCols;

        /// <summary>本次升级消耗。</summary>
        public List<BuildCost> Costs = new List<BuildCost>();

        /// <summary>本次升级解锁的 CropConfig Id 列表。</summary>
        public List<string> UnlockCropIds = new List<string>();
    }
}
