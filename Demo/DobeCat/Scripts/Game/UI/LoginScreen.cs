using System;
using Demo.DobeCat.Game.Auth;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Demo.DobeCat.Sys.Platform.Windows;
#endif

namespace Demo.DobeCat.Game.UI
{
    /// <summary>
    /// 启动期登录界面 —— M1 暂用 IMGUI（无需依赖 UGUI InputField 等组件，零配置）。
    /// 登录成功后回调 <see cref="OnLoginComplete"/> 并自动销毁自身。
    /// <para>注意：本界面期间 <see cref="Sys.Platform.Windows.DesktopWindow"/> 尚未应用透明 / click-through，
    /// 主窗口处于"普通可点击 Unity Standalone 窗口"状态，IMGUI 即可正常接收输入。</para>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        /// <summary>登录成功后回调（保证在主线程触发）。</summary>
        public Action OnLoginComplete;

        private string _token = string.Empty;
        private string _error;
        private GUIStyle _titleStyle;
        private GUIStyle _errorStyle;
        private Texture2D _bgTex;

        private void Awake()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // SkipSplash 启动时把主窗口隐藏了，登录界面必须先把它显示出来；
            // 后续 DesktopWindow.ApplyDesktopWindow 会再次调用 ShowMainWindow（幂等）。
            try
            {
                var hwnd = SkipSplash.HiddenHwnd;
                if (hwnd != IntPtr.Zero) SkipSplash.ShowMainWindow(hwnd);
            }
            catch { /* ignore */ }
#endif
            // 预读已保存的 token，方便用户确认
            _token = AuthSession.Token ?? string.Empty;
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
            if (_errorStyle == null)
            {
                _errorStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 13,
                    alignment = TextAnchor.MiddleCenter,
                    normal    = { textColor = new Color(1f, 0.4f, 0.4f) },
                };
            }
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1);
                _bgTex.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.08f, 1f));
                _bgTex.Apply();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            // 全屏不透明黑底，遮住背后的桌宠相机（alpha=0 clear）
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _bgTex);

            const float w = 480f, h = 320f;
            var rect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Space(8);
            GUILayout.Label("DobeCat 登录", _titleStyle);
            GUILayout.Space(8);

            GUILayout.Label("请输入 Token（B 站 Cookie / 第三方颁发的访问令牌）：");
            _token = GUILayout.TextField(_token ?? string.Empty, GUILayout.Height(28));

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("从剪贴板粘贴", GUILayout.Height(36)))
            {
                _token = GUIUtility.systemCopyBuffer ?? string.Empty;
                _error = null;
            }
            if (GUILayout.Button("清空", GUILayout.Width(80f), GUILayout.Height(36)))
            {
                _token = string.Empty;
                _error = null;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("登 录", GUILayout.Height(44)))
            {
                TryLogin();
            }
            if (GUILayout.Button("退出", GUILayout.Width(100f), GUILayout.Height(44)))
            {
                QuitApp();
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_error))
            {
                GUILayout.Space(4);
                GUILayout.Label(_error, _errorStyle);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Token 仅保存在本机 PlayerPrefs，下次启动自动登录。", new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter });

            GUILayout.EndArea();

            // 回车键登录
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
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

        private static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_bgTex != null) Destroy(_bgTex);
        }
    }
}
