using System;
using Demo.DobeCat.Game.Auth;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Demo.DobeCat.Sys.Platform.Windows;
#endif

namespace Demo.DobeCat.Game.UI
{
    /// <summary>
    /// 启动期登录界面 —— M1 IMGUI 实现。
    /// <list type="bullet">
    /// <item>仅含「登录方式下拉」+「Token 输入框」+「登录」按钮。</item>
    /// <item>窗口本身缩成登录框尺寸 + 无边框（Standalone Win 通过 Win32 修整 HWND）。</item>
    /// <item>登录通过后回调 <see cref="OnLoginComplete"/>，由 <c>DesktopWindow</c> 重新撑满工作区。</item>
    /// </list>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        // 窗口尺寸 —— 对齐 SkipSplash.LaunchWidth/Height（启动期已按这个尺寸预排版）。
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static int WinW => SkipSplash.LaunchWidth;
        private static int WinH => SkipSplash.LaunchHeight;
#else
        private const int WinW = 460;
        private const int WinH = 360;
#endif

        /// <summary>登录方式（下拉项）。当前只支持 BiliBili Cookie，预留扩展位。</summary>
        private static readonly string[] LoginMethods =
        {
            "B 站 Cookie (SESSDATA)",
            "OpenLive 身份码（开发中）",
            "测试 / 占位 Token",
        };

        /// <summary>登录方式对应的 token 获取说明。</summary>
        private static readonly string[] LoginHints =
        {
            "1. 浏览器登录 https://www.bilibili.com\n2. F12 → 应用 → Cookies → 复制 SESSDATA 值\n3. 粘贴到下方输入框，回车或点击登录。",
            "OpenLive 模式需要主播身份码 + appKey，详见 B 站开放平台文档；M2 接入。",
            "随便填一段非空字符串即可，仅用于本地调试。",
        };

        public Action OnLoginComplete;

        private int _method;
        private bool _methodOpen;
        private string _token = string.Empty;
        private string _error;

        // 样式（懒构建）
        private GUIStyle _bgStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _errorStyle;
        private GUIStyle _dropdownBtnStyle;
        private GUIStyle _primaryBtnStyle;
        private Texture2D _bgTex;

