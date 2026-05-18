namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 作物生长阶段 —— FarmSlot 在每个 Tick 根据 CropConfig.StageDurations 推进到下一阶段。
    /// </summary>
    public enum CropGrowthStage
    {
        /// <summary>未种植（槽位空闲）。</summary>
        Empty = 0,

        /// <summary>种子 —— 刚种下。</summary>
        Seed = 1,

        /// <summary>幼苗。</summary>
        Sprout = 2,

        /// <summary>生长中。</summary>
        Growing = 3,

        /// <summary>成熟 —— 可收割。</summary>
        Mature = 4,

        /// <summary>枯萎 —— 超时未收割（可选规则，未收割保留 Mature 也合理）。</summary>
        Wilted = 5,
    }
}
