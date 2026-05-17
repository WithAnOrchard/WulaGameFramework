using System.Collections.Generic;
using System.Linq;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;
using UnityEngine;

namespace Demo.Tribe.Enemy
{
    /// <summary>
    /// 通用部落生物 —— 由 <see cref="TribeCreatureConfig"/> 驱动的可配置实体。
    /// <list type="bullet">
    /// <item>动物：无接触伤害、无血条</item>
    /// <item>怪物：有接触伤害、有血条</item>
    /// </list>
    /// 物理结构：根 scale=1 + Visual 子节点缩放（与 TribePlayer 同款，collider 始终在 scale=1 世界空间）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class TribeCreature : MonoBehaviour
    {
        // ─── 运行时 ──────────────────────────────────────
        private TribeCreatureConfig _config;
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private GameObject _visual;
        private SpriteRenderer _renderer;
        private TribeSpriteAnimator _animator;
        private TribeEnemyHealthUI _healthBar;
        private TribeEnemyContactDamager _contactDamager;

        private string _entityInstanceId;
        private Entity _entity;
        private IDamageable _damageable;
        private IBrain _brain;
        private bool _dead;
        private int _sortingOrder;

        /// <summary>外部生成时设视觉的 sortingOrder。</summary>
        public int SortingOrder
        {
            get => _sortingOrder;
            set { _sortingOrder = value; if (_renderer != null) _renderer.sortingOrder = value; }
        }

        /// <summary>用配置初始化生物。在 AddComponent 后立即调用（Start 前）。</summary>
        public void Configure(TribeCreatureConfig config)
        {
            _config = config;
        }

        // ─── 生命周期 ────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
        }

        private void Start()
        {
            if (_config == null)
            {
                Debug.LogWarning($"[TribeCreature] {gameObject.name} 缺少配置，跳过初始化");
                return;
            }
            ConfigureRigidbody();
            BuildVisual();
            BuildAnimator();
            if (_config.CanAttack)
            {
                BuildHealthBar();
                BuildContactDamager();
            }
            RegisterEntityAndBrain();
        }

        private void Update()
        {
            if (_dead || _config == null) return;

            // ─── 动画同步 ───
            if (_animator != null && _brain != null)
            {
                var ctx = _brain.Context;
                _animator.SetDirection(ctx.FacingDirection);
                _animator.SetWalking(ctx.IsMoving);
                _animator.SetRunning(ctx.IsRunning);
            }
            _animator?.Tick(Time.deltaTime);
        }

        // ─── 子模块构建 ──────────────────────────────
        private void ConfigureRigidbody()
        {
            _rb.gravityScale = _config.UseGravity ? _config.GravityScale : 0f;
            _rb.bodyType = _config.UseGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            _rb.drag = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var c = RigidbodyConstraints2D.FreezeRotation;
            if (_config.UseGravity && _config.FreezePositionX)
                c |= RigidbodyConstraints2D.FreezePositionX;
            _rb.constraints = c;

            _collider.radius = _config.ColliderRadius;
            _collider.isTrigger = !_config.UseGravity;
        }

        private void BuildVisual()
        {
            var legacySR = GetComponent<SpriteRenderer>();
            if (legacySR != null) Destroy(legacySR);

            var existing = transform.Find("Visual");
            _visual = existing != null ? existing.gameObject : new GameObject("Visual");
            _visual.transform.SetParent(transform, false);
            _visual.transform.localPosition = new Vector3(0f, _config.VisualYOffset, 0f);
            _visual.transform.localScale = Vector3.one * _config.VisualScale;

            _renderer = _visual.GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = _visual.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = _sortingOrder;
        }

        private void BuildAnimator()
        {
            _animator = _visual.GetComponent<TribeSpriteAnimator>();
            if (_animator == null) _animator = _visual.AddComponent<TribeSpriteAnimator>();
            _animator.Setup(_config.IdleResourcePath, _config.WalkResourcePath,
                _config.FrameTime, _config.Pivot);
            _animator.LoadFrames();
        }

        private void BuildHealthBar()
        {
            _healthBar = gameObject.GetComponent<TribeEnemyHealthUI>();
            if (_healthBar == null) _healthBar = gameObject.AddComponent<TribeEnemyHealthUI>();
            _healthBar.Build(_entityInstanceId ?? gameObject.name + "_" + GetInstanceID());
            _healthBar.SetValue(_config.MaxHp, _config.MaxHp);
        }

        private void BuildContactDamager()
        {
            _contactDamager = gameObject.AddComponent<TribeEnemyContactDamager>();
            _contactDamager.Configure(_config.ContactDamage, _config.DamageCooldown);
        }