        private void Awake()
        {
            _token = AuthSession.Token ?? string.Empty;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            ApplyLoginWindowStyle();
#else
            // Editor / 非 Win：用 Unity API 调小 Game/Player 窗口便于预览
            try { Screen.SetResolution(WinW, WinH, FullScreenMode.Windowed); }
            catch { /* ignore */ }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static void ApplyLoginWindowStyle()
        {
            try
            {
                // SkipSplash 启动时把主窗口隐藏；先 SetResolution 再显示，可避免一帧旧尺寸闪烁。
                Screen.SetResolution(WinW, WinH, FullScreenMode.Windowed);

                var hwnd = SkipSplash.HiddenHwnd;
                if (hwnd == IntPtr.Zero) hwnd = Win32Native.GetActiveWindow();
                if (hwnd == IntPtr.Zero) hwnd = Win32Native.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                // 去除标题栏 / 边框 / 系统菜单 / 最大最小化按钮，但保留 WS_POPUP 让窗口可见。
                var style = Win32Native.GetWindowLong(hwnd, Win32Native.GWL_STYLE);
                style &= ~(Win32Native.WS_CAPTION
                           | Win32Native.WS_THICKFRAME
                           | Win32Native.WS_SYSMENU
                           | Win32Native.WS_MINIMIZEBOX
                           | Win32Native.WS_MAXIMIZEBOX);
                style |= Win32Native.WS_POPUP | Win32Native.WS_VISIBLE;
                Win32Native.SetWindowLong(hwnd, Win32Native.GWL_STYLE, style);

                // 居中到主屏
                var sw = Display.main.systemWidth;
                var sh = Display.main.systemHeight;
                var x = Mathf.Max(0, (sw - WinW) / 2);
                var y = Mathf.Max(0, (sh - WinH) / 2);
                Win32Native.SetWindowPos(hwnd, IntPtr.Zero,
                    x, y, WinW, WinH,
                    Win32Native.SWP_FRAMECHANGED | Win32Native.SWP_SHOWWINDOW | Win32Native.SWP_NOZORDER);

                SkipSplash.ShowMainWindow(hwnd);
            }
            catch { /* 失败不致命，IMGUI 仍能在默认 Standalone 窗口里渲染 */ }
        }
#endif

        private void EnsureStyles()
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1);
                _bgTex.SetPixel(0, 0, new Color(0.10f, 0.11f, 0.13f, 1f));
                _bgTex.Apply();
            }
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = new Color(0.95f, 0.95f, 0.95f) },
                };
            }
            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 11,
                    wordWrap  = true,
                    alignment = TextAnchor.UpperLeft,
                    normal    = { textColor = new Color(0.75f, 0.78f, 0.85f) },
                };
            }
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 12,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = new Color(1f, 0.45f, 0.45f) },
                };
            }
            if (_dropdownBtnStyle == null)
            {
                _dropdownBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize  = 13,
                    padding   = new RectOffset(10, 10, 6, 6),
                };
            }
            if (_primaryBtnStyle == null)
            {
                _primaryBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 16,
                    fontStyle = FontStyle.Bold,
                };
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 把整窗都涂成深色（无边框 → 没有原生背景）
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

            const float pad = 18f;
            var area = new Rect(pad, pad, Screen.width - pad * 2f, Screen.height - pad * 2f);
            GUILayout.BeginArea(area);

            GUILayout.Label("DobeCat 登录", _titleStyle);
            GUILayout.Space(10);

            // 1) 登录方式下拉
            GUILayout.Label("登录方式：", _hintStyle);
            DrawMethodDropdown();
            GUILayout.Space(8);

            // 2) Token 获取方式说明（嵌在界面上）
            GUILayout.Label(LoginHints[_method], _hintStyle);
            GUILayout.Space(8);

            // 3) Token 输入框
            GUILayout.Label("Token：", _hintStyle);
            _token = GUILayout.TextField(_token ?? string.Empty, GUILayout.Height(28));

            GUILayout.Space(10);

            // 4) 登录按钮（唯一动作按钮）
            if (GUILayout.Button("登 录", _primaryBtnStyle, GUILayout.Height(40)))
            {
                TryLogin();
            }

            if (!string.IsNullOrEmpty(_error))
            {
                GUILayout.Space(4);
                GUILayout.Label(_error, _errorStyle);
            }

            GUILayout.EndArea();

            HandleHotkeys();
        }

        /// <summary>简易自绘下拉：当前选项以按钮形式显示，点开后铺一列选项按钮。</summary>
        private void DrawMethodDropdown()
        {
            var label = (_methodOpen ? "▼ " : "▶ ") + LoginMethods[_method];
            if (GUILayout.Button(label, _dropdownBtnStyle, GUILayout.Height(28)))
            {
                _methodOpen = !_methodOpen;
            }
            if (!_methodOpen) return;

            for (var i = 0; i < LoginMethods.Length; i++)
            {
                if (i == _method) continue; // 当前已选不重复列
                if (GUILayout.Button("  " + LoginMethods[i], _dropdownBtnStyle, GUILayout.Height(24)))
                {
                    _method = i;
                    _methodOpen = false;
                    _error = null;
                }
            }
        }

        private void HandleHotkeys()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                TryLogin();
                e.Use();
            }
        }

        private void TryLogin()
        {
            if (!AuthSession.Login(_token))
            {
                _error = "Token 不能为空。";
                return;
            }
            try { OnLoginComplete?.Invoke(); }
            finally { Destroy(gameObject); }
        }

        private void OnDestroy()
        {
            if (_bgTex != null) Destroy(_bgTex);
        }
    }
}
