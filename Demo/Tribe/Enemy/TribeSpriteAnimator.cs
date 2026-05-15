using System.Collections.Generic;
using UnityEngine;

namespace Demo.Tribe.Enemy
{
    /// <summary>
    /// 通用精灵动画器 —— 从 <see cref="TribeSkeletonAnimator"/> 泛化而来。
    /// <para>支持通过 <see cref="Setup"/> 在运行时配置资源路径，适用于所有 4行×4列 16帧 spritesheet。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TribeSpriteAnimator : MonoBehaviour
    {
        private float _frameTime = 0.1f;
        private string _idleResourcePath;
        private string _walkResourcePath;
        private SpritePivot _spritePivot = SpritePivot.Center;

        private readonly List<Sprite> _idleFrames = new List<Sprite>();
        private readonly List<Sprite> _walkFrames = new List<Sprite>();
        private readonly List<Sprite> _currentFrames = new List<Sprite>();

        private SpriteRenderer _renderer;
        private float _animTimer;
        private int _frameIndex;
        private int _direction = 1;
        private int _lastGroup = -1;
        private bool _walking = true;
        private bool _running;

        public int Direction => _direction;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>运行时配置资源路径和参数。在 <see cref="LoadFrames"/> 之前调用。</summary>
        public void Setup(string idlePath, string walkPath, float frameTime = 0.1f,
            SpritePivot pivot = SpritePivot.Center)
        {
            _idleResourcePath = idlePath;
            _walkResourcePath = walkPath;
            _frameTime = frameTime;
            _spritePivot = pivot;
        }

        /// <summary>加载帧资源；调一次即可。</summary>
        public void LoadFrames()
        {
            LoadFrames(_idleResourcePath, _idleFrames);
            LoadFrames(_walkResourcePath, _walkFrames);
            RefreshCurrentFrames();
            if (_currentFrames.Count > 0)
            {
                _renderer.sprite = _currentFrames[0];
                ApplyPivotOffset();
            }
        }

        /// <summary>设置朝向：+1 = 右，-1 = 左。</summary>
        public void SetDirection(int direction)
        {
            var d = direction >= 0 ? 1 : -1;
            if (_direction == d) return;
            _direction = d;
            _frameIndex = 0;
            _lastGroup = -1;
        }

        /// <summary>切换 walk / idle 动作组。</summary>
        public void SetWalking(bool walking)
        {
            if (_walking == walking) return;
            _walking = walking;
            _lastGroup = -1;
        }

        /// <summary>设置奔跑状态 —— 奔跑时动画加速播放。</summary>
        public void SetRunning(bool running) => _running = running;

        /// <summary>每帧推进。</summary>
        public void Tick(float deltaTime)
        {
            RefreshCurrentFrames();
            if (_currentFrames.Count == 0) return;
            _animTimer += deltaTime;
            var ft = _running ? _frameTime * 0.5f : _frameTime;
            if (_animTimer < ft) return;
            _animTimer -= ft;
            _frameIndex = (_frameIndex + 1) % _currentFrames.Count;
            _renderer.sprite = _currentFrames[_frameIndex];
            ApplyPivotOffset();
        }

        // ─── 内部 ────────────────────────────────────────────────
        private static void LoadFrames(string path, List<Sprite> target)
        {
            target.Clear();
            if (string.IsNullOrEmpty(path)) return;
            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites == null || sprites.Length == 0) return;
            target.AddRange(sprites);
            target.Sort((a, b) => GetFrameIndex(a.name).CompareTo(GetFrameIndex(b.name)));
        }

        private void RefreshCurrentFrames()
        {
            var group = _direction < 0 ? 1 : 2;
            var source = _walking && _walkFrames.Count >= 16 ? _walkFrames : _idleFrames;
            if (group == _lastGroup && _currentFrames.Count > 0) return;

            _lastGroup = group;
            _currentFrames.Clear();
            var start = group * 4;
            if (source.Count >= start + 4)
            {
                for (var i = 0; i < 4; i++) _currentFrames.Add(source[start + i]);
                _frameIndex = Mathf.Clamp(_frameIndex, 0, _currentFrames.Count - 1);
                return;
            }
            _currentFrames.AddRange(source);
            _frameIndex = Mathf.Clamp(_frameIndex, 0, Mathf.Max(0, _currentFrames.Count - 1));
        }

        private static int GetFrameIndex(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return 0;
            var underscore = spriteName.LastIndexOf('_');
            if (underscore < 0 || underscore >= spriteName.Length - 1) return 0;
            return int.TryParse(spriteName.Substring(underscore + 1), out var index) ? index : 0;
        }

        private void ApplyPivotOffset()
        {
            if (_spritePivot == SpritePivot.Center || _renderer == null || _renderer.sprite == null)
                return;
            var halfH = _renderer.sprite.bounds.extents.y;
            var pos = transform.localPosition;
            pos.y = halfH;
            transform.localPosition = pos;
        }
    }
}
