#if !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demo.DobeCat.Platform.Windows
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
#endif
    }
}
#endif
