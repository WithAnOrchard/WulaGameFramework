using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 作物模板 —— 一种作物（如"小麦" / "南瓜"）的不变数据：种子物品、产物物品、生长曲线、阶段视觉。
    /// 运行时槽位状态见 <see cref="FarmSlot"/>。
    /// </summary>
    [Serializable]
    public class CropConfig
    {
        /// <summary>唯一 Id（如 "crop_wheat"）。</summary>
        public string Id;

        /// <summary>显示名（"小麦"）。</summary>
        public string DisplayName;

        /// <summary>种子物品 Id（接 InventoryManager；玩家手持的种子物品，种植时消耗）。</summary>
        public string SeedItemId;

        /// <summary>产物物品 Id（成熟收割后入背包）。</summary>
        public string OutputItemId;

        /// <summary>单次收割产量（默认 1）。</summary>
        public int OutputAmount = 1;

        /// <summary>
        /// 各阶段持续秒数 —— 索引对应 <see cref="CropGrowthStage"/>：
        /// [Seed→Sprout, Sprout→Growing, Growing→Mature] 三段；Mature→Wilted 可选第 4 段。
        /// 缺省 (null 或长度不足) 时阶段不推进，便于"种下立即成熟"的调试 CropConfig。
        /// </summary>
        public List<float> StageDurations = new List<float>();

        /// <summary>
        /// 各阶段对应的 sprite Id（接 ResourceManager；与 StageDurations 平行索引）。
        /// 可空 —— 业务侧也可走 CharacterManager 部件方案，CropConfig 只提供裸 spriteId 通道。
        /// </summary>
        public List<string> StageSpriteIds = new List<string>();
    }
}
