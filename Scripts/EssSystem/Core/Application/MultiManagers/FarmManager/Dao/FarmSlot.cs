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

        /// <summary>是否被浇水 —— 业务规则保留位（可加速生长 / 防枯萎，未来里程碑实施）。</summary>
        public bool Watered;
    }
}
