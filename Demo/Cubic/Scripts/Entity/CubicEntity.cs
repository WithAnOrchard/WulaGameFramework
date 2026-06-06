using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;
using Demo.Cubic.Skill;
using Demo.Cubic.VFX;
using Demo.Cubic.Utils;

namespace Demo.Cubic.Entities
{
    /// <summary>
    /// Cubic 实体 —— 玩家 / 敌人共享的 MonoBehaviour 编排器（3D 伪 2D 版）。
    /// <para>
    /// <b>3D 物理改造点</b>（相对 2D 版）：
    /// <list type="bullet">
    /// <item><c>Rigidbody2D</c> → <c>Rigidbody</c>（3D），重力走引擎默认 gravity，<c>Move</c> 只改 X 速度、保留 Y 给跳跃/重力</item>
    /// <item><c>BoxCollider2D</c> → <c>BoxCollider</c></item>
    /// <item><c>SpriteRenderer</c> → <c>MeshRenderer</c>（低多边形 Cube），<c>ApplyJobColor</c> 改走 <see cref="Cubic3DStyle"/> 共享材质缓存</item>
    /// <item>不依赖框架 <c>IMovable</c>（框架目前只有 <c>Rigidbody2DMoverComponent</c>，与 3D 不兼容）—— 自己用 <c>Rigidbody.linearVelocity</c> 控位移</item>
    /// <item><c>EntityRuntimeDefinition.Collider.Size</c> 仍为 <see cref="Vector2"/>（框架结构不动），<c>BoxCollider.size</c> 读它的 X/Y 拼 Vector3</item>
    /// </list>
    /// </para>
    /// <para>
    /// HP / 受击仍走框架 <see cref="DamageableComponent"/>（与物理解耦，2D/3D 通用）；
    /// 屏幕闪光 / 死亡置灰等表现层由本类自己处理。
    /// </para>
    /// </summary>
    public class CubicEntity : MonoBehaviour, ISpeedAffected, IControllable
    {
        [Header("身份")]
        public CubicCharacterClass JobClass = CubicCharacterClass.Warrior;

        [Header("运行时句柄（只读）")]
        [SerializeField] protected string _instanceId;

        /// <summary>Entity 运行时引用。Start 之后才非空。</summary>
        public Entity Runtime { get; protected set; }

        /// <summary>EntityHandle 桥接（Start 之后才非空）。</summary>
        public EntityHandle Handle { get; protected set; }

        /// <summary>当前是否死亡（来自 IDamageable）。</summary>
        public bool IsDead
        {
            get
            {
                if (Runtime == null || !Runtime.Has<IDamageable>()) return false;
                var d = Runtime.Get<IDamageable>();
                return d != null && d.IsDead;
            }
        }

        protected Rigidbody _rigidbody;
        protected MeshRenderer _renderer;
        protected BoxCollider _collider;

        [Header("技能栏（按槽位索引；顺序与 CubicSkillRegistry.GetClassSkills 一致）")]
        [SerializeField] protected List<string> _skillIds = new();

        protected bool _registered;

        // 受伤闪白：缓存初始颜色，闪完恢复
        private Color _baseColor;
        private float _flashTimer;
        private const float FlashDuration = 0.12f;

        // ─── ISpeedAffected ───────────────────────────────────────
        // 速度倍率，1=正常，&lt;1=减速，&gt;1=加速。SlowEffect 等 Buff 走接口修改，Move() 应用到 Rigidbody。
        private float _speedMultiplier = 1f;
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Mathf.Max(0f, value);
        }

        // ─── IControllable ───────────────────────────────────────
        // 计数式 Push/Pop，多个 Stun/Silence Buff 共存时计数累加（与框架 ControllableComponent 同语义）。
        private int _stunCount;
        private int _silenceCount;
        public bool Stunned => _stunCount > 0;
        public bool Silenced => _silenceCount > 0;
        public void PushStun() => _stunCount++;
        public void PopStun() => _stunCount = Mathf.Max(0, _stunCount - 1);
        public void PushSilence() => _silenceCount++;
        public void PopSilence() => _silenceCount = Mathf.Max(0, _silenceCount - 1);

