#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace EssSystem.Core.Platform.Windows
{
    /// <summary>
    /// Windows 系统托盘集成器（Win32 原生实现）。
    /// <para>独立线程消息泵、递归子菜单、从 .exe 自动提取图标。</para>
    /// </summary>
    public class SystemTray : IDisposable
    {
        private const string WindowClassName = "DobeCat_SystemTray_Wnd";
        private const uint TrayIconId = 1;

        private Thread _trayThread;
        private IntPtr _hWnd;
        private Queue<Action> _mainThreadQueue = new Queue<Action>();
        private List<MenuItemDef> _items = new List<MenuItemDef>();
        private bool _disposed;
        private string _tooltip = "Application";

        public string Tooltip
        {
            get => _tooltip;
            set => _tooltip = value ?? "Application";
        }

        public event Action OnDoubleClick;

        public SystemTray()
        {
            _trayThread = new Thread(ThreadProc) { IsBackground = true };
            _trayThread.Start();
        }

        public void SetItems(List<MenuItemDef> items)
        {
            lock (_items)
            {
                _items.Clear();
                if (items != null) _items.AddRange(items);
            }
        }

        public void RequestShowMenu()
        {
            if (_hWnd != IntPtr.Zero)
                TrayNative.PostMessage(_hWnd, TrayNative.WM_USER_SHOW_MENU, IntPtr.Zero, IntPtr.Zero);
        }

        public void Start()
        {
            // 托盘已在 ThreadProc 中启动
        }

        public void PumpMainThread()
        {
            while (_mainThreadQueue.Count > 0)
            {
                var act = _mainThreadQueue.Dequeue();
                try { act?.Invoke(); }
                catch (Exception e) { Debug.LogError("[SystemTray] 主线程回调异常：" + e); }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_hWnd != IntPtr.Zero)
                TrayNative.PostMessage(_hWnd, TrayNative.WM_DESTROY, IntPtr.Zero, IntPtr.Zero);

            if (_trayThread != null && _trayThread.IsAlive)
                _trayThread.Join(2000);
        }

        private void ThreadProc()
        {
            try
            {
                // 1) 注册窗口类
                var hInst = TrayNative.GetModuleHandle(null);
                var wndClass = new TrayNative.WNDCLASS
                {
                    lpszClassName = WindowClassName,
                    lpfnWndProc = WndProc,
                };
                if (TrayNative.RegisterClass(ref wndClass) == 0)
                {
                    Debug.LogError("[SystemTray] RegisterClass 失败");
                    return;
                }

                // 2) 创建隐藏窗口
                _hWnd = TrayNative.CreateWindowEx(0, WindowClassName, "DobeCat Tray",
                    0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
                if (_hWnd == IntPtr.Zero)
                {
                    Debug.LogError("[SystemTray] CreateWindowEx 失败");
                    return;
                }

                // 3) 添加托盘图标
                var hIcon = LoadExeIcon();
                var nid = new TrayNative.NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(TrayNative.NOTIFYICONDATA)),
                    hWnd = _hWnd,
                    uID = TrayIconId,
                    uFlags = TrayNative.NIF_ICON | TrayNative.NIF_MESSAGE,
                    uCallbackMessage = TrayNative.WM_TRAYICON,
                    hIcon = hIcon,
                    szInfoTitle = string.Empty,
                };
                if (!TrayNative.Shell_NotifyIcon(TrayNative.NIM_ADD, ref nid))
                {
                    Debug.LogError("[SystemTray] Shell_NotifyIcon NIM_ADD 失败");
                    return;
                }

                // 4) 消息循环
                while (TrayNative.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
                {
                    TrayNative.TranslateMessage(ref msg);
                    TrayNative.DispatchMessage(ref msg);
                }

                // 5) 清理
                var nidDel = new TrayNative.NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(TrayNative.NOTIFYICONDATA)),
                    hWnd = _hWnd,
                    uID = TrayIconId,
                };
                TrayNative.Shell_NotifyIcon(TrayNative.NIM_DELETE, ref nidDel);
                if (_hWnd != IntPtr.Zero) TrayNative.DestroyWindow(_hWnd);
                TrayNative.UnregisterClass(WindowClassName, hInst);
            }
            catch (Exception e)
            {
                Debug.LogError("[SystemTray] ThreadProc 异常：" + e);
            }
        }

        /// <summary>从正在运行的 .exe 提取第一个图标（即 Unity Player Settings 里设置的图标）。</summary>
        private static IntPtr LoadExeIcon()
        {
            try
            {
                var exePath = GetCurrentExecutablePath();
                if (!string.IsNullOrEmpty(exePath))
                {
                    var small = new IntPtr[1];
                    var large = new IntPtr[1];
                    uint cnt = TrayNative.ExtractIconEx(exePath, 0, large, small, 1);
                    if (cnt > 0)
                    {
                        // 优先用小图标（16px，托盘标准尺寸），大图标不为零则释放
                        if (large[0] != IntPtr.Zero && large[0] != small[0])
                            TrayNative.DestroyIcon(large[0]);
                        if (small[0] != IntPtr.Zero) return small[0];
                        if (large[0] != IntPtr.Zero) return large[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[SystemTray] LoadExeIcon 失败，回退默认图标：" + ex.Message);
            }
            return TrayNative.LoadIcon(IntPtr.Zero, TrayNative.IDI_APPLICATION);
        }

        private static string GetCurrentExecutablePath()
        {
            var buffer = new StringBuilder(1024);
            var length = TrayNative.GetModuleFileName(IntPtr.Zero, buffer, (uint)buffer.Capacity);
            return length > 0 ? buffer.ToString() : null;
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (msg == TrayNative.WM_TRAYICON)
                {
                    var ev = (uint)lParam.ToInt32();
                    if (ev == TrayNative.WM_RBUTTONUP)
                    {
                        ShowContextMenu();
                        return IntPtr.Zero;
                    }
                    if (ev == TrayNative.WM_LBUTTONDBLCLK)
                    {
                        if (OnDoubleClick != null)
                            _mainThreadQueue.Enqueue(OnDoubleClick);
                        return IntPtr.Zero;
                    }
                }
                else if (msg == TrayNative.WM_USER_SHOW_MENU)
                {
                    // 来自外部线程的弹菜单请求（如桌宠右键）→ 在 tray 线程上执行 TrackPopupMenu
                    ShowContextMenu();
                    return IntPtr.Zero;
                }
                else if (msg == TrayNative.WM_DESTROY)
                {
                    TrayNative.PostQuitMessage(0);
                    return IntPtr.Zero;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[SystemTray] WndProc 异常：" + e);
            }
            return TrayNative.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void ShowContextMenu()
        {
            var hMenu = TrayNative.CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;
            try
            {
                var idToAction = new List<Action>(); // index 0 占位（菜单 ID 从 1 开始）
                idToAction.Add(null);
                MenuItemDef[] snapshot;
                lock (_items) snapshot = _items.ToArray();
                BuildMenuTree(hMenu, snapshot, idToAction);

                // TrackPopupMenu 要求 SetForegroundWindow，否则菜单不会自动消失
                TrayNative.SetForegroundWindow(_hWnd);
                TrayNative.GetCursorPos(out var pt);
                var cmd = TrayNative.TrackPopupMenu(hMenu,
                    TrayNative.TPM_RIGHTBUTTON | TrayNative.TPM_RETURNCMD,
                    pt.x, pt.y, 0, _hWnd, IntPtr.Zero);

                if (cmd > 0 && cmd < idToAction.Count)
                {
                    var act = idToAction[cmd];
                    if (act != null) _mainThreadQueue.Enqueue(act);
                }

                // PostMessage WM_NULL 推开 TrackPopupMenu 的后续状态（MS 推荐）
                TrayNative.PostMessage(_hWnd, 0x0000, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                TrayNative.DestroyMenu(hMenu); // 递归销毁整棵菜单树
            }
        }

        /// <summary>
        /// 递归构建 Win32 菜单树。SubItems 非空 → 创建 popup 子菜单并用 MF_POPUP 附加；
        /// 叶节点分配递增 ID，TrackPopupMenu(TPM_RETURNCMD) 最终只返回叶节点 ID。
        /// </summary>
        private static void BuildMenuTree(IntPtr hMenu, MenuItemDef[] items, List<Action> idToAction)
        {
            foreach (var def in items)
            {
                if (def.SubItems != null)
                {
                    // 子菜单：创建 popup，递归填充，然后用 MF_POPUP 追加到父菜单
                    var hSub = TrayNative.CreatePopupMenu();
                    BuildMenuTree(hSub, def.SubItems, idToAction);
                    TrayNative.AppendMenu(hMenu,
                        TrayNative.MF_POPUP | TrayNative.MF_STRING,
                        hSub, def.Text);
                }
                else if (string.IsNullOrEmpty(def.Text))
                {
                    TrayNative.AppendMenu(hMenu, TrayNative.MF_SEPARATOR, IntPtr.Zero, null);
                }
                else
                {
                    idToAction.Add(def.OnClick);
                    uint flags = TrayNative.MF_STRING;
                    if (!def.Enabled) flags |= TrayNative.MF_GRAYED;
                    TrayNative.AppendMenu(hMenu, flags, new IntPtr(idToAction.Count - 1), def.Text);
                }
            }
        }

        public struct MenuItemDef
        {
            /// <summary>显示文本；<c>null</c>/空 = 分隔符。</summary>
            public string Text;
            /// <summary>点击回调（在 Unity 主线程派发）。分隔符或灰色项可为 <c>null</c>。</summary>
            public Action OnClick;
            /// <summary><c>false</c> = 灰色不可点。默认（new 出来时）是 <c>false</c>，请用 <see cref="Item"/> 工厂创建。</summary>
            public bool Enabled;
            /// <summary>非 null = 此项为子菜单头，鼠标悬停自动展开。子项由 Win32 原生悬停行为驱动。</summary>
            public MenuItemDef[] SubItems;

            public static MenuItemDef Item(string text, Action onClick, bool enabled = true)
                => new MenuItemDef { Text = text, OnClick = onClick, Enabled = enabled };
            public static MenuItemDef Separator()
                => new MenuItemDef { Text = null, OnClick = null, Enabled = false };
            public static MenuItemDef Disabled(string text)
                => new MenuItemDef { Text = text, OnClick = null, Enabled = false };
            /// <summary>创建带子菜单的父项。鼠标悬停时自动展开二级菜单（Win32 原生行为）。</summary>
            public static MenuItemDef Sub(string text, MenuItemDef[] subItems)
                => new MenuItemDef { Text = text, SubItems = subItems, Enabled = true };
        }
    }
}
#endif
