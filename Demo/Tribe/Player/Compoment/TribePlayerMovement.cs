using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default;

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 玩家移动模块 —— **能力装配器**：
    /// 把"可跳跃 / 可碰撞地面 / 可转向 / 可移动 / 可穿墙"等能力作为
    /// <see cref="IEntityCapability"/> 拼到内部 <see cref="Entity"/> 上，本组件本身不实现逻辑。
    /// <para>每帧把输入翻译成能力方法调用：<c>Move</c> / <c>Jump</c> / <c>SetFacing</c> / <c>Refresh</c>。</para>
    /// <para>同一套能力可被任何实体复用（NPC / 鬼魂 / 载具），只要换组合即可。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class TribePlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float _speed = 6f;
        [SerializeField, Min(1f)]   private float _sprintMultiplier = 2.5f;
        [SerializeField, Min(0f)]   private float _idleSpeedEpsilon = 0.05f;

        [Header("Physics")]
        [SerializeField, Min(0.05f)] private float _colliderRadius = 0.45f;
        [SerializeField, Min(0f)]    private float _linearDrag     = 0f;

        [Tooltip("是否启用横版物理：重力 ON + 仅 X 受输入驱动 + Space/W 跳跃。关闭后退为俯视自由走位。")]
        [SerializeField] private bool _useSideScrollerPhysics = true;
        [SerializeField, Min(0f)] private float _gravityScale = 5f;
        [SerializeField, Min(0f)] private float _jumpForce = 12f;
        [SerializeField, Min(0.05f)] private float _groundCheckDistance = 0.1f;
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
        [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;

        [Header("Capabilities")]
        [Tooltip("装配地面检测能力（IGroundSensor）—— 关闭后无法判定落地，跳跃约束需自行处理。")]
        [SerializeField] private bool _enableGroundSensor = true;

        [Tooltip("装配跳跃能力（IJumpable）—— 关闭则忽略跳跃键。")]
        [SerializeField] private bool _enableJumpable = true;

        [Tooltip("装配穿墙能力（IPhaseThrough）—— 装上后可通过 Entity.Get<IPhaseThrough>() 切换。")]
        [SerializeField] private bool _enablePhaseThrough = false;

        // ─── 运行时 ─────────────────────────────────────────────
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private Entity _entity;
        private Rigidbody2DMoverComponent _mover;   // 具体类型保留，方便设 Sprinting
        private IJumpable _jumpable;
        private IGroundSensor _groundSensor;
        private IFacing _facing;

        private Vector2 _pendingInput;

        // ─── 对外属性（供 Player / Combat 读取）──────────────────
        public bool Moving { get; private set; }
        public bool Grounded => _groundSensor != null ? _groundSensor.IsGrounded : true;
        public bool FacingRight => _facing == null || _facing.FacingRight;
        public float ColliderRadius => _colliderRadius;
        public bool UseSideScrollerPhysics => _useSideScrollerPhysics;

        /// <summary>暴露内部 Entity，外部可在运行时 <c>_player.Movement.Entity.Add&lt;ISomething&gt;(...)</c>。</summary>
        public Entity Entity => _entity;

        // ─── 装配 ───────────────────────────────────────────────
        public void Initialize(string instanceId)
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            ConfigureRigidbody();

            _entity = new Entity
            {
                InstanceId = instanceId,
                ConfigId = "TribePlayer",
                CharacterInstanceId = instanceId,
                CharacterRoot = transform,
                WorldPosition = transform.position,
            };

            // 必装：移动 + 朝向
            _mover = _entity.Add<IMovable>(new Rigidbody2DMoverComponent(_rb, _speed, _useSideScrollerPhysics)
            {
                SprintMultiplier = _sprintMultiplier,
            }) as Rigidbody2DMoverComponent;
            _facing = _entity.Add<IFacing>(new FacingComponent(instanceId, initialRight: true));

            // 可选装配
            if (_enableGroundSensor)
                _groundSensor = _entity.Add<IGroundSensor>(new Raycast2DGroundSensorComponent(transform, _collider, _groundCheckDistance));
            if (_enableJumpable)
                _jumpable = _entity.Add<IJumpable>(new Rigidbody2DJumpableComponent(_rb, _jumpForce));
            if (_enablePhaseThrough)
                _entity.Add<IPhaseThrough>(new ColliderPhaseThroughComponent(_collider));

            // 注册到 EntityService —— 让 EntityHandle 自动挂上，所有碰撞回调可反查 Entity
            EntityService.Instance?.AttachEntityHandle(gameObject, _entity);
        }

        private void ConfigureRigidbody()
        {
            _rb.gravityScale = _useSideScrollerPhysics ? _gravityScale : 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.drag = _linearDrag;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _collider.radius = _colliderRadius;
            _collider.isTrigger = false;
        }

        private void OnDestroy()
        {
            if (_entity != null) _entity.DetachAllCapabilities();
        }

        // ─── 每帧逻辑 ───────────────────────────────────────────
        /// <summary>读取输入，刷新地面状态、跳跃、面朝；速度施加放到 <see cref="FixedTick"/>。</summary>
        public void Tick()
        {
            var h = Input.GetAxisRaw("Horizontal");
            var v = _useSideScrollerPhysics ? 0f : Input.GetAxisRaw("Vertical");
            var dir = new Vector2(h, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            _pendingInput = dir;

            _groundSensor?.Refresh();

            if (_useSideScrollerPhysics && _jumpable != null && Grounded &&
                (Input.GetKeyDown(_jumpKey) || Input.GetKeyDown(KeyCode.Space)))
            {
                _jumpable.Jump();
            }

            Moving = _pendingInput.sqrMagnitude > _idleSpeedEpsilon * _idleSpeedEpsilon;
            if (Moving && Mathf.Abs(h) > 0.01f && _facing != null)
                _facing.SetFacingRight(h > 0f);
        }

        /// <summary>FixedUpdate：把缓存的输入交给 <see cref="IMovable"/>。</summary>
        public void FixedTick()
        {
            if (_mover == null) return;
            _mover.Sprinting = Input.GetKey(_sprintKey);
            _mover.Move(_pendingInput, Time.fixedDeltaTime);
        }
    }
}
