using System;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Config
{
    [Serializable]
    public class PerlinMapConfig : EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config.PerlinMapConfig
    {
        public PerlinMapConfig() { }

        public PerlinMapConfig(string configId, string displayName) : base(configId, displayName) { }
    }
}
