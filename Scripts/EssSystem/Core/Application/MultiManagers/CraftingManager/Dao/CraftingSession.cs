using System;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager.Dao
{
    /// <summary>
    /// 运行中的制作任务 —— 玩家发起到完成 / 取消之间的状态。
    /// </summary>
    [Serializable]
    public class CraftingSession
    {
        /// <summary>会话唯一 Id（生成时分配）。</summary>
        public string SessionId;

        /// <summary>发起的玩家 Id。</summary>
        public string PlayerId;

        /// <summary>使用的配方 Id。</summary>
        public string RecipeId;

        /// <summary>使用的工作台 Id（""=手搓）。</summary>
        public string WorkstationId;

        /// <summary>本批次数量（×N）。</summary>
        public int Quantity = 1;

        /// <summary>开始时间（<c>Time.time</c>）。</summary>
        public float StartTime;

        /// <summary>预计完成时间（<c>Time.time</c>）。</summary>
        public float EndTime;

        /// <summary>已预扣的材料是否还在保留状态（取消时退还）。</summary>
        public bool MaterialsReserved;
    }
}
