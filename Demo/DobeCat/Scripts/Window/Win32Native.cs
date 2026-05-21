#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace Demo.DobeCat.Window
{
    /// <summary>
    /// Win32 P/Invoke 集合：仅在 Windows 独立运行时（非 Editor）参与编译。
    /// <para>桌宠所需窗口能力：无边框 / 分层 / 鼠标穿透 / 置顶 / 移动。</para>
    /// </summary>
    internal static class Win32Native
    {
        // ── window styles ──
        public const int GWL_STYLE   = -16;
        public const int GWL_EXSTYLE = -20;

        public const uint WS_POPUP        = 0x80000000;
        public const uint WS_VISIBLE      = 0x10000000;
        public const uint WS_CAPTION      = 0x00C00000;
        public const uint WS_THICKFRAME   = 0x00040000;
        public const uint WS_SYSMENU      = 0x00080000;
        public const uint WS_MINIMIZEBOX  = 0x00020000;
        public const uint WS_MAXIMIZEBOX  = 0x00010000;

        public const uint WS_EX_LAYERED     = 0x00080000;
        public const uint WS_EX_TRANSPARENT = 0x00000020;
        public const uint WS_EX_TOPMOST     = 0x00000008;
        public const uint WS_EX_TOOLWINDOW  = 0x00000080;

        public const uint LWA_COLORKEY = 0x1;
        public const uint LWA_ALPHA    = 0x2;

        // ── SetWindowPos flags ──
        public static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const uint SWP_NOSIZE       = 0x0001;
        public const uint SWP_NOMOVE       = 0x0002;
        public const uint SWP_NOZORDER     = 0x0004;
        public const uint SWP_NOACTIVATE   = 0x0010;
        public const uint SWP_SHOWWINDOW   = 0x0040;
        public const uint SWP_FRAMECHANGED = 0x0020;

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x, y;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        public static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_HIDE = 0;
        public const int SW_SHOWNA = 8;

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("Dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        /// <summary>全局键盘状态查询（不受窗口焦点 / click-through 影响）。</summary>
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        // virtual-key codes
        public const int VK_ESCAPE  = 0x1B;
        public const int VK_CONTROL = 0x11;
        public const int VK_SHIFT   = 0x10;
        public const int VK_Q       = 0x51;
        public const int VK_W       = 0x57;
        public const int VK_A       = 0x41;
        public const int VK_S       = 0x53;
        public const int VK_D       = 0x44;
    }
}
#endif
