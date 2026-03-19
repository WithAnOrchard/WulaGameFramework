using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.AbstractClass;
using EssSystem.Core.Dao;
using EssSystem.EssManager.AudioManager.Dao;
using Unity.VisualScripting;
using UnityEngine;

namespace EssSystem.EssManager.AudioManager
{
    public class AudioService: ServiceBase
    {
        [Header("音频设置")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _uiVolume = 1f;
        [SerializeField] private float _fadeDuration = 1f;
        
        
        public static AudioService Instance => InstanceWithInit<AudioService>(instance =>
        {
          Instance.DataSpaces.Add("AudioSource",new Dictionary<string, object>());
        });


        public void PlaySound(string audioSource, string soundKey, float volume, float pitch)
        {
            if (Instance.DataSpaces["AudioSource"].ContainsKey(audioSource))
            {
                AudioSource source = Instance.DataSpaces["AudioSource"][audioSource] as AudioSource;
                source.clip = ResourceLoaderManager.Instance.AudioClips[soundKey];
                source.pitch = pitch;
                source.volume = volume * _sfxVolume * _masterVolume;
                source.Play();
            }
        }

        public void StopSound(string targetAudioSource)
        {
            // foreach (var audioData in audioDatas)
            // {
            //     if (audioData.id.Equals(targetAudioSource))
            //     {
            //         audioData.source.Stop();
            //     }
            // }

        }
        
        public void CreateAudioSource(string audioSourceId)
        {
            // AudioData audioData = new AudioData();
            // GameObject target=new  GameObject();
            // audioData.source = target.AddComponent<AudioSource>();
            // target.name = audioSourceId;
            // // target.transform.SetParent(transform);
            // audioData.id = audioSourceId;
            // audioDatas.Add(audioData);
        }
    }
}