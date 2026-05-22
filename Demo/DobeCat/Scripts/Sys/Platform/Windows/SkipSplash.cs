#if !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demo.DobeCat.Sys.Platform.Windows
{
    /// <summary>
    /// 启动期"无感"处理：
    /// <list type="number">
    /// <item><see cref="RuntimeInitializeLoadType.BeforeSplashScreen"/> 时立即 <see cref="SplashScreen.Stop"/> 跳过 Logo。</item>
    /// <item>同时把进程主窗口 <c>HWND</c> 隐藏，避免默认黑底窗口闪一帧。</item>
    /// <item>窗口由 <c>DesktopWindow</c> 应用完透明 / Layered 属性后再调用 <see cref="ShowMainWindow"/> 显示。</item>
    /// </list>
    /// </summary>
    public static class SkipSplash
    {
        public static IntPtr HiddenHwnd { get; private set; }

        /// <summary>启动期窗口预设尺寸（与 <c>LoginScreen</c> 保持一致；登录后 <c>DesktopWindow</c> 会再次撑满工作区）。</summary>
        public const int LaunchWidth  = 460;
        public const int LaunchHeight = 360;

#if UNITY_STANDALONE_WIN
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_HIDE = 0;
        private const int SW_SHOWNA = 8;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void BeforeSplashScreen()
        {
#if UNITY_STANDALONE_WIN
            // 1) 找到当前进程主窗口并立即隐藏（此时 Unity 已创建但尚未渲染 splash）
            try
            {
                var hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    HiddenHwnd = hwnd;
                    ShowWindow(hwnd, SW_HIDE);
                    // 趁窗口还没显示，把它的尺寸/样式调成登录界面的样子，
                    // 否则 splash 结束后 Unity 会以 PlayerSettings 默认分辨率短暂闪一帧黑色全屏。
                    PrelayoutLoginWindow(hwnd);
                }
            }
            catch { /* 进程信息查询失败也无所谓，continue */ }
#endif

#if UNITY_WEBGL
            Application.focusChanged += OnFocusChanged;
#else
            // 2) 后台线程异步停止 splash（阻止其渲染流程）
            System.Threading.Tasks.Task.Run(AsyncSkip);
#endif
        }

#if UNITY_WEBGL
        private static void OnFocusChanged(bool focus)
        {
            Application.focusChanged -= OnFocusChanged;
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
        }
#else
        private static void AsyncSkip()
        {
            SplashScreen.Stop(SplashScreen.StopBehavior.StopImmediate);
        }
#endif

#if UNITY_STANDALONE_WIN
        /// <summary>桌宠窗口属性已应用完毕后调用，显示主窗口。</summary>
        public static void ShowMainWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            ShowWindow(hwnd, SW_SHOWNA);
        }

        // ── PrelayoutLoginWindow 用到的 Win32 ───────────────────
        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]  private static extern uint GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]  private static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int  GWL_STYLE        = -16;
        private const uint WS_POPUP         = 0x80000000;
        private const uint WS_CAPTION       = 0x00C00000;
        private const uint WS_THICKFRAME    = 0x00040000;
        private const uint WS_SYSMENU       = 0x00080000;
        private const uint WS_MINIMIZEBOX   = 0x00020000;
        private const uint WS_MAXIMIZEBOX   = 0x00010000;
        private const uint SWP_NOZORDER     = 0x0004;
        private const uint SWP_NOACTIVATE   = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int  SM_CXSCREEN      = 0;
        private const int  SM_CYSCREEN      = 1;

        /// <summary>窗口仍隐藏时，把它的尺寸 / 样式预先调成登录界面的样子，避免 splash 结束后闪烁。</summary>
        private static void PrelayoutLoginWindow(IntPtr hwnd)
        {
            try
            {
                var style = GetWindowLong(hwnd, GWL_STYLE);
                style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
                style |= WS_POPUP;
                SetWindowLong(hwnd, GWL_STYLE, style);

                var sw = GetSystemMetrics(SM_CXSCREEN);
                var sh = GetSystemMetrics(SM_CYSCREEN);
                var x = System.Math.Max(0, (sw - LaunchWidth)  / 2);
                var y = System.Math.Max(0, (sh - LaunchHeight) / 2);
                // 不带 SWP_SHOWWINDOW —— 仅调整尺寸 / 样式，让 LoginScreen 决定显示时机。
                SetWindowPos(hwnd, IntPtr.Zero, x, y, LaunchWidth, LaunchHeight,
                    SWP_FRAMECHANGED | SWP_NOZORDER | SWP_NOACTIVATE);
            }
            catch { /* 兜底：失败也不阻断启动，LoginScreen.Awake 还会再尝试一次 */ }
        }
#endif
    }
}
#endif
