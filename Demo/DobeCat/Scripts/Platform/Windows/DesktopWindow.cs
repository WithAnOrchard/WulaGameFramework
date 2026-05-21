using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
#endif

namespace Demo.DobeCat.Platform.Windows
{
    /// <summary>
    /// 桌面宠物窗口能力封装。
    /// <list type="bullet">
    /// <item>启动时：去边框 / 分层 / 置顶 / 透明背景。</item>
    /// <item>运行时：动态切换"鼠标穿透"，整窗口内除桌宠像素之外的区域不阻挡桌面点击。</item>
    /// <item>Editor 内为空操作（仅打 log），便于在 Editor 直接调试逻辑。</item>
    /// </list>
    /// <para>窗口策略采用 §M1.6：全屏覆盖 + 桌宠在窗口内移动，无需移动 HWND。</para>
    /// </summary>
    // 字段在非 Standalone Windows 编译时被条件分支排除使用，统一抑制 CS0414
#pragma warning disable 0414
    public class DesktopWindow : MonoBehaviour
    {
        [Tooltip("启动时把窗口铺满主屏 WorkArea（任务栏外的可见桌面区域）。")]
        [SerializeField] private bool _fullscreenWorkArea = true;

        [Tooltip("是否常驻最前。关闭后窗口会沉到桌面层。")]
        [SerializeField] private bool _topmost = true;

        [Tooltip("Color Key 颜色：相机背景与 shader 后处理 ColorKey 都会用此值。\n" +
                 "默认纯绿 #00FF00（避免猫咪 / 桌面常见色误伤）。")]
        [SerializeField] private Color _colorKey = new Color(0f, 1f, 0f, 1f);

        [Tooltip("ColorKey 容差（shader 中相邻像素抗锯齿边缘吃掉用）。")]
        [Range(0f, 0.5f)] [SerializeField] private float _colorKeyMargin = 0.01f;

        [Tooltip("启动时自动给主相机挂 TransparentRenderBlit 后处理（Built-in 管线）。")]
        [SerializeField] private bool _autoAttachBlit = true;

        public static DesktopWindow Instance { get; private set; }

        /// <summary>当前是否处于"鼠标穿透"状态。</summary>
        public bool ClickThrough { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IntPtr _hwnd;
        private uint _baseExStyle;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            ApplyTransparentClearColor();
        }

        private void Start()
        {
            ApplyDesktopWindow();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ──────────────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────────────

        /// <summary>切换鼠标穿透状态。<paramref name="through"/>=true 时点击穿过窗口到下层桌面。</summary>
        public void SetClickThrough(bool through)
        {
            if (ClickThrough == through) return;
            ClickThrough = through;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_hwnd == IntPtr.Zero) return;
            var ex = Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE);
            if (through) ex |= Win32Native.WS_EX_TRANSPARENT;
            else         ex &= ~Win32Native.WS_EX_TRANSPARENT;
            Win32Native.SetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE, ex);
#endif
        }

        /// <summary>全局热键查询：当前帧是否按下 Esc（不受 click-through / 焦点影响）。</summary>
        public bool IsGlobalEscapePressed()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            return (Win32Native.GetAsyncKeyState(Win32Native.VK_ESCAPE) & 0x8000) != 0;
#else
            return Input.GetKey(KeyCode.Escape);
#endif
        }

        /// <summary>全局热键查询：是否按下 Ctrl+Shift+Q（推荐退出热键）。</summary>
        public bool IsGlobalQuitHotkeyPressed()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var ctrl  = (Win32Native.GetAsyncKeyState(Win32Native.VK_CONTROL) & 0x8000) != 0;
            var shift = (Win32Native.GetAsyncKeyState(Win32Native.VK_SHIFT)   & 0x8000) != 0;
            var q     = (Win32Native.GetAsyncKeyState(Win32Native.VK_Q)       & 0x8000) != 0;
            return ctrl && shift && q;
#else
            return Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.Q);
#endif
        }

        /// <summary>全局 WASD 输入轴：x=D-A, y=W-S。click-through 失去焦点时也能读到。</summary>
        public Vector2 GetGlobalWasdAxis()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var w = (Win32Native.GetAsyncKeyState(Win32Native.VK_W) & 0x8000) != 0;
            var a = (Win32Native.GetAsyncKeyState(Win32Native.VK_A) & 0x8000) != 0;
            var s = (Win32Native.GetAsyncKeyState(Win32Native.VK_S) & 0x8000) != 0;
            var d = (Win32Native.GetAsyncKeyState(Win32Native.VK_D) & 0x8000) != 0;
#else
            var w = Input.GetKey(KeyCode.W);
            var a = Input.GetKey(KeyCode.A);
            var s = Input.GetKey(KeyCode.S);
            var d = Input.GetKey(KeyCode.D);
