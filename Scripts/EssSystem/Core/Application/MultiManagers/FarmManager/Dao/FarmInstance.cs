using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    /// <summary>
    /// 运行时农场实例 —— 玩家放置的一座具体农场，记录位置 / 等级 / 各槽位状态。
    /// </summary>
    [Serializable]
    public class FarmInstance
    {
        /// <summary>实例 Id（全局唯一，如 "farm_0001"）。</summary>
        public string InstanceId;

        /// <summary>引用的 <see cref="FarmConfig.Id"/>。</summary>
        public string ConfigId;

        /// <summary>世界坐标（主场景中农场入口位置）。</summary>
        public Vector3 WorldPosition;

        /// <summary>当前等级 —— 0 表示初始，每升一级走一条 <see cref="FarmConfig.Upgrades"/>。</summary>
        public int Level;

        /// <summary>当前实际网格行数（= InitialRows + 已升级累加 AddRows）。</summary>
        public int Rows;

        /// <summary>当前实际网格列数。</summary>
        public int Cols;

        /// <summary>所有槽位（顺序：行优先 row * Cols + col）。Spawn 时按 Rows × Cols 预填空槽。</summary>
        public List<FarmSlot> Slots = new List<FarmSlot>();

        /// <summary>子场景实例 Id（玩家进入后激活；空 = 未实例化或不需要子场景）。</summary>
        public string ActiveSceneInstanceId;
    }
}
