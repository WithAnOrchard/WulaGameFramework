using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.AbstractClass;
using UnityEngine;

namespace EssSystem.EssManager.AudioManager
{
    public class AudioService : ServiceBase
    {
        //当前BGM的AudioSource ID
        private string _currentBgmSourceId = "";

        private float _fadeDuration = 1f;

        //音频设置
        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private float _uiVolume = 1f;

        public static AudioService Instance => InstanceWithInit<AudioService>(instance =>
        {
            if (!instance.DataSpaces.ContainsKey("AudioSource"))
                instance.DataSpaces.Add("AudioSource", new Dictionary<string, object>());
        });

        //获取AudioSource
        public AudioSource GetAudioSource(string audioSourceId)
        {
            if (DataSpaces["AudioSource"].ContainsKey(audioSourceId))
                return DataSpaces["AudioSource"][audioSourceId] as AudioSource;
            return null;
        }

        //播放音效(一次性)
        public void PlaySound(string audioSourceId, string soundKey, float volume, float pitch)
        {
            var source = GetAudioSource(audioSourceId);
            if (source != null)
            {
                var clip = ResourceLoaderManager.Instance.GetAudio(soundKey);
                if (clip == null)
                {
                    Debug.LogWarning($"未找到音频: {soundKey}");
                    return;
                }

                source.clip = clip;
                source.pitch = pitch;
                source.volume = volume * _sfxVolume * _masterVolume;
                source.loop = false;
                source.Play();
            }
            else
            {
                Debug.LogWarning($"未找到AudioSource: {audioSourceId}");
            }
        }

        //播放BGM(循环)
        public void PlayBGM(string audioSourceId, string soundKey, float volume)
        {
            var source = GetAudioSource(audioSourceId);
            if (source == null)
            {
                Debug.LogWarning($"未找到AudioSource: {audioSourceId}");
                return;
            }

            var clip = ResourceLoaderManager.Instance.GetAudio(soundKey);
            if (clip == null)
            {
                Debug.LogWarning($"未找到音频: {soundKey}");
                return;
            }

            //停止旧BGM
            if (!string.IsNullOrEmpty(_currentBgmSourceId) && _currentBgmSourceId != audioSourceId)
                StopSound(_currentBgmSourceId);

            source.clip = clip;
            source.volume = volume * _musicVolume * _masterVolume;
            source.loop = true;
            source.Play();
            _currentBgmSourceId = audioSourceId;
        }

        //停止音频
        public void StopSound(string audioSourceId)
        {
            var source = GetAudioSource(audioSourceId);
            if (source != null) source.Stop();
        }

        //暂停音频
        public void PauseSound(string audioSourceId)
        {
            var source = GetAudioSource(audioSourceId);
            if (source != null) source.Pause();
        }

        //恢复音频
        public void ResumeSound(string audioSourceId)
        {
            var source = GetAudioSource(audioSourceId);
            if (source != null) source.UnPause();
        }

        //播放OneShot(不打断当前播放,适合叠加音效)
        public void PlayOneShot(string audioSourceId, string soundKey, float volume)
        {
            var source = GetAudioSource(audioSourceId);
            if (source == null)
            {
                Debug.LogWarning($"未找到AudioSource: {audioSourceId}");
                return;
            }

            var clip = ResourceLoaderManager.Instance.GetAudio(soundKey);
            if (clip == null)
            {
                Debug.LogWarning($"未找到音频: {soundKey}");
                return;
            }

            source.PlayOneShot(clip, volume * _sfxVolume * _masterVolume);
        }

        //创建音频源并存入DataSpaces
        public void CreateAudioSource(string audioSourceId, Transform parent)
        {
            if (DataSpaces["AudioSource"].ContainsKey(audioSourceId))
            {
                Debug.LogWarning($"AudioSource已存在: {audioSourceId}");
                return;
            }

            var target = new GameObject();
            target.name = audioSourceId;
            if (parent != null) target.transform.SetParent(parent);
            var source = target.AddComponent<AudioSource>();
            DataSpaces["AudioSource"][audioSourceId] = source;
        }

        //设置音量
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
        }

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        public void SetUIVolume(float volume)
        {
            _uiVolume = Mathf.Clamp01(volume);
        }

        //设置淡入淡出时长
        public void SetFadeDuration(float duration)
        {
            _fadeDuration = Mathf.Max(0.1f, duration);
        }

        //获取音量设置
        public float GetMasterVolume()
        {
            return _masterVolume;
        }

        public float GetMusicVolume()
        {
            return _musicVolume;
        }

        public float GetSfxVolume()
        {
            return _sfxVolume;
        }

        public float GetUIVolume()
        {
            return _uiVolume;
        }
    }
}