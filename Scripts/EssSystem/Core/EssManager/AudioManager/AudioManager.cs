using System.Collections.Generic;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.EventManager;

namespace EssSystem.EssManager.AudioManager
{
    //管理器层 - 编排层，所有操作通过Event触发，禁止直接调用Service
    public class AudioManager : ManagerBase
    {
        private bool _hasInit;

        public static AudioManager Instance => InstanceWithInit<AudioManager>(instance => { instance.Init(true); });

        public void Init(bool logMessage)
        {
            if (_hasInit) return;
            _hasInit = true;
            //通过Event初始化Service层
            EventManager.Instance.TriggerEvent("InitAudioEvent");
        }

        //通过Event创建音频源
        public void CreateAudioSource(string audioSourceId)
        {
            EventManager.Instance.TriggerEvent("CreateAudioSourceEvent", new List<object> { audioSourceId });
        }

        //通过Event播放音频
        public void PlaySound(string audioSourceId, string soundKey, float volume, float pitch = 1f)
        {
            EventManager.Instance.TriggerEvent("PlayAudioEvent", new List<object>
            {
                audioSourceId, soundKey, volume.ToString(), pitch.ToString()
            });
        }

        //通过Event停止音频
        public void StopSound(string audioSourceId)
        {
            EventManager.Instance.TriggerEvent("StopAudioEvent", new List<object> { audioSourceId });
        }

        //通过Event播放BGM(循环)
        public void PlayBGM(string audioSourceId, string soundKey, float volume)
        {
            EventManager.Instance.TriggerEvent("PlayBGMEvent", new List<object>
            {
                audioSourceId, soundKey, volume.ToString()
            });
        }

        //通过Event暂停音频
        public void PauseSound(string audioSourceId)
        {
            EventManager.Instance.TriggerEvent("PauseSoundEvent", new List<object> { audioSourceId });
        }

        //通过Event恢复音频
        public void ResumeSound(string audioSourceId)
        {
            EventManager.Instance.TriggerEvent("ResumeSoundEvent", new List<object> { audioSourceId });
        }

        //通过Event播放叠加音效
        public void PlayOneShot(string audioSourceId, string soundKey, float volume)
        {
            EventManager.Instance.TriggerEvent("PlayOneShotEvent", new List<object>
            {
                audioSourceId, soundKey, volume.ToString()
            });
        }
    }
}