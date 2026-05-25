using System;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 农场单个种植槽位 —— FarmInstance.Slots 的一格。
    /// 槽位空时 <see cref="CropConfigId"/> 为空 + <see cref="Stage"/>=Empty；
    /// 种下后记录 CropConfigId 与种植时间，FarmService 周期性检查推进生长阶段。
    /// </summary>
    [Serializable]
    public class FarmSlot
    {
        /// <summary>槽位坐标（农场子场景内的网格行，0-based）。</summary>
        public int Row;

        /// <summary>槽位坐标（农场子场景内的网格列，0-based）。</summary>
        public int Col;

        /// <summary>当前种植的作物 Id（空 = 槽位空闲）。</summary>
        public string CropConfigId;

        /// <summary>种植时刻（UTC Unix 秒，方便离线时长累计）。0 = 未种植。</summary>
        public long PlantedAtUnixSeconds;

        /// <summary>当前生长阶段（按 CropConfig.StageDurations 推进）。</summary>
        public CropGrowthStage Stage = CropGrowthStage.Empty;

        /// <summary>是否被浇水 —— 浇水后生长速度按 <c>FarmService.WateredSpeedMultiplier</c> 加速；阶段推进后自动重置。</summary>
        public bool Watered;

        /// <summary>是否有害虫 —— 害虫存在期间生长停滞；调用除虫操作后恢复并重新安排下次害虫定时。</summary>
        public bool HasPest;

        /// <summary>施肥加速到期时刻（UTC Unix 秒）；0 = 无施肥效果。</summary>
        public long FertilizeBoostUntilUnix;

        /// <summary>当前生长阶段开始时刻（UTC Unix 秒）；种植或阶段推进时更新，用于计算本阶段已累计时间。</summary>
        public long StageStartUnixSeconds;

        /// <summary>下次害虫触发时刻（UTC Unix 秒）；0 = 不触发。种植 / 除虫后随机重新安排。</summary>
        public long ScheduledPestUnixSeconds;
    }
}
