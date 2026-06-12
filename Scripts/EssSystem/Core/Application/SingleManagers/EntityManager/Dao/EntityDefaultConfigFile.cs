using System;
using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao
{
    [Serializable]
    public class EntityDefaultConfigFile
    {
        public List<EntityConfig> EntityConfigs = new();
    }
}