#endif
            return new Vector2((d ? 1f : 0f) - (a ? 1f : 0f), (w ? 1f : 0f) - (s ? 1f : 0f));
        }

        /// <summary>读取当前鼠标在屏幕坐标系的位置（无视 Unity 焦点；穿透时也能读取）。</summary>
        public Vector2 GetGlobalCursorScreenPos()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (Win32Native.GetCursorPos(out var p))
            {
                if (_hwnd != IntPtr.Zero && Win32Native.GetWindowRect(_hwnd, out var rect))
                {
                    // 转为窗口客户区像素坐标 → Unity 屏幕坐标（左下原点）
                    var localX = p.x - rect.left;
                    var localYTop = p.y - rect.top;
                    var height = rect.bottom - rect.top;
                    return new Vector2(localX, height - localYTop);
                }
                return new Vector2(p.x, p.y);
            }
#endif
            return Input.mousePosition;
        }

        // ──────────────────────────────────────────────────────
        //  Internal
        // ──────────────────────────────────────────────────────

        private void ApplyTransparentClearColor()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(_colorKey.r, _colorKey.g, _colorKey.b, 1f);

            if (_autoAttachBlit)
            {
                var blit = cam.GetComponent<TransparentRenderBlit>();
                if (blit == null) blit = cam.gameObject.AddComponent<TransparentRenderBlit>();
                blit.ColorKey = _colorKey;
                blit.Margin = _colorKeyMargin;
            }
        }

        private void ApplyDesktopWindow()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _hwnd = Win32Native.GetActiveWindow();
            if (_hwnd == IntPtr.Zero) _hwnd = Win32Native.GetForegroundWindow();
            if (_hwnd == IntPtr.Zero)
            {
                Debug.LogWarning("[DesktopWindow] 无法获取 HWND");
                return;
            }

            // 1) 主样式：去标题栏 / 边框 / 系统菜单
            var style = Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_STYLE);
            style &= ~(Win32Native.WS_CAPTION
                       | Win32Native.WS_THICKFRAME
                       | Win32Native.WS_SYSMENU
                       | Win32Native.WS_MINIMIZEBOX
                       | Win32Native.WS_MAXIMIZEBOX);
            style |= Win32Native.WS_POPUP;
            Win32Native.SetWindowLong(_hwnd, Win32Native.GWL_STYLE, style);

            // 2) 扩展样式：layered（透明）+ topmost + toolwindow（不出现在 alt-tab/任务栏）
            var ex = Win32Native.GetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE);
            ex |= Win32Native.WS_EX_LAYERED | Win32Native.WS_EX_TOOLWINDOW;
            if (_topmost) ex |= Win32Native.WS_EX_TOPMOST;
            Win32Native.SetWindowLong(_hwnd, Win32Native.GWL_EXSTYLE, ex);
            _baseExStyle = ex;

            // 3) 让 DWM 把整个客户区当作"扩展玻璃帧"，按 backbuffer 的 per-pixel alpha 合成。
            //    backbuffer alpha 由 TransparentRenderBlit 后处理 shader 写入（颜色键的像素 → α=0）。
            //    不调 SetLayeredWindowAttributes —— 那会强制全窗口统一 alpha 覆盖 DWM 路径。
            var margins = new Win32Native.MARGINS
            {
                cxLeftWidth = -1, cxRightWidth = -1,
                cyTopHeight = -1, cyBottomHeight = -1,
            };
            Win32Native.DwmExtendFrameIntoClientArea(_hwnd, ref margins);

            // 4) 占满主屏 WorkArea
            if (_fullscreenWorkArea)
            {
                var w = Display.main.systemWidth;
                var h = Display.main.systemHeight;
                Win32Native.SetWindowPos(_hwnd,
                    _topmost ? Win32Native.HWND_TOPMOST : Win32Native.HWND_NOTOPMOST,
                    0, 0, w, h,
                    Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW);
                Screen.SetResolution(w, h, FullScreenMode.Windowed, Screen.currentResolution.refreshRate);
            }
            else if (_topmost)
            {
                Win32Native.SetWindowPos(_hwnd, Win32Native.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32Native.SWP_NOMOVE | Win32Native.SWP_NOSIZE | Win32Native.SWP_FRAMECHANGED);
            }

            // 5) 透明属性已应用，把启动期间被 SkipSplash 隐藏的窗口显示出来
            SkipSplash.ShowMainWindow(_hwnd);

            Debug.Log("[DesktopWindow] 桌宠窗口模式已激活（透明 / 无边框 / 置顶 / Toolwindow）");
#else
            Debug.Log("[DesktopWindow] 非 Standalone Windows，跳过窗口设置（Editor 调试模式）");
#endif
        }
    }
#pragma warning restore 0414
}
