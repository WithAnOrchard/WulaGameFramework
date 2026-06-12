using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao
{
    [Serializable]
    public class VoxelDefaultConfigFile
    {
        public List<VoxelMapConfig> MapConfigs = new();
    }
}
