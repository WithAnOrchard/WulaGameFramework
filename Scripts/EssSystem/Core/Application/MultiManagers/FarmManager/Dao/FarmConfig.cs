using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 农场模板 —— 一种农场（如"基础农场" / "温室"）的不变数据：网格大小、允许作物白名单、升级表。
    /// 运行时实例见 <see cref="FarmInstance"/>。
    /// </summary>
    [Serializable]
    public class FarmConfig
    {
        /// <summary>唯一 Id（如 "farm_basic"）。</summary>
        public string Id;

        /// <summary>显示名（"农场"）。</summary>
        public string DisplayName;

        /// <summary>初始网格行数（升级可扩展）。</summary>
        public int InitialRows = 2;

        /// <summary>初始网格列数。</summary>
        public int InitialCols = 3;

        /// <summary>允许种植的 CropConfig Id 白名单（空 = 接受任意已注册作物）。</summary>
        public List<string> AllowedCropIds = new List<string>();

        /// <summary>建造时消耗的物品需求（itemId → 数量）—— 接 InventoryManager 扣库存。</summary>
        public List<BuildCost> BuildCosts = new List<BuildCost>();

        /// <summary>升级表：索引 = 目标等级（从 1 起，0 = 初始），每级追加行 / 列 / 解锁。</summary>
        public List<FarmUpgradeStep> Upgrades = new List<FarmUpgradeStep>();

        /// <summary>互动子场景 Id（接 SceneInstanceManager；空 = 不进入子场景，原地种植）。</summary>
        public string InteriorSceneInstanceId;
    }

    /// <summary>建造 / 升级时的物品消耗条目。</summary>
    [Serializable]
    public class BuildCost
    {
        public string ItemId;
        public int Amount = 1;
    }

    /// <summary>农场单次升级带来的容量扩展。</summary>
    [Serializable]
    public class FarmUpgradeStep
    {
        /// <summary>升级到此等级追加的行数（叠加在当前网格上）。</summary>
        public int AddRows;

        /// <summary>升级到此等级追加的列数。</summary>
        public int AddCols;

        /// <summary>本次升级消耗。</summary>
        public List<BuildCost> Costs = new List<BuildCost>();

        /// <summary>解锁的新 CropConfigId 列表（追加到 FarmConfig.AllowedCropIds）。</summary>
        public List<string> UnlockCropIds = new List<string>();
    }
}
