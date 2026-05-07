using System;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn.Dao
{
    /// <summary>
    /// 体素实体生成规则集合（与 2D <c>EntitySpawnRuleSet</c> 平行）。
    /// 一个 ConfigId 可绑定多个规则集（叠加生效），由 <c>VoxelEntitySpawnService</c> 显式绑定到 ConfigId。
    /// <para>持久化：以 <c>"{configId}::{ruleSetId}"</c> 为 key 写入 <c>VoxelEntitySpawnService._dataStorage</c>。</para>
    /// </summary>
    [Serializable]
    public class VoxelEntitySpawnRuleSet
    {
        public string Id;
        public List<VoxelEntitySpawnRule> Rules = new();

        public VoxelEntitySpawnRuleSet() { }
        public VoxelEntitySpawnRuleSet(string id) { Id = id; }

        public VoxelEntitySpawnRuleSet WithRule(VoxelEntitySpawnRule rule)
        {
            Rules.Add(rule);
            return this;
        }
    }
}
