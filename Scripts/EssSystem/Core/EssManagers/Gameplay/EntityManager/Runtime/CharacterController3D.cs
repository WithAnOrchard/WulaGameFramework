using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime
{
    /// <summary>
    /// 通用 3D 角色物理控制器。Capsule + Rigidbody 实现，提供：
    /// <list type="bullet">
    /// <item>外部驱动的水平移动（<see cref="MoveInput"/>）+ 冲刺（<see cref="SprintInput"/>）</item>
    /// <item>跳跃请求（<see cref="RequestJump"/>）—— 仅地面起跳</item>
    /// <item>额外重力（<see cref="GravityScale"/>）—— Unity 默认 g=9.81 之上叠加</item>
    /// <item>地面检测：OverlapSphere（即时几何） || OnCollisionStay 法线（物理接触）|| 任意接触+vY 近零</item>
    /// <item>零摩擦 PhysicMaterial —— 贴墙跳跃不被墙摩擦蹭掉 vY</item>
    /// </list>
    /// <para>
    /// <b>不读输入</b>：调用方（PlayerController / AI）每帧设 <see cref="MoveInput"/>。
    /// <b>不管动画</b>：通过 <see cref="IsMoving"/> / <see cref="IsGrounded"/> 暴露状态供动画层使用。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class CharacterController3D : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        #region Inspector

        [Header("Movement")]
        [Tooltip("基础移动速度（m/s）。")]
        [SerializeField, Min(0.1f)] private float _speed = 5f;

        [Tooltip("Sprint 时的速度倍率。")]
        [SerializeField, Min(1f)] private float _sprintMultiplier = 2f;

        [Tooltip("低于此速度阈值视为停下。")]
        [SerializeField, Min(0f)] private float _idleSpeedEpsilon = 0.05f;

        [Header("Jump")]
        [Tooltip("跳跃初速度（m/s）。GravityScale=2.5 / g=9.81 下：vJump=10 → 跳 ~2.04 m。")]
        [SerializeField, Min(0f)] private float _jumpSpeed = 10f;

        [Tooltip("重力倍率。Unity 默认 1（g=9.81）；MC 风约 2.5（更紧凑跳跃手感）。")]
        [SerializeField, Min(0.1f)] private float _gravityScale = 2.5f;

        [Header("Physics")]
        [Tooltip("CapsuleCollider 半径。")]
        [SerializeField, Min(0.05f)] private float _colliderRadius = 0.4f;

        [Tooltip("CapsuleCollider 高度。")]
        [SerializeField, Min(0.2f)] private float _colliderHeight = 1.8f;

        [Tooltip("Rigidbody 阻尼。⚠ Drag 同时作用所有轴；> 0 会衰减跳跃 Y 速度。本控制器 XZ 直接 set velocity（无输入即 0）不需要 drag —— 默认 0。")]
        [SerializeField, Min(0f)] private float _linearDrag = 0f;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>水平移动方向（XZ 平面，长度 ≤ 1，由调用方转到世界方向）。</summary>
        public Vector3 MoveInput { get; set; }

        /// <summary>是否处于冲刺。</summary>
        public bool SprintInput { get; set; }

        /// <summary>下次 FixedUpdate 起跳（仅地面有效；自动消费）。</summary>
        public void RequestJump() => _jumpRequested = true;

        /// <summary>当前是否在地面（综合 3 路判定）。</summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>当前是否在移动（按 <see cref="_idleSpeedEpsilon"/> 阈值）。</summary>
        public bool IsMoving => MoveInput.sqrMagnitude > _idleSpeedEpsilon * _idleSpeedEpsilon;

        /// <summary>读取/覆盖速度（直接代理 Rigidbody.velocity）。</summary>
        public Vector3 Velocity { get => _rb.velocity; set => _rb.velocity = value; }

        public float Speed             { get => _speed;            set => _speed = Mathf.Max(0.1f, value); }
        public float SprintMultiplier  { get => _sprintMultiplier; set => _sprintMultiplier = Mathf.Max(1f, value); }
        public float JumpSpeed         { get => _jumpSpeed;        set => _jumpSpeed = Mathf.Max(0f, value); }
        public float GravityScale      { get => _gravityScale;     set => _gravityScale = Mathf.Max(0.1f, value); }

        /// <summary>地面状态变化广播（true=落地, false=离地）。</summary>
        public event System.Action<bool> OnGroundedChanged;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Runtime

        private Rigidbody _rb;
        private CapsuleCollider _collider;

        private bool _jumpRequested;
        private bool _isGrounded;
        private bool _stayGrounded;          // OnCollisionStay 设：本物理步内有任何法线 y > 0.3 的接触
        private bool _anyContact;            // OnCollisionStay 设：本物理步任意非 self 接触

        // OverlapSphere 缓存 buffer（避免每帧分配）
        private static readonly Collider[] _groundProbeBuf = new Collider[8];

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<CapsuleCollider>();
            ConfigurePhysics();
        }

        private void ConfigurePhysics()
        {
            _rb.useGravity = true;
            // 冻结所有旋转轴 —— 朝向完全由代码控制；冻结 Y 避免撞墙时被扭矩带转
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.drag = _linearDrag;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            _collider.radius = _colliderRadius;
            _collider.height = _colliderHeight;
            _collider.direction = 1; // Y 轴
            _collider.center = new Vector3(0f, _colliderHeight * 0.5f, 0f);
            _collider.isTrigger = false;

            // 零摩擦 PhysicMaterial —— 贴墙起跳时墙面摩擦不再沿 -Y 把 vY 蹭掉
            _collider.material = new PhysicMaterial("CharacterController3D_NoFriction")
            {
                dynamicFriction  = 0f,
                staticFriction   = 0f,
                bounciness       = 0f,
                frictionCombine  = PhysicMaterialCombine.Minimum,
                bounceCombine    = PhysicMaterialCombine.Minimum,
            };
        }

        private void FixedUpdate()
        {
            // 1) 地面检测（三路 OR）
            var probeCenter = transform.position + Vector3.down * 0.25f;
            var probeRadius = Mathf.Max(_colliderRadius * 0.9f, 0.1f);
            var hits = Physics.OverlapSphereNonAlloc(probeCenter, probeRadius, _groundProbeBuf, ~0, QueryTriggerInteraction.Ignore);
            var sphereGrounded = false;
            for (var i = 0; i < hits; i++)
            {
                if (_groundProbeBuf[i] != _collider)
                {
                    sphereGrounded = true;
                    break;
                }
            }
            var velYSmall = Mathf.Abs(_rb.velocity.y) < 1f;
            var newGrounded = sphereGrounded || _stayGrounded || (_anyContact && velYSmall);
            if (newGrounded != _isGrounded)
            {
                _isGrounded = newGrounded;
                OnGroundedChanged?.Invoke(_isGrounded);
            }
            _stayGrounded = false;
            _anyContact   = false;

            // 2) XZ 速度（保留 Y）
            var speed  = _speed * (SprintInput ? _sprintMultiplier : 1f);
            var v      = _rb.velocity;
            var planar = MoveInput * speed;
            v.x = planar.x; v.z = planar.z;

            // 3) 跳跃（仅地面）
            if (_jumpRequested && _isGrounded) v.y = _jumpSpeed;
            _jumpRequested = false;

            _rb.velocity = v;

            // 4) 额外重力
            if (_gravityScale > 1f)
            {
                var extra = (_gravityScale - 1f) * Physics.gravity;
                _rb.AddForce(extra, ForceMode.Acceleration);
            }
        }

        private void OnCollisionStay(Collision col)
        {
            _anyContact = true;
            if (_stayGrounded) return;
            for (int i = 0, n = col.contactCount; i < n; i++)
            {
                if (col.GetContact(i).normal.y > 0.3f)
                {
                    _stayGrounded = true;
                    return;
                }
            }
        }

        #endregion
    }
}
