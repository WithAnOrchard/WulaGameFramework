using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 作物模板。描述一种作物的固定数据，例如种子物品、产物物品、生长时间和阶段视觉。
    /// 运行期槽位状态见 <see cref="FarmSlot"/>。
    /// </summary>
    [Serializable]
    public class CropConfig
    {
        /// <summary>唯一 Id，例如 "crop_wheat"。</summary>
        public string Id;

        /// <summary>显示名称。</summary>
        public string DisplayName;

        /// <summary>种子物品 Id，接 InventoryManager。</summary>
        public string SeedItemId;

        /// <summary>成熟收割后的产物物品 Id。</summary>
        public string OutputItemId;

        /// <summary>单次收割产量。</summary>
        public int OutputAmount = 1;

        /// <summary>
        /// 各阶段持续秒数。通常按 Seed -> Sprout -> Growing -> Mature 的推进顺序配置。
        /// 为空或长度不足时，缺失阶段不会自动推进。
        /// </summary>
        public List<float> StageDurations = new List<float>();

        /// <summary>各阶段对应的 sprite Id，接 ResourceManager。可为空。</summary>
        public List<string> StageSpriteIds = new List<string>();
    }
}
