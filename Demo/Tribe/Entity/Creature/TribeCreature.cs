using System.Collections.Generic;
using System.Linq;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;
using Demo.Tribe;
using UnityEngine;

namespace Demo.Tribe.Entities
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
        private Transform _characterRoot;          // CharacterViewBridge.CreateCharacter 返回的根

        /// <summary>视觉角色根节点 —— 由 CharacterManager 创建 + 持有 SpriteRenderer 的子节点。
        /// 业务侧（如 GiantSlimeState）需要直接操作视觉时（缩放 / 染色）读此引用。
        /// 返回 null 时说明角色尚未创建（Start 前 / 配置缺失）。</summary>
        public Transform CharacterRoot => _characterRoot;
        private TribeCreatureHealthUI _healthBar;
        private TribeCreatureContactDamager _contactDamager;

        private string _entityInstanceId;
        private Entity _entity;
        private IDamageable _damageable;
        private IBrain _brain;
        private bool _dead;
        private int _sortingOrder;
        private int _lastDirection;
        private bool _lastMoving;

        /// <summary>外部生成时设视觉的 sortingOrder（透传给 Character 子节点的 SpriteRenderer）。</summary>
        public int SortingOrder
        {
            get => _sortingOrder;
            set { _sortingOrder = value; ApplySortingOrderToCharacter(); }
        }

        private void ApplySortingOrderToCharacter()
        {
            if (_characterRoot == null) return;
            // Body part 上的 SpriteRenderer —— CharacterPartView2D 创建一个 SR；递归找一个即可
            var sr = _characterRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) sr.sortingOrder = _sortingOrder;
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
            // 注：视觉走 CharacterManager；在 RegisterEntityAndBrain 之后由 BuildCharacterView 创建
            if (_config.CanAttack)
            {
                // HealthBar 需要 _entityInstanceId，挪到 RegisterEntityAndBrain 之后
                BuildContactDamager();
            }
            RegisterEntityAndBrain();
            BuildCharacterView();
            if (_config.CanAttack) BuildHealthBar();
        }

        private void Update()
        {
            if (_dead || _config == null || _brain == null) return;

            // ─── 动画同步（CharacterManager 走 CharacterViewBridge） ───
            if (string.IsNullOrEmpty(_entityInstanceId)) return;
            var ctx = _brain.Context;
            if (ctx.FacingDirection != _lastDirection)
            {
                _lastDirection = ctx.FacingDirection;
                CharacterViewBridge.SetDirection(_entityInstanceId, ctx.FacingDirection);
            }
            if (ctx.IsMoving != _lastMoving)
            {
                _lastMoving = ctx.IsMoving;
                CharacterViewBridge.PlayLocomotion(_entityInstanceId, ctx.IsMoving, grounded: true);
            }
        }

        // ─── 子模块构建 ──────────────────────────────
        private void ConfigureRigidbody()
        {
            _rb.gravityScale = _config.UseGravity ? _config.GravityScale : 0f;
            _rb.bodyType = _config.UseGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            _rb.linearDamping = 0f;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            // 防止玩家（mass=1）把怪物当肉垫推：调高 mass 让玩家几乎推不动；
            // 同时让 brain 写 rb.velocity 时不被玩家挤压抹平 → 接触伤害可正常 OnCollisionStay2D 触发。
            // 注：velocity 直接写不受 mass 影响；只影响 collision resolve / AddForce。
            _rb.mass = 50f;

            var c = RigidbodyConstraints2D.FreezeRotation;
            if (_config.UseGravity && _config.FreezePositionX)
                c |= RigidbodyConstraints2D.FreezePositionX;
            _rb.constraints = c;

            _collider.radius = _config.ColliderRadius;
            _collider.isTrigger = !_config.UseGravity;

            // 物理 layer：怪物之间不碰、怪物不推掉落物（参 TribeCollisionLayers）
            TribeCollisionLayers.MarkCreature(gameObject);
        }

        /// <summary>通过 <see cref="CharacterViewBridge"/> 创建视觉角色 —— 走框架统一管线。
        /// <para>所有视觉细节（sheet 路径 / frame rate / scale / Y 偏移 / 方向变体）已在
        /// <c>TribeCreatureConfig.CharacterConfigId</c> 对应的 CharacterConfig 中预注册。</para></summary>
        private void BuildCharacterView()
        {
            if (string.IsNullOrEmpty(_entityInstanceId)) return;
            if (string.IsNullOrEmpty(_config.CharacterConfigId))
            {
                Debug.LogWarning($"[TribeCreature] {gameObject.name} 未设置 CharacterConfigId，无视觉。");
                return;
            }
            _characterRoot = CharacterViewBridge.CreateCharacter(
                _config.CharacterConfigId, _entityInstanceId,
                parent: transform, worldPosition: transform.position);
            if (_characterRoot != null)
            {
                ApplySortingOrderToCharacter();
                // 立即播一次 Idle + 写初始 Direction，避免首帧黑屏 / 朝向错乱
                CharacterViewBridge.PlayLocomotion(_entityInstanceId, moving: false, grounded: true);
                CharacterViewBridge.SetDirection(_entityInstanceId, _lastDirection != 0 ? _lastDirection : 1);
            }
        }

        private void BuildHealthBar()
        {
            _healthBar = gameObject.GetComponent<TribeCreatureHealthUI>();
            if (_healthBar == null) _healthBar = gameObject.AddComponent<TribeCreatureHealthUI>();
            _healthBar.Build(_entityInstanceId ?? gameObject.name + "_" + GetInstanceID());
            _healthBar.SetValue(_config.MaxHp, _config.MaxHp);
        }

        private void BuildContactDamager()
        {
            _contactDamager = gameObject.AddComponent<TribeCreatureContactDamager>();
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

            // 跳跃式移动（史莱姆专用）：完全跳过 Brain 巡游 / 追击 / 逃跑，
            // 改由 TribeSlimeHopBehavior 自驱动。Brain 不挂、_brain 保持 null 即可，
            // ContactDamage / Flash / Knockback 仍走 Entity 通道，不受影响。
            if (_config.UseHopMovement)
            {
                var hop = gameObject.AddComponent<TribeSlimeHopBehavior>();
                hop.Configure(_config);
                hop.BindEntity(_entity);
                return;
            }

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
                {
                    // 注意：事件名是 "InventorySpawnPickableItem"（参 InventoryManager.EVT_SPAWN_PICKABLE_ITEM），
                    // 旧代码写成 "SpawnPickableItem" 会静默匹配失败 → 怪物从不掉物。这里同步修正。
                    // 参数顺序：[pickableId, worldPosition, targetInventoryId?, amount?]
                    var dropResult = EventProcessor.Instance.TriggerEventMethod(
                        "InventorySpawnPickableItem",
                        new List<object> { _config.DropPickableId, transform.position, "player", _config.DropAmount });
                    if (dropResult != null && dropResult.Count >= 2 && dropResult[1] is GameObject dropGo)
                        TribeCollisionLayers.MarkDrop(dropGo);
                }
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }
    }
}
