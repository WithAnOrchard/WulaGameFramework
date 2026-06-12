using System;
using System.Collections.Generic;

namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    [Serializable]
    public class CharacterDefaultConfigFile
    {
        public List<CharacterConfig> CharacterConfigs = new();
    }
}
