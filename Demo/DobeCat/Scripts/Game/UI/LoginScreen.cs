using System;
using Demo.DobeCat.Game.Auth;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Demo.DobeCat.Sys.Platform.Windows;
#endif

namespace Demo.DobeCat.Game.UI
{
    /// <summary>
    /// 启动期登录界面 —— IMGUI 实现。
    /// <list type="bullet">
    /// <item>仅含「登录方式下拉」+「Token 输入框」+「登录」按钮。</item>
    /// <item>窗口本身被 <c>SkipSplash.PrelayoutLoginWindow</c> 压成 460×360 无边框 + WS_EX_LAYERED + DWM 玻璃帧，IMGUI 铺满整窗。</item>
    /// <item>登录通过后回调 <see cref="OnLoginComplete"/>，<c>DesktopWindow</c> 接管把窗口扩成全屏透明 + 启用 ColorKey 后处理。</item>
    /// </list>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {

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
            // SkipSplash 已经把窗口压成 460x360 + LAYERED + DWM 玻璃帧，但还隐藏着。
            // 等 IMGUI 第一次 OnGUI 把不透明背景画上去（backbuffer alpha=1）再 show，避免一帧透明闪烁。
            // 直接 show 也 OK，DWM 会用 backbuffer 的现有内容，第一帧可能是相机底色。
            if (SkipSplash.HiddenHwnd != IntPtr.Zero) SkipSplash.ShowMainWindow(SkipSplash.HiddenHwnd);
#endif
        }

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

            // 窗口本身已经是 460×360 登录尺寸，整窗铺满深色背景。
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
