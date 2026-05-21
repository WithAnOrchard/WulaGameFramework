#if UNITY_STANDALONE_WIN
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Demo.DobeCat.Sys.Platform.Windows
{
    /// <summary>
    /// Windows 系统托盘图标 —— 纯 Win32 P/Invoke 实现，不依赖 System.Windows.Forms。
    /// <list type="bullet">
    /// <item>独立线程跑 Win32 消息循环（<see cref="TrayNative.GetMessage"/>）。</item>
    /// <item>右键 / 双击 → 主线程 action 队列，Update 中 pump。</item>
    /// <item>菜单使用 <see cref="TrayNative.CreatePopupMenu"/> + <see cref="TrayNative.TrackPopupMenu"/>。</item>
    /// </list>
    /// </summary>
    public class SystemTray : IDisposable
    {
        private const string WindowClassName = "DobeCatTrayWnd";
        private const uint TrayIconId = 1;
        // 菜单项 ID 从 1 开始（0 在 TPM_RETURNCMD 模式下表示 "没有选中"）
        private const int FirstMenuId = 1;

        public string Tooltip = "DobeCat";

        public event Action OnDoubleClick;

        private readonly List<MenuItemDef> _items = new List<MenuItemDef>();
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private Thread _thread;
        private IntPtr _hWnd;
        private IntPtr _hIcon;
        private ushort _classAtom;
        private TrayNative.WndProcDelegate _wndProcKeepAlive; // 防 GC
        private volatile bool _disposed;

        public SystemTray AddItem(string text, Action onClick)
        {
            lock (_items) _items.Add(MenuItemDef.Item(text, onClick));
            return this;
        }

        public SystemTray AddSeparator()
        {
            lock (_items) _items.Add(MenuItemDef.Separator());
            return this;
        }

        /// <summary>原子替换整张菜单。线程安全：可在 Unity 主线程调；下次右键即生效。
        /// 传入 <c>Text=null</c> 的项 = 分隔符；<c>Enabled=false</c> = 灰色不可点。</summary>
        public void SetItems(IEnumerable<MenuItemDef> items)
        {
            lock (_items)
            {
                _items.Clear();
                if (items != null) _items.AddRange(items);
            }
        }

        /// <summary>外部（如桌宠右键）请求弹出托盘右键菜单。线程安全，菜单会在 tray 线程跑 TrackPopupMenu。</summary>
        public void RequestShowMenu()
        {
            if (_hWnd == IntPtr.Zero) return;
            TrayNative.PostMessage(_hWnd, TrayNative.WM_USER_SHOW_MENU, IntPtr.Zero, IntPtr.Zero);
        }

        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(ThreadProc)
            {
                IsBackground = true,
                Name = "DobeCat.SystemTray",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        /// <summary>Unity 主线程每帧调用，抽取菜单回调。</summary>
        public void PumpMainThread()
        {
            while (_mainThreadQueue.TryDequeue(out var act))
            {
                try { act?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // 投递 WM_DESTROY 到托盘线程触发 GetMessage 返回 0
            if (_hWnd != IntPtr.Zero)
            {
                try { TrayNative.PostMessage(_hWnd, TrayNative.WM_DESTROY, IntPtr.Zero, IntPtr.Zero); }
                catch { /* swallow */ }
            }
        }

        // ─────────────────────────────────────────────────────────
        //  Tray Thread
        // ─────────────────────────────────────────────────────────

        private void ThreadProc()
        {
            try
            {
                var hInst = TrayNative.GetModuleHandle(null);
                _wndProcKeepAlive = WndProc;

                // 1) 注册窗口类
                var wc = new TrayNative.WNDCLASSEX
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(TrayNative.WNDCLASSEX)),
                    lpfnWndProc = _wndProcKeepAlive,
                    hInstance = hInst,
                    lpszClassName = WindowClassName,
                };
                _classAtom = TrayNative.RegisterClassEx(ref wc);
                if (_classAtom == 0)
                {
                    Debug.LogError("[SystemTray] RegisterClassEx 失败");
                    return;
                }

                // 2) 创建隐藏宿主窗口
                _hWnd = TrayNative.CreateWindowEx(
                    0, WindowClassName, "DobeCatTrayHost", TrayNative.WS_OVERLAPPED,
                    TrayNative.CW_USEDEFAULT, TrayNative.CW_USEDEFAULT, 0, 0,
                    IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
                if (_hWnd == IntPtr.Zero)
                {
                    Debug.LogError("[SystemTray] CreateWindowEx 失败");
                    return;
                }

                // 3) 注册托盘图标
                _hIcon = TrayNative.LoadIcon(IntPtr.Zero, TrayNative.IDI_APPLICATION);
                var nid = new TrayNative.NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(TrayNative.NOTIFYICONDATA)),
                    hWnd = _hWnd,
                    uID = TrayIconId,
                    uFlags = TrayNative.NIF_MESSAGE | TrayNative.NIF_ICON | TrayNative.NIF_TIP,
                    uCallbackMessage = TrayNative.WM_TRAYICON,
                    hIcon = _hIcon,
                    szTip = Tooltip ?? "DobeCat",
                    // 必须给 ByValTStr 字段赋非 null 值，否则 Marshal 抛异常 / Shell 拒绝
                    szInfo = string.Empty,
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
                // 构建菜单
                var idToAction = new List<Action>(); // index 0 占位（菜单 ID 从 1 开始）
                idToAction.Add(null);
                MenuItemDef[] snapshot;
                lock (_items) snapshot = _items.ToArray();
                foreach (var def in snapshot)
                {
                    if (string.IsNullOrEmpty(def.Text))
                    {
                        TrayNative.AppendMenu(hMenu, TrayNative.MF_SEPARATOR, IntPtr.Zero, null);
                    }
                    else
                    {
                        idToAction.Add(def.OnClick);
                        var id = idToAction.Count - 1;
                        var flags = TrayNative.MF_STRING;
                        if (!def.Enabled) flags |= TrayNative.MF_GRAYED;
                        TrayNative.AppendMenu(hMenu, flags, new IntPtr(id), def.Text);
                    }
                }

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
                TrayNative.DestroyMenu(hMenu);
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

            public static MenuItemDef Item(string text, Action onClick, bool enabled = true)
                => new MenuItemDef { Text = text, OnClick = onClick, Enabled = enabled };
            public static MenuItemDef Separator()
                => new MenuItemDef { Text = null, OnClick = null, Enabled = false };
            public static MenuItemDef Disabled(string text)
                => new MenuItemDef { Text = text, OnClick = null, Enabled = false };
        }
    }
}
#endif
