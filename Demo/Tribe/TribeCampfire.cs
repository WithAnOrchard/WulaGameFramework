using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Core.Base.Event;
using AudioMgr = EssSystem.Core.Presentation.AudioManager.AudioManager;

namespace Demo.Tribe
{
    /// <summary>
    /// 营火组件 —— 播放 spritesheet 帧动画 + 根据与玩家距离控制燃烧音量。
    /// 通过 <see cref="CharacterService"/> 获取玩家位置，
    /// 通过 <see cref="AudioMgr"/> 的 SFX 音量设置影响最终音量。
    /// </summary>
    public class TribeCampfire : MonoBehaviour
    {
        [SerializeField] private float _frameRate = 8f;
        [SerializeField] private float _hearDistance = 10f;
        [SerializeField] private float _maxVolume = 0.5f;

        private SpriteRenderer _sr;
        private Sprite[] _frames;
        private AudioSource _audio;
        private Transform _playerTransform;
        private float _timer;
        private int _currentFrame;

        public void Initialize(Sprite[] frames, AudioClip fireClip)
        {
            _sr = GetComponent<SpriteRenderer>();
            _frames = frames;
            if (_frames != null && _frames.Length > 0)
                _sr.sprite = _frames[0];

            if (fireClip != null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.clip = fireClip;
                _audio.loop = true;
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f; // 2D 模式，手动控制音量
                _audio.volume = 0f;
                _audio.Play();
            }
        }

        private void Update()
        {
            AnimateFrames();
            UpdateAudioByDistance();
        }

        private void AnimateFrames()
        {
            if (_frames == null || _frames.Length <= 1) return;
            _timer += Time.deltaTime;
            var interval = 1f / _frameRate;
            if (_timer >= interval)
            {
                _timer -= interval;
                _currentFrame = (_currentFrame + 1) % _frames.Length;
                _sr.sprite = _frames[_currentFrame];
            }
        }

        private void UpdateAudioByDistance()
        {
            if (_audio == null) return;

            // 通过 CharacterService 查找玩家 Transform（懒缓存）
            if (_playerTransform == null)
                _playerTransform = FindPlayerTransform();
            if (_playerTransform == null) return;

            var dist = Vector2.Distance(transform.position, _playerTransform.position);
            // 线性衰减：距离 0 → maxVolume，距离 >= hearDistance → 0
            var baseVolume = Mathf.Clamp01(1f - dist / _hearDistance) * _maxVolume;

            // 受 AudioManager 的 SFX 音量影响
            var sfxScale = AudioMgr.HasInstance ? AudioMgr.Instance.SFXVolume : 1f;
            _audio.volume = baseVolume * sfxScale;
        }

        /// <summary>
        /// 查找玩家 Transform：优先通过 CharacterService 获取角色 View 的父节点（TribePlayer 根），
        /// 回退到 FindObjectOfType。
        /// </summary>
        private static Transform FindPlayerTransform()
        {
            if (CharacterService.HasInstance)
            {
                foreach (var c in CharacterService.Instance.GetAllCharacters())
                {
                    if (c.View != null)
                    {
                        // CharacterView 是 TribePlayer 的子节点，取 parent 得到玩家根 Transform
                        var parent = c.View.transform.parent;
                        return parent != null ? parent : c.View.transform;
                    }
                }
            }
            // 回退
            var player = FindObjectOfType<Player.TribePlayer>();
            return player != null ? player.transform : null;
        }
    }
}
