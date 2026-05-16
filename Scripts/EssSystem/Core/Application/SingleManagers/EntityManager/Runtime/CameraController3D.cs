using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Runtime
{
    /// <summary>
    /// 通用 3D 第一/第三人称相机控制器。
    /// <list type="bullet">
    /// <item>跟随 <see cref="Target"/>（通常是 PlayerController 的 transform）</item>
    /// <item>鼠标拖拽控制 <see cref="Yaw"/> / <see cref="Pitch"/>；FirstPerson 默认锁鼠标</item>
    /// <item>V 键运行时切第一/第三人称（可关）</item>
    /// <item>FirstPerson 切换时通过 <see cref="OnFirstPersonChanged"/> 通知（外部用来 hide/show 自身模型）</item>
    /// <item>强制 Camera 为 Perspective（避免场景 Camera 误设 Orthographic）</item>
    /// </list>
    /// <para>
    /// <b>相机姿态</b>由本组件独立管理；调用方可读 <see cref="Yaw"/> 用于让玩家朝向跟相机一致（FP 标配）。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraController3D : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        #region Inspector / Public

        public enum CameraMode { FirstPerson, ThirdPerson }

        [Header("Target")]
        [Tooltip("相机要跟随的目标。空则用本节点 transform。")]
        [SerializeField] private Transform _target;

        [Tooltip("用 Camera.main 自动绑定。否则用 _camera 指定。")]
        [SerializeField] private bool _useMainCamera = true;

        [Tooltip("当 _useMainCamera = false 时，绑定的 Camera。")]
        [SerializeField] private Camera _camera;

        [Header("Mode")]
        [SerializeField] private CameraMode _mode = CameraMode.FirstPerson;

        [Tooltip("V 键运行时切换 FirstPerson ↔ ThirdPerson。")]
        [SerializeField] private bool _enableModeToggleKey = true;

        [Tooltip("FirstPerson 时锁定 + 隐藏鼠标光标。")]
        [SerializeField] private bool _lockCursorInFirstPerson = true;

        [Tooltip("ThirdPerson 时也锁定鼠标光标（让相机始终跟鼠标转）。Esc 由 Unity 自动暂时释放。")]
        [SerializeField] private bool _lockCursorInThirdPerson = true;

        [Header("Mouse")]
        [SerializeField, Min(0f)] private float _mouseSensitivity = 2f;

        [SerializeField, Range(-89f, 89f)] private float _pitch = 0f;
        [SerializeField] private float _yaw = 0f;

        [Header("First Person")]
        [Tooltip("相机在 target 本地空间的头部偏移。z 略大让相机在头部前方避免看到自己头颅背面。")]
        [SerializeField] private Vector3 _firstPersonHeadOffset = new Vector3(0f, 1.7f, 0.35f);

        [Header("Third Person")]
        [SerializeField] private Vector3 _thirdPersonOffset    = new Vector3(0f, 1.8f, -4f);
        [SerializeField, Min(0f)] private float _thirdPersonLookHeight = 1.4f;
        [Tooltip("第三人称相机平滑度。1 = 即时跟随（推荐，消除奔跑抖动）；< 1 软跟随但跑步时会有 lag/抖。")]
        [SerializeField, Range(0f, 1f)] private float _thirdPersonSmoothing = 1f;

        [Header("Camera Settings")]
        [SerializeField, Range(20f, 120f)] private float _fieldOfView = 60f;
        [SerializeField, Min(0.01f)] private float _nearClipPlane = 0.05f;
        [SerializeField, Min(1f)] private float _farClipPlane = 500f;

        // ─── Public state ─────────────────────────────────────────
        public Transform Target { get => _target;        set => _target = value; }
        public CameraMode Mode  { get => _mode;          set { if (_mode != value) { _mode = value; ApplyModeSideEffects(); } } }
        public float Yaw        { get => _yaw;           set => _yaw = value; }
        public float Pitch      { get => _pitch;         set => _pitch = Mathf.Clamp(value, -89f, 89f); }

        public bool IsFirstPerson => _mode == CameraMode.FirstPerson;

        /// <summary>FirstPerson 状态变化广播（true=进入 FP；订阅方常用于 hide/show 模型）。</summary>
        public event System.Action<bool> OnFirstPersonChanged;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        private void Start()
        {
            if (_target == null) _target = transform;
            if (_camera == null && _useMainCamera) _camera = Camera.main;
            ConfigureCamera();
            ApplyModeSideEffects();
        }

        private void ConfigureCamera()
        {
            if (_camera == null) return;
            _camera.orthographic  = false;
            _camera.fieldOfView   = _fieldOfView;
            _camera.nearClipPlane = _nearClipPlane;
            _camera.farClipPlane  = _farClipPlane;
        }

        private void ApplyModeSideEffects()
        {
            // 鼠标锁：FP / TP 各自一个开关，默认两者都锁，让相机始终跟鼠标
            var shouldLock = IsFirstPerson ? _lockCursorInFirstPerson : _lockCursorInThirdPerson;
            if (shouldLock)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible   = true;
            }
            OnFirstPersonChanged?.Invoke(IsFirstPerson);
        }

        private void Update()
        {
            // V 切换
            if (_enableModeToggleKey && Input.GetKeyDown(KeyCode.V))
                Mode = IsFirstPerson ? CameraMode.ThirdPerson : CameraMode.FirstPerson;

            // 鼠标 → yaw / pitch；FP 与 TP 都始终跟随鼠标（不需按右键）。
            // 鼠标想离开窗口时按 Esc，Unity 会自动把 CursorLockMode.Locked 释放为 None。
            _yaw   += Input.GetAxis("Mouse X") * _mouseSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * _mouseSensitivity;
            _pitch  = Mathf.Clamp(_pitch, IsFirstPerson ? -89f : -30f, IsFirstPerson ? 89f : 70f);
        }

        private void LateUpdate()
        {
            if (_camera == null || _target == null) return;

            if (IsFirstPerson)
            {
                _camera.transform.position = _target.TransformPoint(_firstPersonHeadOffset);
                _camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                return;
            }

            // 第三人称：相机绕 target 头部 pivot 旋转 + 平滑跟随
            var pivot   = _target.position + Vector3.up * _thirdPersonLookHeight;
            var rot     = Quaternion.Euler(_pitch, _yaw, 0f);
            var desired = pivot + rot * _thirdPersonOffset;

            _camera.transform.position = _thirdPersonSmoothing >= 1f
                ? desired
                : Vector3.Lerp(_camera.transform.position, desired,
                    1f - Mathf.Pow(1f - _thirdPersonSmoothing, Time.deltaTime * 60f));
            _camera.transform.LookAt(pivot);
        }

        #endregion
    }
}
