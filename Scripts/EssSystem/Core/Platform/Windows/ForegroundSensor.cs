using System.Text;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace EssSystem.Core.Platform.Windows
{
    /// <summary>
    /// 检测前台窗口标题与是否全屏，供 <see cref="EssSystem.Core.Platform.FrameRateController"/> 等系统进行上下文感知。
    /// 全屏独占应用（游戏 / 视频播放器等）前台时自动降帧，避免与游戏抢占资源。
    /// </summary>
    public class ForegroundSensor : MonoBehaviour
    {
        public static ForegroundSensor Instance { get; private set; }

        [Tooltip("查询间隔（秒）。不必每帧查询，2s 足够。")]
        [SerializeField, Min(0.5f)] private float _pollInterval = 2f;

        private float _timer;

        /// <summary>当前前台窗口标题（非 Windows 平台始终为空字符串）。</summary>
        public string ForegroundTitle { get; private set; } = "";

        /// <summary>
        /// 前台窗口是否为全屏独占应用（非 Unity 窗口且覆盖整个主工作区）。
        /// </summary>
        public bool IsFullscreen { get; private set; }

        private void Awake()  => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = _pollInterval;
            Poll();
        }

        private void Poll()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = Win32Native.GetForegroundWindow();
            if (hwnd == System.IntPtr.Zero) return;

            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, sb.Capacity);
            ForegroundTitle = sb.ToString();

            if (Win32Native.GetWindowRect(hwnd, out var rect))
            {
                var wa = Win32Native.GetPrimaryWorkArea();
                var waW = wa.right - wa.left;
                var waH = wa.bottom - wa.top;
                var wW  = rect.right  - rect.left;
                var wH  = rect.bottom - rect.top;
                IsFullscreen = wW >= waW && wH >= waH
                    && !ForegroundTitle.Contains("Unity");
            }
#else
            ForegroundTitle = "";
            IsFullscreen    = false;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(System.IntPtr hWnd, StringBuilder lpString, int nMaxCount);
#endif
    }
}
