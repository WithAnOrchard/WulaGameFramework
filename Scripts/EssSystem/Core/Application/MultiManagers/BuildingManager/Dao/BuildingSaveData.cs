using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.BuildingManager.Dao
{
    /// <summary>
    /// 建筑可序列化存档数据 —— 不含 Unity 引用和 Action 回调。
    /// <para><see cref="BuildingService.SaveAllCategories"/> 将每个运行时 <see cref="Building"/>
    /// 快照为本结构写入 JSON；<see cref="BuildingService.RestoreBuildings"/> 根据本结构 + 已注册的
    /// <see cref="Config.BuildingConfig"/> 重建运行时实例。</para>
    /// </summary>
    [Serializable]
    public class BuildingSaveData
    {
        /// <summary>实例 ID（= Entity instanceId）。</summary>
        public string InstanceId;

        /// <summary>模板 ConfigId —— 用于重新查找 <see cref="Config.BuildingConfig"/>。</summary>
        public string ConfigId;

        /// <summary>建造状态。</summary>
        public BuildingState State;

        /// <summary>世界坐标。</summary>
        public Vector3 Position;

        /// <summary>剩余材料快照：itemId → remaining。仅 Constructing 态有意义。</summary>
        public List<CostEntry> RemainingCosts = new List<CostEntry>();

        [Serializable]
        public struct CostEntry
        {
            public string ItemId;
            public int Amount;
        }
    }
}
