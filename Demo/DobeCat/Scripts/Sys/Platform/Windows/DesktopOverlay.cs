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
            if (capture)
                ApplyWindowCapture();
            else
            {
                // 切回叠加：使用 Enter() 缓存的工作区尺寸，而非 Display.main（含任务栏）
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
        /// Editor 内仅执行 Screen.SetResolution，跳过 Win32 调用。
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

            // 步骤 1：立即隐藏窗口 + 预启用 DWM 透明
            //   - 隐藏确保任何内容都不会在正确样式应用前被看见
            //   - DWM 预启用确保 resize 后第一帧已处于透明合成状态
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

            // 步骤 2：仅在分辨率不匹配时才 resize（稳定状态跳过，零 SwapChain 重建）
            if (Screen.width != w || Screen.height != h)
                Screen.SetResolution(w, h, false);

            // 步骤 3：等多帧让 Unity 完成异步 SetResolution（windowed resize 是异步的）
            //   单一 yield null 不够 —— Unity 可能在我们 ApplyOverlay 之后才完成 resize
            //   并把 WS_POPUP 重置回 WS_OVERLAPPEDWINDOW（标题栏、任务栏图标全部回来）
            yield return null;
            yield return null;
            yield return null;

            // 步骤 4：应用完整叠加样式并显示
            ApplyOverlay(w, h);
            SetClickThrough(false); // 登录阶段禁用穿透，RunAfterLogin 中重新启用

            Debug.Log($"[DesktopOverlay] 已进入桌面叠加模式 {w}×{h}");

            // 步骤 5：永久看门狗 —— Unity 在 ExclusiveFullscreen fallback / 窗口聚焦切换 等场景下
            //   可能反复重置窗口样式（标题栏 / 任务栏图标重新出现）。每 500ms 检查一次，
            //   样式被重置就立即重应用。协程随 DobeCatGameManager 销毁自动结束。
            while (true)
            {
                yield return new WaitForSeconds(0.5f);
                // 窗口捕捉模式下样式与 overlay 不同，跳过检查
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

        /// <summary>将窗口居中于主显示器工作区（用于登录窗口定位）。</summary>
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

        /// <summary>
        /// 将窗口带到前台并获取键盘焦点，使 TMP_InputField 等需要键盘的 UI 可以正常输入。
        /// 会暂时去除 WS_EX_TRANSPARENT，使窗口能被激活；<see cref="PetClickThroughDriver"/>
        /// 仍负责在没有可交互 UI 时自动恢复穿透状态。
        /// </summary>
        public static void BringToForeground()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return;
            // 临时去除 TRANSPARENT，让窗口可被激活
            var ex = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_EXSTYLE);
            ex &= ~Win32Native.WS_EX_TRANSPARENT;
            Win32Native.SetWindowLong(hwnd, Win32Native.GWL_EXSTYLE, ex);
            // 将窗口置前并激活 → 键盘事件路由到此窗口
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

        /// <summary>
        /// 检查窗口是否仍处于完整覆盖层状态：
        /// 样式（WS_POPUP + WS_EX_TOOLWINDOW + WS_EX_TOPMOST）+ 矩形（覆盖工作区，位置正确）。
        /// </summary>
        private static bool IsOverlayApplied(int expectedW, int expectedH)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var hwnd = GetHwnd();
            if (hwnd == System.IntPtr.Zero) return true; // 找不到句柄不重应用

            var style = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_STYLE);
            var ex    = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_EXSTYLE);
            bool styleOk = (style & Win32Native.WS_POPUP) != 0
                        && (ex & Win32Native.WS_EX_TOOLWINDOW) != 0
                        && (ex & Win32Native.WS_EX_TOPMOST)    != 0;
            if (!styleOk) return false;

            // 矩形检查：宽高与工作区一致、位置在工作区左上角（允许 ±2px 误差以兼容 DPI 缩放）
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

            // 关键：WS_EX_TOOLWINDOW 在已可见窗口上设置不会立即从任务栏移除，
            // 必须 SW_HIDE → SWP_SHOWWINDOW 才能触发样式真正生效（MSDN 明确要求）。
            // 同时确保窗口不被 Windows 当成普通应用（失焦时不被发到后面）。
            Win32Native.ShowWindow(hwnd, Win32Native.SW_HIDE);
            Win32Native.SetWindowPos(hwnd, Win32Native.HWND_TOPMOST,
                _workAreaLeft, _workAreaTop, w, h,
                Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW | Win32Native.SWP_NOACTIVATE);
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
