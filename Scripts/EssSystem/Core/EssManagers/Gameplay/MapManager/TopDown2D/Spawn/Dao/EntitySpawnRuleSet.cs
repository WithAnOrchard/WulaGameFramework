using System;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn.Dao
{
    /// <summary>
    /// 规则集合。一个 MapConfig 可绑定多个规则集（叠加生效），由 <c>EntitySpawnService</c> 显式绑定到 MapConfigId。
    /// <para>持久化：以"MapConfigId::RuleSetId"为 key 写入 <c>EntitySpawnService._dataStorage</c>。</para>
    /// </summary>
    [Serializable]
    public class EntitySpawnRuleSet
    {
        /// <summary>规则集 ID（在同一 MapConfigId 下唯一）。</summary>
        public string Id;

        /// <summary>规则列表；装饰器评估前按 (Priority asc, RuleId asc) 排序保确定性。</summary>
        public List<EntitySpawnRule> Rules = new();

        public EntitySpawnRuleSet() { }
        public EntitySpawnRuleSet(string id) { Id = id; }

        public EntitySpawnRuleSet WithRule(EntitySpawnRule rule)
        {
            Rules.Add(rule);
            return this;
        }
    }
}
