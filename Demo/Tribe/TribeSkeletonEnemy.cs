using System.Collections.Generic;
using Demo.Tribe.Enemy;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.EntityManager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
// §4.1 跨模块 EntityManager 事件常量走 bare-string。
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config;
using UnityEngine;

namespace Demo.Tribe
{
    /// <summary>
    /// 部落 Demo 骷髅敌人 —— **编排器**：物理结构完全照搬 <see cref="Demo.Tribe.Player.TribePlayer"/>：
    /// <list type="bullet">
    /// <item><b>根 GameObject</b>：scale = 1，挂 <see cref="Rigidbody2D"/> + <see cref="CircleCollider2D"/> + 本类 + ContactDamager + HealthUI</item>
    /// <item><b>子节点 "Visual"</b>：scale = <see cref="_visualScale"/>，挂 <see cref="SpriteRenderer"/> + <see cref="TribeSkeletonAnimator"/></item>
    /// </list>
    /// 这样 collider 永远在 scale = 1 的世界空间，半径 = <see cref="_colliderRadius"/>（与玩家一致），
    /// 不会因 visualScale 放大碰撞体导致初始穿透地板。
    /// <para>注册为场景 Entity（<c>EVT_REGISTER_SCENE_ENTITY</c>），获得 Damageable / Attacker / Movable；
    /// 额外挂 <see cref="HorizontalPatrolComponent"/> 实现横向往返；血条走 UIManager。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class TribeSkeletonEnemy : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField, Min(1f)] private float _maxHp = 10f;
        [SerializeField, Min(0f)] private float _moveSpeed = 1.2f;
        [SerializeField, Min(0f)] private float _patrolDistance = 2.5f;
        [SerializeField, Min(0f)] private float _contactDamage = 8f;
        [SerializeField, Min(0.1f)] private float _damageCooldown = 1f;

        [Header("Visual")]
        [Tooltip("视觉子节点的缩放（仅作用于 Visual 子 GameObject，不影响物理）。")]
        [SerializeField, Min(0.1f)] private float _visualScale = 10f;

        [Tooltip("视觉子节点相对根 transform 的本地 Y 偏移；典型让 sprite 中心位于碰撞体顶部。")]
        [SerializeField] private float _visualYOffset = 0f;

        [Header("Physics (与 TribePlayer 一致)")]
        [SerializeField] private bool _useGravity = true;
        [SerializeField, Min(0f)] private float _gravityScale = 5f;
        [SerializeField, Min(0f)] private float _linearDrag = 0f;

        [Tooltip("启用重力时是否冻结 X —— 冻结后玩家撞不动怪物；巡逻通过逻辑模式（直写 transform.position.x）绕开。")]
        [SerializeField] private bool _freezePositionXWhenGravity = true;

        [Header("Collider (圆形，与玩家一致)")]
        [Tooltip("CircleCollider2D 半径（世界单位，根 transform 不缩放）。")]
        [SerializeField, Min(0.05f)] private float _colliderRadius = 0.45f;

        // ─── 运行时 ─────────────────────────────────────────────
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private GameObject _visual;
        private SpriteRenderer _renderer;
        private TribeSkeletonAnimator _animator;
        private TribeEnemyHealthUI _healthBar;
        private TribeEnemyContactDamager _contactDamager;
        private int _sortingOrder;

        private string _entityInstanceId;
        private Entity _entity;
        private IDamageable _damageable;
        private IPatrol _patrol;
        private bool _dead;

        /// <summary>外部生成时设视觉的 sortingOrder（在 Start 之前调用才会生效；之后调用会立即应用）。</summary>
        public int SortingOrder
        {
            get => _sortingOrder;
            set { _sortingOrder = value; if (_renderer != null) _renderer.sortingOrder = value; }
        }