        // ════════════════════════════════════════════════════════════
        //  生命周期
        // ════════════════════════════════════════════════════════════

        public virtual void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<BoxCollider>();
            // MeshRenderer 可能在子物体上（玩家 / 敌人把 visual 拆成 child cube，保留朝向鼻锥等可挂件）
            _renderer = GetComponentInChildren<MeshRenderer>(includeInactive: true);
            _baseColor = _renderer != null && _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty("_BaseColor")
                ? _renderer.sharedMaterial.GetColor("_BaseColor")
                : Color.white;
            ApplyJobColor();

            // 3D 物理基础参数：锁 Z 轴旋转 + 锁 X/Y 旋转（保持立方面朝相机不倒）
            if (_rigidbody != null)
            {
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
                _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                _rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        public virtual void Start()
        {
            RegisterToEntityManager();
        }

        protected virtual void OnDestroy()
        {
            if (Runtime != null && !string.IsNullOrEmpty(_instanceId) && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEventMethod(
                    "DestroyEntity", new List<object> { _instanceId });
            }
        }

        protected virtual void Update()
        {
            // 闪白衰减
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f && _renderer != null) ApplyJobColor();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  EntityManager 注册
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 把本实体注册到 <see cref="EntityManager"/>。幂等，重复调用只生效一次。
        /// <para>注意：<see cref="EntityRuntimeDefinition.EnableKnockbackEffect"/> 在 3D 下不接 Rigidbody 推力（Cubic 自己控水平速度，框架 knockback 用 2D 刚体），
        /// 故本类把它关掉，避免出现"框架扣血后还调 2D knockback"日志噪音。</para>
        /// </summary>
        protected void RegisterToEntityManager()
        {
            if (_registered) return;
            if (!EventProcessor.HasInstance)
            {
                Debug.LogWarning($"[CubicEntity] EventProcessor 未就绪，无法注册 {name}");
                return;
            }

            _instanceId = string.IsNullOrEmpty(_instanceId)
                ? $"cubic_{JobClass}_{GetEntityId()}"
                : _instanceId;

            var def = BuildRuntimeDefinition();
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterSceneEntity", new List<object> { _instanceId, gameObject, def });

            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetEntity", new List<object> { _instanceId });
            if (result != null && result.Count > 0 && result[0] is Entity e)
            {
                Runtime = e;
                Handle = GetComponent<EntityHandle>();
                HookEntityCallbacks();
            }

            _registered = true;
            Debug.Log($"[CubicEntity] {JobClass} 已注册到 EntityManager → instanceId={_instanceId}");
        }

        /// <summary>构造 <see cref="EntityRuntimeDefinition"/>。子类可重写以调整 MoveSpeed / MaxHp / 攻击能力。</summary>
        protected virtual EntityRuntimeDefinition BuildRuntimeDefinition()
        {
            var stats = CubicJobStats.GetStats(JobClass);
            return new EntityRuntimeDefinition
            {
                Kind = EntityKind.Dynamic,
                // Cubic 自带 3D BoxCollider（PlayerController / EnemySpawner 已挂），
                // 不让框架再 AddComponent<BoxCollider2D>()，否则在 3D Rigidbody + 2D Collider 共存场景下
                // EntityService.ApplyColliderLocal 内部属性写入会 NRE。
                // Shape = None 走 EntityService 的早退路径，整段 2D collider 注入直接跳过。
                Collider = new EntityColliderConfig { Shape = EntityColliderShape.None },
                CanMove = true,
                MoveSpeed = stats.MoveSpeed,
                CanBeAttacked = true,
                MaxHp = stats.MaxHP,
                CanAttack = false,
                EnableFlashEffect = false,   // 3D 下框架 flash 改 sprite color 失效，本类自己处理
                FlashDuration = 0.15f,
                FlashColor = Color.white,
                EnableKnockbackEffect = false,  // 3D 下框架 knockback 走 Rigidbody2D，无效
                KnockbackForce = 5f,
                KnockbackDuration = 0.2f,
            };
        }

        // ════════════════════════════════════════════════════════════
        //  Entity 回调
        // ════════════════════════════════════════════════════════════

        protected virtual void HookEntityCallbacks()
        {
            if (Runtime == null) return;
            // 把自身注册成 ISpeedAffected / IControllable 能力 —— 让 Slow / Stun 等能在 3D 路径生效
            Runtime.With<ISpeedAffected>(this);
            Runtime.With<IControllable>(this);

            var dmg = Runtime.Get<IDamageable>();
            if (dmg == null) return;

            dmg.Damaged += OnDamaged;
            dmg.Died += OnDiedEvent;
        }

        // ─── IEntityCapability（ISpeedAffected 的父接口要求） ─────────
        public virtual void OnAttach(Entity owner) { }
        public virtual void OnDetach(Entity owner) { }

        protected virtual void OnDamaged(Entity self, Entity source, float damageDealt, string damageType)
        {
            Debug.Log($"[{JobClass}] 受到 {damageDealt:F1} 点伤害（type={damageType ?? "unknown"}），" +
                      $"HP={self?.Get<IDamageable>()?.CurrentHp:F1}");
            TriggerFlash();   // 自己闪白
            CubicVFXManager.PlayScreenFlash(CubicVFXManager.ScreenFlashType.Damage);
        }

        protected virtual void OnDiedEvent(Entity self, Entity killer)
        {
            Debug.Log($"[{JobClass}] 死亡！killer={(killer != null ? killer.InstanceId : "unknown")}");
            enabled = false;
            if (_renderer != null) Cubic3DStyle.ApplyJobColor(_renderer, Color.gray);
        }

        /// <summary>触发短暂闪白（受击反馈）。</summary>
        protected void TriggerFlash()
        {
            _flashTimer = FlashDuration;
            if (_renderer != null) Cubic3DStyle.ApplyJobColor(_renderer, Color.white);
        }

        // ════════════════════════════════════════════════════════════
        //  对外业务接口
        // ════════════════════════════════════════════════════════════

        public void ApplyJobColor()
        {
            if (_renderer == null) return;
            var c = CubicClassColors.GetColor(JobClass);
            _baseColor = c;
            Cubic3DStyle.ApplyJobColor(_renderer, c);
        }

        /// <summary>设置职业 —— 不重置 HP（HP 由 EntityManager 状态权威）。</summary>
        public virtual void SetJobClass(CubicCharacterClass jobClass)
        {
            JobClass = jobClass;
            ApplyJobColor();
            if (Runtime != null)
            {
                var stats = CubicJobStats.GetStats(jobClass);
                var dmg = Runtime.Get<IDamageable>();
                if (dmg is DamageableComponent dc) dc.SetMaxHp(stats.MaxHP, refill: true);
            }
        }

        /// <summary>
        /// 移动 —— 直接改 Rigidbody.linearVelocity 的 X 分量，保留 Y（重力 / 跳跃）。
        /// <paramref name="horizontalSpeed"/> 为带方向的水平速度（米/秒），正=右，负=左。
        /// <para>实际写入 = <paramref name="horizontalSpeed"/> × <see cref="SpeedMultiplier"/>（Slow / Haste 等 Buff 走 ISpeedAffected 改倍率）。</para>
        /// </summary>
        public virtual void Move(float horizontalSpeed)
        {
            if (_rigidbody == null) return;
            // Stun 状态：水平速度强制归零（Y 保留重力/跳跃惯性，让它正常落地/跳跃）
            if (Stunned)
            {
                var v = _rigidbody.linearVelocity;
                _rigidbody.linearVelocity = new Vector3(0f, v.y, 0f);
                return;
            }
            var v2 = _rigidbody.linearVelocity;
            _rigidbody.linearVelocity = new Vector3(horizontalSpeed * _speedMultiplier, v2.y, 0f);
        }

        /// <summary>跳跃 —— 给 Y 方向一个脉冲速度。PlayerController 在 grounded 时调。</summary>
        public virtual void Jump(float jumpVelocity)
        {
            if (_rigidbody == null) return;
            var v = _rigidbody.linearVelocity;
            _rigidbody.linearVelocity = new Vector3(v.x, jumpVelocity, 0f);
        }

        /// <summary>受到伤害 —— 走 EntityHandle.TakeDamage → EVT_DAMAGE_ENTITY。</summary>
        public virtual void TakeDamage(float damage, Vector3 fromPosition)
        {
            if (Handle == null) return;
            Handle.TakeDamage(damage, damageType: "skill", sourcePosition: fromPosition);
        }

        /// <summary>治疗 —— 走 IDamageable.Heal（直接调用，不走事件）。</summary>
        public virtual void Heal(float amount)
        {
            if (Runtime == null) return;
            var dmg = Runtime.Get<IDamageable>();
            if (dmg == null || dmg.IsDead) return;
            var healed = dmg.Heal(amount);
            if (healed > 0f)
            {
                Debug.Log($"[{JobClass}] 治疗 {healed:F1} HP");
                CubicVFXManager.PlayScreenFlash(CubicVFXManager.ScreenFlashType.Heal);
            }
        }

        // ─── 技能 ────────────────────────────────────────────────

        public virtual bool CastSkill(string skillId, Vector3? direction = null)
        {
            if (Runtime == null || string.IsNullOrEmpty(skillId)) return false;
            // Silence 状态：拒绝施法（与框架 IControllable 消费方语义一致）
            if (Silenced) { Debug.Log($"[{JobClass}] 被沉默，无法施法 {skillId}"); return false; }

            var vfxId = CubicSkillRegistry.VfxIdOf(skillId);
            if (!string.IsNullOrEmpty(vfxId))
                CubicVFXManager.PlaySkillVFX(vfxId, transform.position);

            var dir = direction ?? transform.right;   // 跟随 rotation.y —— PlayerController 已切到 Y 轴 180° 旋转,不再用负 scale
            EventProcessor.Instance.TriggerEventMethod(
                "CastSkill",
                new List<object> { _instanceId, skillId, null, dir, transform.position });
            return true;
        }

        public virtual void AddSkill(string skillId)
        {
            if (Runtime == null || string.IsNullOrEmpty(skillId)) return;
            if (_skillIds.Contains(skillId)) return;

            EventProcessor.Instance.TriggerEventMethod(
                "LearnSkill",
                new List<object> { _instanceId, skillId });
            _skillIds.Add(skillId);
        }

        public string GetSkillId(int slot)
        {
            return slot >= 0 && slot < _skillIds.Count ? _skillIds[slot] : null;
        }

        public string GetJobName() => CubicClassColors.GetClassName(JobClass);
    }

