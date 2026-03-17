using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.Singleton;
using EssSystem.EssManager.AudioManager.Dao;
using UnityEngine;

namespace EssSystem.EssManager.AudioManager
{
    public class AudioManager : ManagerBase
    {
        public static AudioManager Instance => InstanceWithInit<AudioManager>(instance =>
        {
            instance.Init(true);
        });

        
        public void Init(bool logMessage)
        {
            
        }

        
     
    }
}