        private void RegisterEntityAndBrain()
        {
            if (!EventProcessor.HasInstance || !string.IsNullOrEmpty(_entityInstanceId)) return;
            _entityInstanceId = $"{gameObject.name}_{GetInstanceID()}";

            var definition = new EntityRuntimeDefinition
            {
                Kind = EntityKind.Dynamic,
                Collider = new EntityColliderConfig(EntityColliderShape.Circle,
                    new Vector2(_config.ColliderRadius, _config.ColliderRadius), Vector2.zero,
                    !_config.UseGravity),
                CanMove = true,
                EnableFlashEffect = _config.EnableFlash,
                FlashDuration = _config.FlashDuration,
                FlashColor = _config.FlashColor,
                EnableKnockbackEffect = _config.EnableKnockback,
                KnockbackForce = _config.KnockbackForce,
                MoveSpeed = _config.MoveSpeed,
                CanBeAttacked = true,
                MaxHp = _config.MaxHp,
                CanAttack = _config.CanAttack,
                AttackPower = _config.CanAttack ? _config.ContactDamage : 0f,
                AttackCooldown = _config.DamageCooldown,
                Died = _ => Die(),
            };
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterSceneEntity",
                new List<object> { _entityInstanceId, gameObject, definition });

            _entity = EntityManager.Instance != null
                ? EntityManager.Instance.Service.GetEntity(_entityInstanceId) : null;
            if (_entity == null) return;

            _damageable = _entity.Get<IDamageable>();
            if (_damageable != null) _damageable.Damaged += OnDamaged;

            // 挂载 Brain（替代 IPatrol）
            _entity.CanThink(brain =>
            {
                brain.AddSensor(new RangeSensor(_config.CanAttack ? 6f : 5f));
                brain.WithPatrol(_config.MoveSpeed, _config.PatrolDistance);

                if (_config.CanAttack)
                {
                    // 受击后追击攻击者（Score 0.75 > Patrol 0.2，但 Flee_LowHp 是 0.85）
                    brain.Add(new Consideration
                    {
                        Id = "Chase_Aggro",
                        Score = ctx =>
                        {
                            if (ctx.ThreatSource == null) return 0f;
                            var targetDmg = ctx.ThreatSource.Get<IDamageable>();
                            if (targetDmg != null && targetDmg.IsDead) return 0f;
                            return 0.75f;
                        },
                        CreateAction = ctx => new ChaseAction(ctx.ThreatSource,
                            giveUpDistance: 12f, maxDuration: 8f, speedMultiplier: 1.8f)
                    });

                    // 低血量时逃跑（Score ≈ 0.85，压过追击）
                    brain.Add(new Consideration
                    {
                        Id = "Flee_LowHp",
                        Score = ctx =>
                        {
                            if (ctx.ThreatSource == null) return 0f;
                            return ctx.HpRatio < 0.3f ? (1f - ctx.HpRatio) * 0.85f : 0f;
                        },
                        CreateAction = ctx => new FleeAction(ctx.ThreatSource, safeDistance: 7f, maxDuration: 4f)
                    });
                }
                else
                {
                    // 动物：检测到附近威胁时逃跑
                    // 威胁 = 有 IAttacker 的实体（怪物） 或 有 IMovable 但无 IBrain 的实体（玩家）
                    // 排除同为 Brain AI 的其他动物/怪物（避免鸡被鸡吓跑）
                    brain.Add(new Consideration
                    {
                        Id = "Flee_Threatened",
                        Score = ctx =>
                        {
                            if (ctx.NearbyEntities.Count == 0) return 0f;
                            var threat = ctx.NearbyEntities
                                .FirstOrDefault(e => e.Has<IAttacker>() || (e.Has<IMovable>() && !e.Has<IBrain>()));
                            if (threat == null) return 0f;
                            ctx.ThreatSource = threat;
                            return 0.8f;
                        },
                        CreateAction = ctx => new FleeAction(ctx.ThreatSource, safeDistance: 6f, maxDuration: 3f),
                        Cooldown = 1f
                    });
                }
            });

            _brain = _entity.Get<IBrain>();
        }

        // ─── 事件回调 ────────────────────────────────
        private void OnDamaged(Entity owner, Entity source, float dealt, string damageType)
        {
            if (_damageable == null || _healthBar == null) return;
            _healthBar.SetValue(_damageable.CurrentHp, _damageable.MaxHp);
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            if (_brain != null) _brain.Enabled = false;
            if (_contactDamager != null) _contactDamager.Enabled = false;
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
            if (_healthBar != null) _healthBar.Dispose();

            // 掉落
            if (!string.IsNullOrEmpty(_config.DropPickableId) && _config.DropAmount > 0)
            {
                if (EventProcessor.HasInstance)
                    EventProcessor.Instance.TriggerEventMethod(
                        "SpawnPickableItem",
                        new List<object> { _config.DropPickableId, _config.DropAmount,
                            transform.position });
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }
    }
}
