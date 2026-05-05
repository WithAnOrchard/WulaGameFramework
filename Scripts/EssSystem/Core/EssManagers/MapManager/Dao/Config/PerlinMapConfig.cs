using System;

namespace EssSystem.EssManager.MapManager.Dao.Config
{
    [Serializable]
    public class PerlinMapConfig : EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Config.PerlinMapConfig
    {
        public PerlinMapConfig() { }

        public PerlinMapConfig(string configId, string displayName) : base(configId, displayName) { }
    }
}
