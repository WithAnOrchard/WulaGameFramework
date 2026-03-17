using EssSystem.Core;
using EssSystem.Core.AbstractClass;
using UnityEngine;

namespace EssSystem.EssManager.AudioManager
{
    public class AudioService: ServiceBase
    {
        public static AudioService Instance => InstanceWithInit<AudioService>(instance =>
        {
            
        });

       
        public void PlaySound(string targetAudioSource, string soundName,float pitch,bool replace)
        {
            // foreach (var audioData in audioDatas)
            // {
            //     if (audioData.id.Equals(targetAudioSource))
            //     {
            //         AudioClip aduio = ResourceLoaderManager.Instance.GetAudio(soundName);
            //         audioData.source.pitch = pitch;
            //         if (replace || !audioData.source.isPlaying)
            //         {
            //             if (aduio)
            //             {
            //                 audioData.source.clip = aduio;
            //                 audioData.source.Play();
            //             }
            //         }
            //     }
            // }
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