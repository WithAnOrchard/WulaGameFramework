using System;
using System.Collections.Generic;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao
{
    [Serializable]
    public class TopDownMapDefaultConfigFile
    {
        public List<PerlinMapConfig> PerlinMapConfigs = new();
        public List<SideScrollerMapConfig> SideScrollerMapConfigs = new();
        public List<MapSpawnRuleSetBinding> SpawnRuleSets = new();
    }

    [Serializable]
    public class MapSpawnRuleSetBinding
    {
        public string MapConfigId;
        public EntitySpawnRuleSet RuleSet;
    }
}
