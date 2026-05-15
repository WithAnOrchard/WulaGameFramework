using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager;
using EssSystem.Core.Base.Event;
using AudioMgr = EssSystem.Core.EssManagers.Foundation.AudioManager.AudioManager;

namespace Demo.Tribe
{
    /// <summary>
    /// 钀ョ伀缁勪欢 鈥斺€?鎾斁 spritesheet 甯у姩鐢?+ 鏍规嵁涓庣帺瀹惰窛绂绘帶鍒剁噧鐑ч煶閲忋€?    /// 閫氳繃 <see cref="CharacterService"/> 鑾峰彇鐜╁浣嶇疆锛?    /// 閫氳繃 <see cref="AudioMgr"/> 鐨?SFX 闊抽噺璁剧疆褰卞搷鏈€缁堥煶閲忋€?    /// </summary>
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
            // 绾挎€ц“鍑忥細璺濈 0 鈫?maxVolume锛岃窛绂?>= hearDistance 鈫?0
            var baseVolume = Mathf.Clamp01(1f - dist / _hearDistance) * _maxVolume;

            // 鍙?AudioManager 鐨?SFX 闊抽噺褰卞搷
            var sfxScale = AudioMgr.HasInstance ? AudioMgr.Instance.SFXVolume : 1f;
            _audio.volume = baseVolume * sfxScale;
        }

        /// <summary>
        /// 鏌ユ壘鐜╁ Transform锛氫紭鍏堥€氳繃 CharacterService 鑾峰彇瑙掕壊 View 鐨勭埗鑺傜偣锛圱ribePlayer 鏍癸級锛?        /// 鍥為€€鍒?FindObjectOfType銆?        /// </summary>
        private static Transform FindPlayerTransform()
        {
            if (CharacterService.HasInstance)
            {
                foreach (var c in CharacterService.Instance.GetAllCharacters())
                {
                    if (c.View != null)
                    {
                        // CharacterView 鏄?TribePlayer 鐨勫瓙鑺傜偣锛屽彇 parent 寰楀埌鐜╁鏍?Transform
                        var parent = c.View.transform.parent;
                        return parent != null ? parent : c.View.transform;
                    }
                }
            }
            // 鍥為€€
            var player = FindObjectOfType<Player.TribePlayer>();
            return player != null ? player.transform : null;
        }
    }
}
