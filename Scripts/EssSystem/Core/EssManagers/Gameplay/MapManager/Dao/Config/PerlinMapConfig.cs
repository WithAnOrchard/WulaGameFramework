using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Config
{
    [Serializable]
    public class PerlinMapConfig : EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Templates.TopDownRandom.Config.PerlinMapConfig
    {
        public PerlinMapConfig() { }

        public PerlinMapConfig(string configId, string displayName) : base(configId, displayName) { }
    }
}
