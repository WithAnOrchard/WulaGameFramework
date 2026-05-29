using System.Collections;
using UnityEngine;

namespace EssSystem.Core.Platform.Windows
{
    /// <summary>
    /// 将 Unity 应用窗口切换为全屏透明无边框桌面叠加层。
    /// <para>调用方式：<c>StartCoroutine(DesktopOverlay.Enter())</c>。</para>
    /// <para>Editor 内仅执行 Screen.SetResolution，跳过 Win32 调用。</para>
    /// </summary>
    public static class DesktopOverlay
    {
        /// <summary>当前是否处于窗口捕捉模式（有边框、可被 OBS 窗口捕捉识别）。</summary>
        public static bool IsWindowCaptureMode { get; private set; }

        /// <summary>
        /// 切换显示模式：
        /// <list type="bullet">
        /// <item><c>false</c>（默认）— 桌面叠加：全屏透明无边框，TOOLWINDOW，鼠标穿透由调用方控制。</item>
        /// <item><c>true</c> — 窗口捕捉：标准有边框窗口，出现在任务栏 / OBS 窗口列表。</item>
        /// </list>
        /// </summary>
        public static void SetWindowCaptureMode(bool capture)
        {
            if (IsWindowCaptureMode == capture) return;
            IsWindowCaptureMode = capture;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (capture)
                ApplyWindowCapture();
            else
            {
                var wa = Win32Native.GetPrimaryWorkArea();
                var w = wa.right - wa.left;
                var h = wa.bottom - wa.top;
                if (w <= 0 || h <= 0) { w = Display.main.systemWidth; h = Display.main.systemHeight; }
                ApplyOverlay(w, h);
            }
#endif
            Debug.Log($"[DesktopOverlay] 显示模式切换 → {(capture ? "窗口捕捉" : "桌面叠加")}");
        }

        /// <summary>
        /// 进入桌面叠加模式：全屏 → 无边框 → DWM 真透明 → 置顶。
        /// </summary>
        public static IEnumerator Enter()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var workArea = Win32Native.GetPrimaryWorkArea();
            var w = workArea.right  - workArea.left;
            var h = workArea.bottom - workArea.top;
            if (w <= 0 || h <= 0) { w = Display.main.systemWidth; h = Display.main.systemHeight; }
            _workAreaLeft = workArea.left;
            _workAreaTop  = workArea.top;

            var hwnd = GetHwnd();
            if (hwnd != System.IntPtr.Zero)
            {
                var ex = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_EXSTYLE);
                Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE, ex | Win32Native.WS_EX_LAYERED);
                var m = new Win32Native.MARGINS
                    { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                Win32Native.DwmExtendFrameIntoClientArea(hwnd, ref m);
                Win32Native.ShowWindow(hwnd, Win32Native.SW_HIDE);
            }

            if (Screen.width != w || Screen.height != h)
                Screen.SetResolution(w, h, false);

            yield return null;
            yield return null;
            yield return null;

            ApplyOverlay(w, h);
            SetClickThrough(false);

            Debug.Log($"[DesktopOverlay] 已进入桌面叠加模式 {w}×{h}");

            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                if (IsWindowCaptureMode) continue;
                if (!IsOverlayApplied(w, h))
                {
                    Debug.LogWarning("[DesktopOverlay] 样式/矩形被重置，重新应用覆盖层");
                    ApplyOverlay(w, h);
                }
            }
#else
            var w = Display.main.systemWidth;
            var h = Display.main.systemHeight;
            yield return null;
            Debug.Log($"[DesktopOverlay] 已进入桌面叠加模式 {w}×{h}（非 Windows，仅记录尺寸）");
#endif
        }

        /// <summary>将窗口居中于主显示器工作区。</summary>
        public static void CenterWindow(int w, int h)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return;
            var wa = Win32Native.GetPrimaryWorkArea();
            var x = wa.left + (wa.right  - wa.left - w) / 2;
            var y = wa.top  + (wa.bottom - wa.top  - h) / 2;
            Win32Native.SetWindowPos(hwnd, System.IntPtr.Zero, x, y, 0, 0,
                Win32Native.SWP_NOSIZE | Win32Native.SWP_NOZORDER | Win32Native.SWP_NOACTIVATE);
#endif
        }

        /// <summary>将窗口带到前台并获取键盘焦点。</summary>
        public static void BringToForeground()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return;
            var ex = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_EXSTYLE);
            ex &= ~Win32Native.WS_EX_TRANSPARENT;
            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE, ex);
            Win32Native.SetForegroundWindow(hwnd);
#endif
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

        private static bool IsOverlayApplied(int expectedW, int expectedH)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return true;

            var style = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_STYLE);
            var ex    = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_EXSTYLE);
            bool styleOk = (style & Win32Native.WS_POPUP) != 0
                        && (ex & Win32Native.WS_EX_TOOLWINDOW) != 0
                        && (ex & Win32Native.WS_EX_TOPMOST)    != 0;
            if (!styleOk) return false;

            if (Win32Native.GetWindowRect(hwnd, out var rect))
            {
                int actualW = rect.right - rect.left;
                int actualH = rect.bottom - rect.top;
                if (System.Math.Abs(actualW - expectedW) > 2 ||
                    System.Math.Abs(actualH - expectedH) > 2 ||
                    System.Math.Abs(rect.left - _workAreaLeft) > 2 ||
                    System.Math.Abs(rect.top  - _workAreaTop)  > 2)
                {
                    return false;
                }
            }
            return true;
#else
            return true;
#endif
        }

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

            Win32Native.ShowWindow(hwnd, Win32Native.SW_HIDE);
            Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST,
                _workAreaLeft, _workAreaTop, w, h,
                Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW | Win32Native.SWP_NOACTIVATE);
        }

        private static void ApplyWindowCapture()
        {
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return;

            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_STYLE,
                Win32Native.WS_CAPTION      |
                Win32Native.WS_SYSMENU      |
                Win32Native.WS_MINIMIZEBOX  |
                Win32Native.WS_VISIBLE);

            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE,
                Win32Native.WS_EX_LAYERED  |
                Win32Native.WS_EX_TOPMOST);

            var margins = new Win32Native.MARGINS
            { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            Win32Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);

            Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE |
                Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW);
        }
#endif
    }
}
