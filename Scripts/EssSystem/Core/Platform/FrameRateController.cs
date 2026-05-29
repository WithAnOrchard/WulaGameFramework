using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Win32 = EssSystem.Core.Platform.Windows.Win32Native;
#endif

namespace EssSystem.Core.Platform
{
    /// <summary>
    /// 根据用户键鼠活跃状态动态调整 <see cref="Application.targetFrameRate"/>，
    /// 降低应用空闲时的 CPU 占用。
    /// <list type="table">
    /// <listheader><term>状态</term><term>帧率</term><term>触发条件</term></listheader>
    /// <item><term>活跃</term><term>ActiveFps</term><term>键鼠在 IdleThresholdSeconds 内有输入</term></item>
    /// <item><term>空闲</term><term>IdleFps</term><term>用户键鼠长时间无操作</term></item>
    /// <item><term>全屏前台</term><term>FullscreenFps</term><term>前台全屏应用运行时</term></item>
    /// </list>
    /// </summary>
    public class FrameRateController : MonoBehaviour
    {
        [Tooltip("用户活跃时的目标帧率。")]
        [SerializeField, Min(1)] private int _activeFps = 60;

        [Tooltip("用户空闲时的目标帧率。")]
        [SerializeField, Min(1)] private int _idleFps = 60;

        [Tooltip("键鼠无输入超过此秒数后切换为空闲帧率。")]
        [SerializeField, Min(1f)] private float _idleThresholdSeconds = 5f;

        [Tooltip("前台全屏应用运行时的帧率。")]
        [SerializeField, Min(1)] private int _fullscreenFps = 60;

        private int _currentFps = -1;

        protected virtual void Awake()
        {
            QualitySettings.vSyncCount = 0;
            SetFps(_activeFps);
        }

        protected virtual void Update()
        {
            if (IsForegroundFullscreen())
            {
                if (_fullscreenFps != _currentFps) SetFps(_fullscreenFps);
                return;
            }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            float idleSec = Win32.GetIdleSeconds();
#else
            float idleSec = 0f;
#endif
            int target = idleSec >= _idleThresholdSeconds ? _idleFps : _activeFps;
            if (target != _currentFps) SetFps(target);
        }

        /// <summary>Override to provide custom fullscreen detection logic.</summary>
        protected virtual bool IsForegroundFullscreen()
        {
            return Windows.ForegroundSensor.Instance != null
                && Windows.ForegroundSensor.Instance.IsFullscreen;
        }

        private void SetFps(int fps)
        {
            _currentFps = fps;
            UnityEngine.Application.targetFrameRate = fps;
        }
    }
}
