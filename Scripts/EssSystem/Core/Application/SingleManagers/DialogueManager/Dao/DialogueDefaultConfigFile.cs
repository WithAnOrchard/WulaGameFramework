using System;
using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.Specs;

namespace EssSystem.Core.Application.SingleManagers.DialogueManager.Dao
{
    [Serializable]
    public class DialogueDefaultConfigFile
    {
        public List<DialogueConfig> Configs = new();
        public List<Dialogue> Dialogues = new();
    }
}
