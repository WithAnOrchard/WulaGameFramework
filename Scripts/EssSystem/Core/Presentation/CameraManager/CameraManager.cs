using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.Presentation.CameraManager
{
    /// <summary>
    /// 相机管理器 —— 主相机引用、跟随、震屏、缩放、世界↔屏幕坐标转换。
    /// <para>外部模块通过 <see cref="EventProcessor"/> 触发 <c>EVT_*</c> 调用；
    /// <see cref="GetMainCamera"/> 优先返 Inspector 注入的 <c>_mainCamera</c>，否则回落 <see cref="Camera.main"/>。</para>
    /// </summary>
    [Manager(4)]
    public class CameraManager : Manager<CameraManager>
    {
        // ============================================================
        // Event 常量（对外 API）
        // ============================================================
        /// <summary>取主相机引用。data: 空。返回 Ok(Camera) / Fail。</summary>
        public const string EVT_GET_MAIN_CAMERA = "GetMainCamera";
        /// <summary>设置跟随目标。data: [Transform target, Vector3 offset?]. 返回 Ok / Fail。</summary>
        public const string EVT_FOLLOW_TARGET   = "FollowCameraTarget";
        /// <summary>停止跟随。data: 空。返回 Ok。</summary>
        public const string EVT_STOP_FOLLOW     = "StopCameraFollow";
        /// <summary>震屏。data: [float amplitude, float duration, int frequency?=20]. 返回 Ok。</summary>
        public const string EVT_SHAKE           = "ShakeCamera";
        /// <summary>设置缩放（正交=orthoSize，透视=fieldOfView）。data: [float value, float duration?=0]. 返回 Ok。</summary>
        public const string EVT_SET_ZOOM        = "SetCameraZoom";
        /// <summary>世界→屏幕。data: [Vector3 worldPos]. 返回 Ok(Vector2 screen) / Fail。</summary>
        public const string EVT_WORLD_TO_SCREEN = "WorldToScreenPoint";
        /// <summary>屏幕→世界。data: [Vector2 screenPos, float zDistance?=10]. 返回 Ok(Vector3 world) / Fail。</summary>
        public const string EVT_SCREEN_TO_WORLD = "ScreenToWorldPoint";
        /// <summary>瞬间设置相机世界坐标。data: [Vector3 worldPos]. 返回 Ok。</summary>
        public const string EVT_SET_POSITION    = "SetCameraPosition";
        /// <summary>瞬间相机朝向某点。data: [Vector3 worldPoint]. 返回 Ok。</summary>
        public const string EVT_LOOK_AT         = "LookCameraAt";

        // ============================================================
        // Inspector
        // ============================================================
        [Header("Main Camera")]
        [Tooltip("主相机引用；为空则启动时回落 Camera.main")]
        [SerializeField] private Camera _mainCamera;

        [Header("Follow")]
        [Tooltip("跟随平滑时间（秒）；越小越紧、0 = 瞬间锁定")]
        [SerializeField, Min(0f)] private float _followSmoothTime = 0.15f;

        [Tooltip("默认跟随偏移（叠加在 target.position 上）")]
        [SerializeField] private Vector3 _followOffset = Vector3.zero;

        [Header("Shake")]
        [Tooltip("震屏强度全局倍率，0 = 关闭（晕动症辅助）")]
        [SerializeField, Range(0f, 2f)] private float _shakeIntensityMultiplier = 1f;

        public CameraService Service => CameraService.Instance;

        // ============================================================
        // 运行时状态
        // ============================================================
        private Transform _followTarget;
        private Vector3   _followCurrentOffset;
        private Vector3   _followVelocity;

        private float   _shakeAmplitude;
        private float   _shakeTimeRemaining;
        private float   _shakeTotalDuration;
        private int     _shakeFrequency;
        private Vector3 _shakeBasePosition;

        private float _zoomTarget;
        private float _zoomFromValue;
        private float _zoomDuration;
        private float _zoomTimer;
        private bool  _zoomActive;

        // ============================================================
        // 生命周期
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null)
                LogWarning("CameraManager: 未找到主相机（场景中无 Tag=MainCamera 的相机）");
            else
                Log($"CameraManager 初始化完成，主相机=\"{_mainCamera.name}\"", Color.green);
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        // ============================================================
        // 每帧驱动（LateUpdate 保证先于 UI / 后于其它 Update）
        // ============================================================
        private void LateUpdate()
        {
            if (_mainCamera == null) return;

            UpdateZoom();
            UpdateFollow();
            UpdateShake();
        }

        private void UpdateFollow()
        {
            if (_followTarget == null) return;
            var desired = _followTarget.position + _followCurrentOffset;
            _shakeBasePosition = _followSmoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(_shakeBasePosition, desired, ref _followVelocity, _followSmoothTime);
            _mainCamera.transform.position = _shakeBasePosition;
        }

        private void UpdateShake()
        {
            if (_shakeTimeRemaining <= 0f) return;
            _shakeTimeRemaining -= Time.deltaTime;
            if (_shakeTimeRemaining <= 0f) return;

            // 衰减强度（线性）
            var t = _shakeTimeRemaining / _shakeTotalDuration;
            var amp = _shakeAmplitude * t * _shakeIntensityMultiplier;

            // 高频随机噪声偏移（XY 平面，对 2D / 3D 通用）
            var seed = Time.time * _shakeFrequency;
            var offset = new Vector3(
                (Mathf.PerlinNoise(seed, 0f) * 2f - 1f) * amp,
                (Mathf.PerlinNoise(0f, seed) * 2f - 1f) * amp,
                0f);

            _mainCamera.transform.position = (_followTarget != null ? _shakeBasePosition : _mainCamera.transform.position) + offset;
        }

        private void UpdateZoom()
        {
            if (!_zoomActive) return;

            if (_zoomDuration <= 0f)
            {
                ApplyZoom(_zoomTarget);
                _zoomActive = false;
                return;
            }

            _zoomTimer += Time.deltaTime;
            var t = Mathf.Clamp01(_zoomTimer / _zoomDuration);
            ApplyZoom(Mathf.Lerp(_zoomFromValue, _zoomTarget, t));
            if (t >= 1f) _zoomActive = false;
        }

        private void ApplyZoom(float v)
        {
            if (_mainCamera == null) return;
            if (_mainCamera.orthographic) _mainCamera.orthographicSize = Mathf.Max(0.01f, v);
            else                          _mainCamera.fieldOfView = Mathf.Clamp(v, 1f, 179f);
        }

        // ============================================================
        // C# API（Service 同名 typed helper，§4 命名规范）
        // ============================================================
        public Camera GetMainCamera() => _mainCamera != null ? _mainCamera : (_mainCamera = Camera.main);

        public void FollowTarget(Transform target, Vector3? offset = null)
        {
            _followTarget = target;
            _followCurrentOffset = offset ?? _followOffset;
            _followVelocity = Vector3.zero;
            if (_mainCamera != null) _shakeBasePosition = _mainCamera.transform.position;
        }

        public void StopFollow()
        {
            _followTarget = null;
            _followVelocity = Vector3.zero;
        }

        public void Shake(float amplitude, float duration, int frequency = 20)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            _shakeAmplitude     = amplitude;
            _shakeTotalDuration = duration;
            _shakeTimeRemaining = duration;
            _shakeFrequency     = Mathf.Max(1, frequency);
            if (_mainCamera != null && _followTarget == null)
                _shakeBasePosition = _mainCamera.transform.position;
        }

        public void SetZoom(float value, float duration = 0f)
        {
            if (_mainCamera == null) return;
            _zoomTarget    = value;
            _zoomDuration  = Mathf.Max(0f, duration);
            _zoomTimer     = 0f;
            _zoomFromValue = _mainCamera.orthographic ? _mainCamera.orthographicSize : _mainCamera.fieldOfView;
            _zoomActive    = true;
        }

        public void SetPosition(Vector3 worldPos)
        {
            if (_mainCamera == null) return;
            _mainCamera.transform.position = worldPos;
            _shakeBasePosition = worldPos;
            _followVelocity = Vector3.zero;
        }

        public void LookAt(Vector3 worldPoint)
        {
            if (_mainCamera == null) return;
            _mainCamera.transform.LookAt(worldPoint);
        }

        // ============================================================
        // Event API
        // ============================================================
        [Event(EVT_GET_MAIN_CAMERA)]
        public List<object> OnGetMainCamera(List<object> data)
        {
            var cam = GetMainCamera();
            return cam != null ? ResultCode.Ok(cam) : ResultCode.Fail("主相机不存在");
        }

        [Event(EVT_FOLLOW_TARGET)]
        public List<object> OnFollowTarget(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Transform t))
                return ResultCode.Fail("参数 [0] 需为 Transform");
            var offset = data.Count > 1 && data[1] is Vector3 v ? (Vector3?)v : null;
            FollowTarget(t, offset);
            return ResultCode.Ok();
        }

        [Event(EVT_STOP_FOLLOW)]
        public List<object> OnStopFollow(List<object> data) { StopFollow(); return ResultCode.Ok(); }

        [Event(EVT_SHAKE)]
        public List<object> OnShake(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is float amp) || !(data[1] is float dur))
                return ResultCode.Fail("参数需 [float amplitude, float duration, int frequency?]");
            var freq = data.Count > 2 && data[2] is int f ? f : 20;
            Shake(amp, dur, freq);
            return ResultCode.Ok();
        }

        [Event(EVT_SET_ZOOM)]
        public List<object> OnSetZoom(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is float value))
                return ResultCode.Fail("参数 [0] 需为 float");
            var dur = data.Count > 1 && data[1] is float d ? d : 0f;
            SetZoom(value, dur);
            return ResultCode.Ok();
        }

        [Event(EVT_WORLD_TO_SCREEN)]
        public List<object> OnWorldToScreen(List<object> data)
        {
            if (_mainCamera == null) return ResultCode.Fail("主相机不存在");
            if (data == null || data.Count < 1 || !(data[0] is Vector3 world))
                return ResultCode.Fail("参数 [0] 需为 Vector3");
            var sp = _mainCamera.WorldToScreenPoint(world);
            return ResultCode.Ok(new Vector2(sp.x, sp.y));
        }

        [Event(EVT_SCREEN_TO_WORLD)]
        public List<object> OnScreenToWorld(List<object> data)
        {
            if (_mainCamera == null) return ResultCode.Fail("主相机不存在");
            if (data == null || data.Count < 1 || !(data[0] is Vector2 screen))
                return ResultCode.Fail("参数 [0] 需为 Vector2");
            var z = data.Count > 1 && data[1] is float zd ? zd : 10f;
            var world = _mainCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
            return ResultCode.Ok(world);
        }

        [Event(EVT_SET_POSITION)]
        public List<object> OnSetPosition(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Vector3 v))
                return ResultCode.Fail("参数 [0] 需为 Vector3");
            SetPosition(v);
            return ResultCode.Ok();
        }

        [Event(EVT_LOOK_AT)]
        public List<object> OnLookAt(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Vector3 v))
                return ResultCode.Fail("参数 [0] 需为 Vector3");
            LookAt(v);
            return ResultCode.Ok();
        }
    }
}
