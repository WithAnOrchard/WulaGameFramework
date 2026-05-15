using UnityEngine;

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 玩家镜头跟随模块 —— 在 LateUpdate 节奏插值跟随玩家位置，可选锁定 Y。
    /// 由 <see cref="TribePlayer"/> 在 LateUpdate 调 <see cref="LateTick"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public class TribePlayerCameraFollow : MonoBehaviour
    {
        [Header("Camera Follow")]
        [SerializeField] private bool _autoFollowMainCamera = true;
        [SerializeField, Range(0f, 1f)] private float _smoothing = 0.18f;
        [SerializeField] private bool _lockY = false;
        [SerializeField] private float _lockedY = 0f;

        private Camera _cam;

        public void Initialize()
        {
            if (_autoFollowMainCamera) _cam = Camera.main;
        }

        public void LateTick()
        {
            if (!_autoFollowMainCamera || _cam == null) return;
            var p = transform.position;
            var c = _cam.transform.position;
            var targetY = _lockY ? _lockedY : p.y;
            var target = new Vector3(p.x, targetY, c.z);
            _cam.transform.position = _smoothing >= 1f
                ? target
                : Vector3.Lerp(c, target, 1f - Mathf.Pow(1f - _smoothing, Time.deltaTime * 60f));
        }

        /// <summary>外部（如 TribeGameManager）锁定镜头 Y。</summary>
        public void SetLockedY(float y, bool enable = true)
        {
            _lockedY = y;
            _lockY = enable;
            if (_cam != null && enable)
            {
                var c = _cam.transform.position;
                _cam.transform.position = new Vector3(c.x, y, c.z);
            }
        }
    }
}
