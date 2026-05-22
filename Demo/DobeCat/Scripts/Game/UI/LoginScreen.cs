using System;
using Demo.DobeCat.Game.Auth;
using Demo.DobeCat.Sys.Platform.Windows;
using UnityEngine;

namespace Demo.DobeCat.Game.UI
{
    /// <summary>
    /// 启动期登录界面 —— IMGUI 浮层实现。
    /// <list type="bullet">
    /// <item>仅含「登录方式下拉」+「Token 输入框」+「登录」按钮。</item>
    /// <item>渲染在 <c>DesktopWindow</c> 的全屏透明窗口里，登录卡片自身约 460×360 居中（无独立 Win32 子窗口）。</item>
    /// <item>登录通过后回调 <see cref="OnLoginComplete"/>，由 <c>DobeCatGameManager.RunAfterLogin</c> 继续后续初始化。</item>
    /// </list>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        // 登录卡片尺寸（在屏幕中央渲染，周围保持透明，桌面可见）
        private const int CardW = 460;
        private const int CardH = 360;

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
        private GUIStyle _closeBtnStyle;
        private Texture2D _bgTex;
        // 卡片矩形 —— OnGUI 算一次，Update 复用做点击穿透 hit-test
        private Rect _cardRect;

        private void Awake()
        {
            _token = AuthSession.Token ?? string.Empty;
            // 不再修改 Win32 窗口尺寸/样式 —— 由 DesktopWindow 统一掌管，登录界面只是这个窗口里的一块 IMGUI 卡片。
        }

        /// <summary>
        /// 鼠标在卡片矩形外时打开窗口点击穿透，让用户可以正常操作桌面 / 任务栏；
        /// 一旦移到卡片上立刻关穿透，保证卡片自身的下拉、输入框、按钮能交互。
        /// 桌宠窗口默认是 LAYERED + topmost 全屏，不做穿透切换的话整张桌面都被它吞掉点击。
        /// </summary>
        private void Update()
        {
            var dw = DesktopWindow.Instance;
            if (dw == null) return;
            if (_cardRect.width <= 0f) { dw.SetClickThrough(true); return; }

            var mx = Input.mousePosition.x;
            // Input.mousePosition 原点在左下，IMGUI / _cardRect 在左上 —— 翻转 Y
            var my = Screen.height - Input.mousePosition.y;
            var inside = mx >= _cardRect.xMin && mx <= _cardRect.xMax
                      && my >= _cardRect.yMin && my <= _cardRect.yMax;
            dw.SetClickThrough(!inside);
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
            if (_closeBtnStyle == null)
            {
                _closeBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize  = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding   = new RectOffset(0, 0, 0, 0),
                    normal    = { textColor = new Color(0.85f, 0.85f, 0.85f) },
                };
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 居中绘制登录卡片：周围保持透明，桌宠透明窗会让 DWM 透出桌面。
            _cardRect = new Rect(
                (Screen.width  - CardW) * 0.5f,
                (Screen.height - CardH) * 0.5f,
                CardW, CardH);
            GUI.DrawTexture(_cardRect, _bgTex);

            // 右上角关闭按钮（×）—— 唯一退出途径，避免桌面被卡片遮挡时无法关闭
            var closeRect = new Rect(_cardRect.xMax - 32f, _cardRect.y + 6f, 26f, 22f);
            if (GUI.Button(closeRect, "×", _closeBtnStyle))
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }

            const float pad = 18f;
            var area = new Rect(_cardRect.x + pad, _cardRect.y + pad + 6f, _cardRect.width - pad * 2f, _cardRect.height - pad * 2f - 6f);
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
            // 关穿透交还给 PetClickThroughDriver，避免登录完成瞬间桌宠还没注入就丢一帧"全穿透"
            var dw = DesktopWindow.Instance;
            if (dw != null) dw.SetClickThrough(false);
        }
    }
}
