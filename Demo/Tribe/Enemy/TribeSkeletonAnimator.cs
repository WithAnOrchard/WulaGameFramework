using System.Collections.Generic;
using UnityEngine;

namespace Demo.Tribe.Enemy
{
    /// <summary>Sprite 锚点模式 —— 决定 SpriteRenderer 在 Visual 节点内的 Y 偏移。</summary>
    public enum SpritePivot { Center, Bottom }

    /// <summary>
    /// 骨架精灵动画器 —— 加载 <c>Resources/Tribe/Entity/Skeleton 01_idle (16x16)</c> /
    /// <c>Skeleton 01_walk (16x16)</c> 子精灵，按方向 / 动作选择 4 帧子组循环播放。
    /// <para>命名约定：每张 sheet 切片后包含 16+ 帧，按行索引作为方向组（0..3）；本动画器使用组 1（左）和组 2（右）。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TribeSkeletonAnimator : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _frameTime = 0.1f;
        [SerializeField] private string _idleResourcePath = "Tribe/Entity/Skeleton 01_idle (16x16)";
        [SerializeField] private string _walkResourcePath = "Tribe/Entity/Skeleton 01_walk (16x16)";

        [Tooltip("Sprite 锚点模式：Bottom 时自动让 sprite 视觉底部对齐 Visual 节点 origin（脚踩地面）。")]
        [SerializeField] private SpritePivot _spritePivot = SpritePivot.Center;

        private readonly List<Sprite> _idleFrames = new List<Sprite>();
        private readonly List<Sprite> _walkFrames = new List<Sprite>();
        private readonly List<Sprite> _currentFrames = new List<Sprite>();

        private SpriteRenderer _renderer;
        private float _animTimer;
        private int _frameIndex;
        private int _direction = 1;
        private int _lastGroup = -1;
        private bool _walking = true;

        public int Direction => _direction;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
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

        /// <summary>每帧推进。</summary>
        public void Tick(float deltaTime)
        {
            RefreshCurrentFrames();
            if (_currentFrames.Count == 0) return;
            _animTimer += deltaTime;
            if (_animTimer < _frameTime) return;
            _animTimer -= _frameTime;
            _frameIndex = (_frameIndex + 1) % _currentFrames.Count;
            _renderer.sprite = _currentFrames[_frameIndex];
            ApplyPivotOffset();
        }

        // ─── 内部 ────────────────────────────────────────────────
        private static void LoadFrames(string path, List<Sprite> target)
        {
            var sprites = Resources.LoadAll<Sprite>(path);
            target.Clear();
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

        /// <summary>
        /// 根据 <see cref="_spritePivot"/> 调整 SpriteRenderer 本地 Y 偏移。
        /// <para>Bottom 模式：sprite 底部对齐 transform.localPosition.y=0（脚踩地面）。
        /// 偏移量 = sprite 世界高度 / 2（因为 pivot=center 时 center 在 sprite 中点）。</para>
        /// </summary>
        private void ApplyPivotOffset()
        {
            if (_spritePivot == SpritePivot.Center || _renderer == null || _renderer.sprite == null) return;
            // sprite bounds.extents.y = 世界空间半高（已含 pixelsPerUnit）
            var halfH = _renderer.sprite.bounds.extents.y;
            var pos = transform.localPosition;
            pos.y = halfH;
            transform.localPosition = pos;
        }
    }
}
