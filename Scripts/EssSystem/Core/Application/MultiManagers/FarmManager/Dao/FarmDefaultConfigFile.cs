using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.FarmManager.Dao
{
    [Serializable]
    public class FarmDefaultConfigFile
    {
        public List<FarmConfig> FarmConfigs = new();
        public List<CropConfig> CropConfigs = new();
    }
}
