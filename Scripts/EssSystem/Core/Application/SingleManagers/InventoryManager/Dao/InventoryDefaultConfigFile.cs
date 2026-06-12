using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao
{
    [Serializable]
    public class InventoryDefaultConfigFile
    {
        public List<InventoryItem> ItemTemplates = new();
        public List<PickableItemDefinition> PickableItems = new();
        public List<InventoryConfig> InventoryConfigs = new();
        public List<InventoryContainerDefinition> Inventories = new();
    }

    [Serializable]
    public class InventoryContainerDefinition
    {
        public string Id;
        public string DisplayName;
        public int MaxSlots = 20;
    }
}
