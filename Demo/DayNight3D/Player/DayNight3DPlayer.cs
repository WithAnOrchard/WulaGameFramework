using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;

namespace Demo.DayNight3D.Player
{
    /// <summary>
    /// 3D 昼夜 Demo 玩家 —— 薄胶水：把三个框架级组件粘起来：
    /// <list type="bullet">
    /// <item><see cref="CharacterAnimatorBinder"/> —— 角色绑定 + 动作播放（Character Manager）</item>
    /// <item><see cref="CharacterController3D"/> —— Capsule 物理 + 移动 + 跳跃（Entity Manager）</item>
    /// <item><see cref="CameraController3D"/> —— 第一/第三人称相机（Entity Manager）</item>
    /// </list>
    /// <para>
    /// 本组件只做：读 WASD/Sprint/Jump 输入 → 设给 Controller；按 IsMoving/Sprint 切动画 action；
    /// 同步玩家朝向到相机 yaw（FP）或移动方向（TP）；FP 切换时自动隐藏自身模型。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController3D))]
    [RequireComponent(typeof(CameraController3D))]
    [RequireComponent(typeof(CharacterAnimatorBinder))]
    public class DayNight3DPlayer : MonoBehaviour
    {
        [Header("Actions（Inspector 提供 idle/walk/run/jump 名；Binder 会做关键词 fallback 解析）")]
        [SerializeField] private string _idleAction = "Idle";
        [SerializeField] private string _walkAction = "Walk";
        [SerializeField] private string _runAction  = "Run";
        [SerializeField] private string _jumpAction = "Jump";

        [Header("Input")]
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;

        [Header("Turning")]
        [Tooltip("第三人称下玩家转身到移动方向的角速度（度/秒）。")]
        [SerializeField, Min(0f)] private float _rotationSpeed = 720f;

        // ─────────────────────────────────────────────────────────────
        #region Runtime

        private CharacterController3D    _ctrl;
        private CameraController3D       _cam;
        private CharacterAnimatorBinder  _binder;

        private string _resolvedIdle;
        private string _resolvedWalk;
        private string _resolvedRun;
        private string _resolvedJump;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Public API（保留给 GameManager 在 AddComponent 后覆盖默认 ConfigId / InstanceId）

        public CharacterAnimatorBinder Binder => _binder;
        public CharacterController3D   Controller => _ctrl;
        public CameraController3D      CameraController => _cam;
        public Transform CharacterRoot => _binder != null ? _binder.CharacterRoot : transform;

        public void SetConfigId(string configId)
        {
            EnsureRefs();
            _binder.SetConfigId(configId);
        }

        public void SetInstanceId(string instanceId)
        {
            EnsureRefs();
            _binder.SetInstanceId(instanceId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Awake() => EnsureRefs();

        private void EnsureRefs()
        {
            if (_ctrl   == null) _ctrl   = GetComponent<CharacterController3D>();
            if (_cam    == null) _cam    = GetComponent<CameraController3D>();
            if (_binder == null) _binder = GetComponent<CharacterAnimatorBinder>();
        }

        private void Start()
        {
            // 1) 相机跟随自己；FP 状态变化 → 自动 hide/show 模型
            _cam.Target = transform;
            _cam.OnFirstPersonChanged += isFP => _binder.SetModelVisible(!isFP);

            // 2) 确保角色已生成（binder.Spawn 是幂等的，autoSpawnOnStart 也会调）
            _binder.Spawn();
            _binder.SetModelVisible(!_cam.IsFirstPerson);

            // 3) 解析动作名（idle/walk/run/jump keyword fallback）
            _resolvedIdle = _binder.ResolveAction(_idleAction, "idle", "stand", "wait");
            _resolvedWalk = _binder.ResolveAction(_walkAction, "walk", "move") ?? _resolvedIdle;
            _resolvedRun  = _binder.ResolveAction(_runAction,  "run",  "sprint") ?? _resolvedWalk;
            _resolvedJump = _binder.ResolveAction(_jumpAction, "jump", "fall", "air");

            Debug.Log($"[DayNight3DPlayer] Actions resolved → Idle='{_resolvedIdle}', Walk='{_resolvedWalk}', Run='{_resolvedRun}', Jump='{_resolvedJump}'");
            if (string.IsNullOrEmpty(_resolvedIdle))
                Debug.LogWarning($"[DayNight3DPlayer] Config '{_binder.ConfigId}' 无可用动作");
            else
                _binder.Play(_resolvedIdle);
        }

        private void Update()
        {
            // 1) 读输入并转到「相机相对」方向
            var h = Input.GetAxisRaw("Horizontal");
            var v = Input.GetAxisRaw("Vertical");
            var raw = new Vector3(h, 0f, v);
            if (raw.sqrMagnitude > 1f) raw.Normalize();

            var camYawRad = _cam.Yaw * Mathf.Deg2Rad;
            var camFwd    = new Vector3(Mathf.Sin(camYawRad), 0f, Mathf.Cos(camYawRad));
            var camRight  = new Vector3(camFwd.z, 0f, -camFwd.x);
            var moveDir   = camFwd * raw.z + camRight * raw.x;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

            _ctrl.MoveInput   = moveDir;
            _ctrl.SprintInput = Input.GetKey(KeyCode.LeftShift);

            if (Input.GetKeyDown(_jumpKey)) _ctrl.RequestJump();

            // 2) 玩家转身
            if (_cam.IsFirstPerson)
            {
                // 第一人称：朝向锁到相机 yaw（鼠标 = 即时转身）
                transform.rotation = Quaternion.Euler(0f, _cam.Yaw, 0f);
            }
            else if (_ctrl.IsMoving)
            {
                // 第三人称：转到面向移动方向（角速度限制）
                var targetYaw = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
                var current   = transform.eulerAngles.y;
                var newYaw    = Mathf.MoveTowardsAngle(current, targetYaw, _rotationSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }

            // 3) 动画状态切换（优先级：Jump > Run > Walk > Idle）
            string desired;
            if (!_ctrl.IsGrounded && !string.IsNullOrEmpty(_resolvedJump)) desired = _resolvedJump;
            else if (!_ctrl.IsMoving)            desired = _resolvedIdle;
            else if (_ctrl.SprintInput && !string.IsNullOrEmpty(_resolvedRun)) desired = _resolvedRun;
            else                            desired = _resolvedWalk;

            if (!string.IsNullOrEmpty(desired)) _binder.Play(desired);
        }

        #endregion
    }
}
