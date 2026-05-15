using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.BuildingManager.Dao.Config;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.BuildingManager.Dao
{
    /// <summary>
    /// 建筑运行时实例 —— 持有底层 <see cref="Entity"/> 引用 + 建造阶段的材料账本 + HUD 索引。
    /// <para>非持久化（含 Unity Transform 引用 + Action 回调）。</para>
    /// </summary>
    public class Building
    {
        /// <summary>场景唯一 id（与底层 Entity.InstanceId 相同）。</summary>
        public string InstanceId;

        /// <summary>对应的 <see cref="BuildingConfig.ConfigId"/>。</summary>
        public string ConfigId;

        /// <summary>原始配置引用。</summary>
        public BuildingConfig Config;

        /// <summary>底层 Entity（已挂 IDamageable 等）。</summary>
        public Entity Entity;

        /// <summary>当前状态。</summary>
        public BuildingState State = BuildingState.Constructing;

        /// <summary>剩余需要 deliver 的材料：itemId → remainingAmount。</summary>
        public Dictionary<string, int> Remaining = new Dictionary<string, int>();

        /// <summary>建造 HUD 的 GameObject（可能为 null —— 已完成 / 永远不需要 HUD 的建筑）。</summary>
        public GameObject CostHudHost;
    }
}
