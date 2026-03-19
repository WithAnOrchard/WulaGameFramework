using UnityEngine;

namespace EssSystem.EssManager.AudioManager.Dao
{
    public class AudioData:Core.Dao.Dao
    {
        //需要播放声音的播放器
        public AudioSource TargetAudioSource;
        //需要播放的音频
        public string TargetAudio;
    }
}