    /// <summary>
    /// 职业数据 —— 给 <see cref="CubicEntity.BuildRuntimeDefinition"/> 与
    /// <see cref="PlayerController"/> / <see cref="CubicEnemy"/> 共享数值。
    /// </summary>
    public static class CubicJobStats
    {
        public struct JobStats
        {
            public float MaxHP;
            public float MoveSpeed;
            public float AttackPower;
        }

        public static JobStats GetStats(CubicCharacterClass jobClass)
        {
            return jobClass switch
            {
                CubicCharacterClass.Warrior => new JobStats { MaxHP = 120f, MoveSpeed = 4f, AttackPower = 14f },
                CubicCharacterClass.Mage => new JobStats { MaxHP = 80f,  MoveSpeed = 3.5f, AttackPower = 18f },
                CubicCharacterClass.Archer => new JobStats { MaxHP = 90f, MoveSpeed = 5f, AttackPower = 12f },
                CubicCharacterClass.Paladin => new JobStats { MaxHP = 150f, MoveSpeed = 3.5f, AttackPower = 16f },
                CubicCharacterClass.Assassin => new JobStats { MaxHP = 70f, MoveSpeed = 6f, AttackPower = 15f },
                CubicCharacterClass.Engineer => new JobStats { MaxHP = 100f, MoveSpeed = 4f, AttackPower = 13f },
                CubicCharacterClass.Necromancer => new JobStats { MaxHP = 75f, MoveSpeed = 3.5f, AttackPower = 20f },
                CubicCharacterClass.Cleric => new JobStats { MaxHP = 95f, MoveSpeed = 3.8f, AttackPower = 11f },
                _ => new JobStats { MaxHP = 100f, MoveSpeed = 4f, AttackPower = 10f },
            };
        }
    }
}
