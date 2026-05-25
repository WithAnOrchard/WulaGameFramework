using Demo.DobeCat.Sys.Platform.Windows;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Win32 = Demo.DobeCat.Sys.Platform.Windows.Win32Native;
#endif

namespace Demo.DobeCat.Sys
{
    /// <summary>
    /// 根据用户键鼠活跃状态动态调整 Application.targetFrameRate，降低桌宠空闲时的 CPU 占用。
    /// <list type="table">
    /// <listheader><term>状态</term><term>帧率</term><term>触发条件</term></listheader>
    /// <item><term>活跃</term><term>60 fps</term><term>键鼠在 _idleThresholdSeconds 内有输入</term></item>
    /// <item><term>空闲</term><term>10 fps</term><term>用户键鼠长时间无操作</term></item>
    /// </list>
    /// DESIGN.md §2.2 帧率策略
    /// </summary>
    public class PetFrameRateController : MonoBehaviour
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

        private void Awake()
        {
            // Awake 中初始化，避免 AddComponent 到 Start 之间那一帧仍用旧帧率
            QualitySettings.vSyncCount = 0;
            SetFps(_activeFps);
        }

        private void Update()
        {
            // 全屏应用前台时降到最低帧率
            if (ForegroundSensor.Instance != null && ForegroundSensor.Instance.IsFullscreen)
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

        private void SetFps(int fps)
        {
            _currentFps = fps;
            Application.targetFrameRate = fps;
        }
    }
}
