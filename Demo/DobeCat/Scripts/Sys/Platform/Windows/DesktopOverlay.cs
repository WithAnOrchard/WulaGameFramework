using System.Collections;
using UnityEngine;

namespace Demo.DobeCat.Sys.Platform.Windows
{
    /// <summary>
    /// 登录完成后将窗口切换为全屏透明无边框桌面叠加层。
    /// <para>调用方式：<c>StartCoroutine(DesktopOverlay.Enter())</c>。
    /// 内部等一帧让 Unity 先完成分辨率切换，再通过 Win32 去边框 + DWM 透明 + 置顶。</para>
    /// </summary>
    public static class DesktopOverlay
    {
        /// <summary>当前是否处于窗口捕捉模式（有边框、可被 OBS 窗口捕捉识别）。</summary>
        public static bool IsWindowCaptureMode { get; private set; }

        /// <summary>
        /// 切换显示模式：
        /// <list type="bullet">
        /// <item><c>false</c>（默认）— 桌面叠加：全屏透明无边框，TOOLWINDOW，鼠标穿透受 PetClickThroughDriver 控制。</item>
        /// <item><c>true</c> — 窗口捕捉：标准有边框窗口，出现在任务栏 / OBS 窗口列表，支持 Game Capture 透明背景；穿透关闭。</item>
        /// </list>
        /// </summary>
        public static void SetWindowCaptureMode(bool capture)
        {
            if (IsWindowCaptureMode == capture) return;
            IsWindowCaptureMode = capture;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (capture) ApplyWindowCapture();
            else          ApplyOverlay(Display.main.systemWidth, Display.main.systemHeight);
#endif
            Debug.Log($"[DesktopOverlay] 显示模式切换 → {(capture ? "窗口捕捉" : "桌面叠加")}");
        }

        /// <summary>
        /// 进入桌面叠加模式：全屏 → 无边框 → DWM 真透明 → 置顶。
        /// Editor 内仅执行 Screen.SetResolution，跳过 Win32 调用。
        /// </summary>
        public static IEnumerator Enter()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // 使用 SPI_GETWORKAREA 获取真实可用区（排除任务栏），避免多显示器 DPI 偏差
            var workArea = Win32Native.GetPrimaryWorkArea();
            var w = workArea.right  - workArea.left;
            var h = workArea.bottom - workArea.top;
            if (w <= 0 || h <= 0) { w = Display.main.systemWidth; h = Display.main.systemHeight; }
            _workAreaLeft = workArea.left;
            _workAreaTop  = workArea.top;
#else
            var w = Display.main.systemWidth;
            var h = Display.main.systemHeight;
#endif
            Screen.SetResolution(w, h, false);

            yield return null; // 等 Unity 完成分辨率切换后再操作窗口

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            ApplyOverlay(w, h);
#endif
            Debug.Log($"[DesktopOverlay] 已进入桌面叠加模式 {w}×{h}");
        }

        /// <summary>切换鼠标穿透（WS_EX_TRANSPARENT）。true=穿透桌面；false=窗口接收鼠标。</summary>
        public static void SetClickThrough(bool clickThrough)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return;
            var ex = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_EXSTYLE);
            if (clickThrough)
                ex |=  Win32Native.WS_EX_TRANSPARENT;
            else
                ex &= ~Win32Native.WS_EX_TRANSPARENT;
            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE, ex);
#endif
        }

        /// <summary>
        /// 返回鼠标在 Unity 屏幕坐标系的位置。
        /// WS_EX_TRANSPARENT 开启时 Input.mousePosition 失效，此方法走 Win32 GetCursorPos 兜底。
        /// </summary>
        public static Vector2 GetGlobalCursorScreenPos()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd != System.IntPtr.Zero)
            {
                Win32Native.GetCursorPos(out var pt);
                return new Vector2(pt.x - _workAreaLeft, Screen.height - (pt.y - _workAreaTop));
            }
#endif
            return Input.mousePosition;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static System.IntPtr _hwnd = System.IntPtr.Zero;
        private static int _workAreaLeft;
        private static int _workAreaTop;

        private static System.IntPtr GetHwnd()
        {
            if (_hwnd != System.IntPtr.Zero) return _hwnd;
            _hwnd = Win32Native.FindWindow("UnityWndClass", null);
            if (_hwnd == System.IntPtr.Zero) _hwnd = Win32Native.GetActiveWindow();
            return _hwnd;
        }

        private static void ApplyOverlay(int w, int h)
        {
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero)
            {
                Debug.LogWarning("[DesktopOverlay] 找不到 Unity 窗口句柄，Win32 调用跳过");
                return;
            }

            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_STYLE,
                Win32Native.WS_POPUP | Win32Native.WS_VISIBLE);

            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE,
                Win32Native.WS_EX_LAYERED      |
                Win32Native.WS_EX_TRANSPARENT  |
                Win32Native.WS_EX_TOPMOST      |
                Win32Native.WS_EX_TOOLWINDOW);

            var margins = new Win32Native.MARGINS
            { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            Win32Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST,
                _workAreaLeft, _workAreaTop, w, h,
                Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW);
        }

        private static void ApplyWindowCapture()
        {
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return;

            // 恢复标准有边框窗口：标题栏 + 系统菜单 + 最小化按钮
            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_STYLE,
                Win32Native.WS_CAPTION      |
                Win32Native.WS_SYSMENU      |
                Win32Native.WS_MINIMIZEBOX  |
                Win32Native.WS_VISIBLE);

            // 去掉 TOOLWINDOW（重新出现在 Alt+Tab / OBS 窗口列表），去掉 TRANSPARENT（窗口接收鼠标）
            // 保留 LAYERED（DWM 透明合成，OBS Game Capture 可透明捕捉）+ TOPMOST（保持置顶）
            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE,
                Win32Native.WS_EX_LAYERED  |
                Win32Native.WS_EX_TOPMOST);

            // DWM 透明：保持，OBS Game Capture + Allow Transparency 时背景仍可透出
            var margins = new Win32Native.MARGINS
            { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            Win32Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            // 刷新样式（保持原位置 / 尺寸不变）
            Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE |
                Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW);
        }
#endif
    }
}