        // ─── 生命周期 ───────────────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            ConfigureRigidbody();
        }

        private void Start()
        {
            BuildVisual();
            BuildAnimator();
            BuildHealthBar();
            BuildContactDamager();
            RegisterEntityAndPatrol();
        }

        private void Update()
        {
            if (_dead) return;
            _patrol?.Tick(Time.deltaTime);
            if (_animator != null && _patrol != null)
            {
                _animator.SetDirection(_patrol.Direction);
                _animator.SetWalking(_patrol.IsMoving);
            }
            _animator?.Tick(Time.deltaTime);

            // 闪烁 / 击退由 EntityService.Tick 自动驱动（ITickableCapability）
        }

        // 受击：由框架 EntityHandle.TakeDamage / EVT_DAMAGE_ENTITY 入口完成；本类不再暴露 TakeHit。

        // ─── 子模块构建 ─────────────────────────────────────────
        /// <summary>与玩家 ConfigureRigidbody 一致：Dynamic + FreezeRotation [+ FreezePositionX]，CircleCollider2D 设半径。</summary>
        private void ConfigureRigidbody()
        {
            _rb.gravityScale = _useGravity ? _gravityScale : 0f;
            _rb.bodyType = _useGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            _rb.drag = _linearDrag;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var c = RigidbodyConstraints2D.FreezeRotation;
            if (_useGravity && _freezePositionXWhenGravity) c |= RigidbodyConstraints2D.FreezePositionX;
            _rb.constraints = c;

            _collider.radius = _colliderRadius;
            _collider.isTrigger = !_useGravity;

            Debug.Log($"[TribeSkeletonEnemy] Rigidbody 配置: gravityScale={_rb.gravityScale}, bodyType={_rb.bodyType}, " +
                     $"constraints={_rb.constraints}, collider.isTrigger={_collider.isTrigger}, " +
                     $"_useGravity={_useGravity}, _freezePositionXWhenGravity={_freezePositionXWhenGravity}");
        }

        /// <summary>创建视觉子 GameObject —— 仅它承担 _visualScale 缩放，根 transform 保持 1。</summary>
        private void BuildVisual()
        {
            // 如果根上有遗留的 SpriteRenderer（来自旧版自动 AddComponent），移除
            var legacySR = GetComponent<SpriteRenderer>();
            if (legacySR != null) Destroy(legacySR);

            var existing = transform.Find("Visual");
            _visual = existing != null ? existing.gameObject : new GameObject("Visual");
            _visual.transform.SetParent(transform, false);
            _visual.transform.localPosition = new Vector3(0f, _visualYOffset, 0f);
            _visual.transform.localScale = Vector3.one * _visualScale;

            // 不用 ??：Unity 的 fake-null 会让 ?? 跳过 AddComponent 分支，造成 _renderer == null 但进不了 if 。
            _renderer = _visual.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = _visual.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = _sortingOrder;
        }

        private void BuildAnimator()
        {
            _animator = _visual.GetComponent<TribeSkeletonAnimator>();
            if (_animator == null) _animator = _visual.AddComponent<TribeSkeletonAnimator>();
            _animator.LoadFrames();
        }

        private void BuildHealthBar()
        {
            _healthBar = gameObject.GetComponent<TribeEnemyHealthUI>();
            if (_healthBar == null) _healthBar = gameObject.AddComponent<TribeEnemyHealthUI>();
            _healthBar.Build(_entityInstanceId ?? gameObject.name + "_" + GetInstanceID());
            _healthBar.SetValue(_maxHp, _maxHp);
        }

        /// <summary>创建接触伤害组件。</summary>
        private void BuildContactDamager()
        {
            var damager = gameObject.AddComponent<TribeEnemyContactDamager>();
            damager.Configure(_contactDamage, _damageCooldown);
        }

        private void RegisterEntityAndPatrol()
        {
            if (!EventProcessor.HasInstance || !string.IsNullOrEmpty(_entityInstanceId)) return;
            _entityInstanceId = $"{gameObject.name}_{GetInstanceID()}";
            var definition = new EntityRuntimeDefinition
            {
                Kind = EntityKind.Dynamic,
                Collider = new EntityColliderConfig(EntityColliderShape.Circle,
                    new Vector2(_colliderRadius, _colliderRadius), Vector2.zero, !_useGravity),
                CanMove = true,
                EnableFlashEffect = true,
                FlashDuration = 0.15f,
                FlashColor = Color.white, // 全白闪烁
                EnableKnockbackEffect = true,
                KnockbackForce = 15f,
                MoveSpeed = _moveSpeed,
                CanBeAttacked = true,
                MaxHp = _maxHp,
                CanAttack = true,
                AttackPower = _contactDamage,
                AttackCooldown = _damageCooldown,
                Died = _ => Die(),
            };
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterSceneEntity",
                new List<object> { _entityInstanceId, gameObject, definition });

            _entity = EntityManager.Instance != null ? EntityManager.Instance.Service.GetEntity(_entityInstanceId) : null;
            if (_entity == null)
            {
                Debug.LogWarning($"[TribeSkeletonEnemy] 取回 Entity 失败: {_entityInstanceId}");
                return;
            }

            _damageable = _entity.Get<IDamageable>();
            if (_damageable != null) _damageable.Damaged += OnDamaged;

            // 闪烁 / 击退由框架 RegisterSceneEntity 根据 definition 标志自动挂载

            // 巡逻走逻辑模式（传 null）—— 重力开启时 X 被冻结，velocity.x 无效，只能改 transform.position.x。
            _patrol = _entity.Add<IPatrol>(new HorizontalPatrolComponent(_moveSpeed, _patrolDistance, null));
        }

        // ─── 事件回调 ───────────────────────────────────────────
        private void OnDamaged(Entity owner, Entity source, float dealt, string damageType)
        {
            if (_damageable == null || _healthBar == null) return;
            _healthBar.SetValue(_damageable.CurrentHp, _damageable.MaxHp);
            // 受伤效果由 EntityService.TryDamage 自动触发，无需手动调用
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            if (_patrol != null) _patrol.Paused = true;
            if (_contactDamager != null) _contactDamager.Enabled = false;
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
            if (_healthBar != null) _healthBar.Dispose();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }
    }
}
