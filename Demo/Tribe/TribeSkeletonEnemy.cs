using System.Collections.Generic;
using Demo.Tribe.Player;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.EntityManager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config;
using UnityEngine;

namespace Demo.Tribe
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class TribeSkeletonEnemy : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maxHp = 10f;
        [SerializeField, Min(0f)] private float _moveSpeed = 1.2f;
        [SerializeField, Min(0f)] private float _patrolDistance = 2.5f;
        [SerializeField, Min(0f)] private float _contactDamage = 8f;
        [SerializeField, Min(0.1f)] private float _damageCooldown = 1f;
        [SerializeField, Min(0.01f)] private float _animationFrameTime = 0.1f;
        [SerializeField] private Vector3 _healthBarOffset = new Vector3(0f, 0.85f, 0f);

        private readonly List<Sprite> _idleFrames = new List<Sprite>();
        private readonly List<Sprite> _walkFrames = new List<Sprite>();
        private readonly List<Sprite> _currentFrames = new List<Sprite>();
        private SpriteRenderer _renderer;
        private Rigidbody2D _rb;
        private BoxCollider2D _collider;
        private Transform _healthFill;
        private float _hp;
        private float _spawnX;
        private float _nextDamageTime;
        private float _animTimer;
        private int _frameIndex;
        private int _direction = 1;
        private int _lastAnimationGroup = -1;
        private bool _dead;
        private string _entityInstanceId;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<BoxCollider2D>();
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _collider.isTrigger = true;
            _collider.size = new Vector2(0.65f, 0.9f);
            _collider.offset = new Vector2(0f, 0.05f);
            _hp = _maxHp;
            _spawnX = transform.position.x;
        }

        private void Start()
        {
            LoadAnimationFrames();
            CreateHealthBar();
            RegisterEntity();
            ApplyHealthBar();
        }

        private void Update()
        {
            if (_dead) return;
            UpdatePatrol();
            UpdateAnimation();
            UpdateHealthBarPosition();
        }

        public void TakeHit(float damage)
        {
            if (_dead) return;
            _hp = Mathf.Max(0f, _hp - Mathf.Max(1f, damage));
            ApplyHealthBar();
            if (EventProcessor.HasInstance && !string.IsNullOrEmpty(_entityInstanceId))
            {
                EventProcessor.Instance.TriggerEventMethod(
                    EntityManager.EVT_DAMAGE_ENTITY,
                    new List<object> { _entityInstanceId, Mathf.Max(1f, damage), "TribePlayerAttack" });
            }
            if (_hp <= 0f) Die();
        }

        public void TakeDamage(float damage)
        {
            TakeHit(damage);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_dead || Time.time < _nextDamageTime) return;
            var player = other.GetComponentInParent<TribePlayer>();
            if (player == null) return;
            player.TakeDamage(_contactDamage);
            _nextDamageTime = Time.time + _damageCooldown;
        }

        private void UpdatePatrol()
        {
            var nextX = transform.position.x + _direction * _moveSpeed * Time.deltaTime;
            if (_patrolDistance > 0f && Mathf.Abs(nextX - _spawnX) > _patrolDistance)
            {
                _direction *= -1;
                _frameIndex = 0;
                _lastAnimationGroup = -1;
                nextX = Mathf.Clamp(nextX, _spawnX - _patrolDistance, _spawnX + _patrolDistance);
            }
            transform.position = new Vector3(nextX, transform.position.y, transform.position.z);
        }

        private void UpdateAnimation()
        {
            RefreshCurrentFrames();
            if (_currentFrames.Count == 0) return;
            _animTimer += Time.deltaTime;
            if (_animTimer < _animationFrameTime) return;
            _animTimer -= _animationFrameTime;
            _frameIndex = (_frameIndex + 1) % _currentFrames.Count;
            _renderer.sprite = _currentFrames[_frameIndex];
        }

        private void LoadAnimationFrames()
        {
            LoadFrames("Tribe/Entity/Skeleton 01_idle (16x16)", _idleFrames);
            LoadFrames("Tribe/Entity/Skeleton 01_walk (16x16)", _walkFrames);
            RefreshCurrentFrames();
            if (_currentFrames.Count > 0) _renderer.sprite = _currentFrames[0];
        }

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
            var group = _direction < 0 ? 1 : 3;
            var source = _moveSpeed > 0f && _walkFrames.Count >= 16 ? _walkFrames : _idleFrames;
            if (group == _lastAnimationGroup && _currentFrames.Count > 0) return;

            _lastAnimationGroup = group;
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

        private void CreateHealthBar()
        {
            var root = new GameObject("HealthBar");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _healthBarOffset;

            var bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            bg.transform.localScale = new Vector3(0.7f, 0.08f, 1f);
            var bgRenderer = bg.AddComponent<SpriteRenderer>();
            bgRenderer.sprite = CreatePixelSprite();
            bgRenderer.color = new Color(0f, 0f, 0f, 0.75f);
            bgRenderer.sortingOrder = _renderer.sortingOrder + 10;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(root.transform, false);
            fill.transform.localPosition = Vector3.zero;
            fill.transform.localScale = new Vector3(0.66f, 0.045f, 1f);
            var fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = CreatePixelSprite();
            fillRenderer.color = new Color(0.85f, 0.1f, 0.1f, 1f);
            fillRenderer.sortingOrder = _renderer.sortingOrder + 11;
            _healthFill = fill.transform;
        }

        private void UpdateHealthBarPosition()
        {
            if (_healthFill == null) return;
            _healthFill.parent.localPosition = _healthBarOffset;
        }

        private void ApplyHealthBar()
        {
            if (_healthFill == null) return;
            var percent = Mathf.Clamp01(_hp / Mathf.Max(1f, _maxHp));
            _healthFill.localScale = new Vector3(0.66f * percent, 0.045f, 1f);
            _healthFill.localPosition = new Vector3(-0.33f * (1f - percent), 0f, 0f);
        }

        private void RegisterEntity()
        {
            if (!EventProcessor.HasInstance || !string.IsNullOrEmpty(_entityInstanceId)) return;
            _entityInstanceId = $"{gameObject.name}_{GetInstanceID()}";
            var definition = new EntityRuntimeDefinition
            {
                Kind = EntityKind.Dynamic,
                Collider = new EntityColliderConfig(EntityColliderShape.Box, _collider.size, _collider.offset, true),
                CanMove = true,
                MoveSpeed = _moveSpeed,
                CanBeAttacked = true,
                MaxHp = _maxHp,
                CanAttack = true,
                AttackPower = _contactDamage,
                AttackCooldown = _damageCooldown,
                Died = _ => Die()
            };
            EventProcessor.Instance.TriggerEventMethod(
                EntityManager.EVT_REGISTER_SCENE_ENTITY,
                new List<object> { _entityInstanceId, gameObject, definition });
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            Destroy(gameObject);
        }

        private static Sprite CreatePixelSprite()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
