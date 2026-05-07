using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Config
{
    [Serializable]
    public class PerlinMapConfig : EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config.PerlinMapConfig
    {
        public PerlinMapConfig() { }

        public PerlinMapConfig(string configId, string displayName) : base(configId, displayName) { }
    }
}
