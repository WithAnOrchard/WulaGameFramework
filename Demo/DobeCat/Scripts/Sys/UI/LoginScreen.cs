using System;
using System.Collections;
using Demo.DobeCat.Sys.Auth;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 启动期登录界面 —— uGUI 窗口实现（普通应用窗口模式，可拖拽）。
    /// <list type="bullet">
    /// <item>仅含「登录方式切换」+「Token 输入框」+「登录」按钮。</item>
    /// <item>登录通过后回调 <see cref="OnLoginComplete"/>，由 <c>DobeCatGameManager.RunAfterLogin</c> 继续后续初始化。</item>
    /// </list>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        private static readonly string[] LoginMethods =
        {
            "B 站 Cookie",
            "OpenLive",
            "测试 Token",
        };

        private static readonly string[] LoginHints =
        {
            "浏览器登录 bilibili.com → F12 → Application → Cookies → 复制 SESSDATA 值后粘贴到下方",
            "OpenLive 模式需要主播身份码 + appKey，详见 B 站开放平台文档；M2 接入",
            "随便填一段非空字符串即可，仅用于本地调试",
        };

        public Action OnLoginComplete;

        private int      _method;
        private InputField _tokenInput;
        private Button   _loginBtn;
        private Text     _loginBtnText;
        private Text     _errorText;
        private Text     _hintText;
        private bool     _validating;
        private Toggle[] _methodToggles;

        // ─── 颜色表 ───────────────────────────────────────────────────────────
        private static readonly Color ColBg        = new Color(0.11f, 0.12f, 0.15f, 1.00f);
        private static readonly Color ColHeaderBg  = new Color(0.07f, 0.08f, 0.11f, 1.00f);
        private static readonly Color ColDivider   = new Color(0.22f, 0.23f, 0.28f, 1.00f);
        private static readonly Color ColAccent    = new Color(0.23f, 0.51f, 0.96f, 1.00f);
        private static readonly Color ColInputBg   = new Color(0.16f, 0.17f, 0.21f, 1.00f);
        private static readonly Color ColToggleOn  = new Color(0.23f, 0.51f, 0.96f, 1.00f);
        private static readonly Color ColToggleOff = new Color(0.18f, 0.19f, 0.24f, 1.00f);
        private static readonly Color ColTextMain  = new Color(0.94f, 0.94f, 0.96f, 1.00f);
        private static readonly Color ColTextSub   = new Color(0.58f, 0.61f, 0.70f, 1.00f);
        private static readonly Color ColTextHint  = new Color(0.46f, 0.49f, 0.58f, 1.00f);
        private static readonly Color ColError     = new Color(1.00f, 0.42f, 0.42f, 1.00f);
        private static readonly Color ColClose     = new Color(0.70f, 0.18f, 0.18f, 1.00f);

        // ─── 生命周期 ─────────────────────────────────────────────────────────
        private void Awake()
        {
            BuildUI();
            if (_tokenInput != null)
                _tokenInput.text = AuthSession.Token ?? string.Empty;
        }

        // ─── UI 构建 ──────────────────────────────────────────────────────────
        private void BuildUI()
        {
            EnsureEventSystem();

            const float PW  = 460f, PH  = 400f;
            const float PAD = 22f;

            // Canvas
            var canvasGo = new GameObject("LoginCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // 主面板
            var panel = MakeRt("LoginPanel", canvasGo.transform);
            panel.sizeDelta        = new Vector2(PW, PH);
            panel.anchorMin        = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = ColBg;

            // ── 顶部蓝色装饰条（4px）──
            var accentStrip = MakeAnchored("AccentStrip", panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(0, -4), Vector2.zero);
            accentStrip.gameObject.AddComponent<Image>().color = ColAccent;

            // ── Header（4 → 91，高 87px）── 深色背景 + 可拖拽
            var header = MakeAnchored("Header", panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(0, -91), new Vector2(0, -4));
            header.gameObject.AddComponent<Image>().color = ColHeaderBg;
            var drag = header.gameObject.AddComponent<UIDraggable>();
            drag.DragTarget = panel;

            // Logo 方块（左侧，44×44）
            var logoBlock = MakeAnchored("LogoBlock", header,
                new Vector2(0, 0), new Vector2(0, 1),
                new Vector2(PAD, 21), new Vector2(PAD + 44f, -21));
            logoBlock.gameObject.AddComponent<Image>().color = ColAccent;
            var logoTxtRt = MakeRt("LogoText", logoBlock);
            StretchFill(logoTxtRt);
            var logoTxt = logoTxtRt.gameObject.AddComponent<Text>();
            logoTxt.text      = "DC";
            logoTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            logoTxt.fontSize  = 20; logoTxt.fontStyle = FontStyle.Bold;
            logoTxt.alignment = TextAnchor.MiddleCenter;
            logoTxt.color     = Color.white; logoTxt.raycastTarget = false;

            // 标题 + 副标题（Logo 右侧）
            var titleArea = MakeAnchored("TitleArea", header,
                new Vector2(0, 0), Vector2.one,
                new Vector2(PAD + 56f, 0), new Vector2(-44f, 0));
            var appNameRt = MakeRt("AppName", titleArea);
            appNameRt.anchorMin = new Vector2(0, 0.52f); appNameRt.anchorMax = Vector2.one;
            appNameRt.offsetMin = Vector2.zero;           appNameRt.offsetMax = Vector2.zero;
            var appNameTxt = appNameRt.gameObject.AddComponent<Text>();
            appNameTxt.text      = "DobeCat";
            appNameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            appNameTxt.fontSize  = 22; appNameTxt.fontStyle = FontStyle.Bold;
            appNameTxt.alignment = TextAnchor.MiddleLeft;
            appNameTxt.color     = ColTextMain; appNameTxt.raycastTarget = false;

            var subtitleRt = MakeRt("Subtitle", titleArea);
            subtitleRt.anchorMin = Vector2.zero; subtitleRt.anchorMax = new Vector2(1, 0.5f);
            subtitleRt.offsetMin = Vector2.zero;  subtitleRt.offsetMax = Vector2.zero;
            var subtitleTxt = subtitleRt.gameObject.AddComponent<Text>();
            subtitleTxt.text      = "弹幕桌宠控制台";
            subtitleTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subtitleTxt.fontSize  = 11;
            subtitleTxt.alignment = TextAnchor.MiddleLeft;
            subtitleTxt.color     = ColTextSub; subtitleTxt.raycastTarget = false;

            // 关闭按钮（Header 右侧 44px）
            var closeRt = MakeAnchored("CloseBtn", header,
                new Vector2(1, 0), Vector2.one, new Vector2(-44, 0), Vector2.zero);
            var closeBg = closeRt.gameObject.AddComponent<Image>();
            closeBg.color = Color.clear;
            MakeText(closeRt.gameObject, "×", 22, FontStyle.Normal,
                TextAnchor.MiddleCenter, new Color(0.65f, 0.68f, 0.78f));
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            var closeCb = ColorBlock.defaultColorBlock;
            closeCb.normalColor      = Color.clear;
            closeCb.highlightedColor = ColClose;
            closeCb.pressedColor     = new Color(0.9f, 0.1f, 0.1f, 1f);
            closeCb.selectedColor    = Color.clear;
            closeBtn.colors          = closeCb;
            closeBtn.onClick.AddListener(QuitApp);

            // ── 分割线（91px 处，1px）──
            var divider = MakeAnchored("Divider", panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(0, -92), new Vector2(0, -91));
            divider.gameObject.AddComponent<Image>().color = ColDivider;

            // ── 内容行 ──
            float y = 92f;

            y += 16f;
            AddLabelRow("MethodLabel", panel, PAD, ref y, 18f,
                "登录方式", 11, ColTextSub);

            y += 5f;
            BuildMethodToggles(panel, PAD, ref y, 36f);

            y += 10f;
            var hintRt = AddLabelRow("HintText", panel, PAD, ref y, 40f,
                LoginHints[0], 11, ColTextHint);
            _hintText = hintRt.gameObject.GetComponent<Text>();
            if (_hintText != null)
            {
                _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _hintText.alignment          = TextAnchor.UpperLeft;
                _hintText.verticalOverflow   = VerticalWrapMode.Overflow;
            }

            y += 12f;
            AddLabelRow("TokenLabel", panel, PAD, ref y, 18f,
                "Token", 11, ColTextSub);

            y += 5f;
            BuildInputField(panel, PAD, ref y, 42f);

            y += 12f;
            BuildLoginButton(panel, PAD, ref y, 48f);

            y += 8f;
            var errRt = AddLabelRow("ErrorText", panel, PAD, ref y, 20f,
                "", 11, ColError);
            _errorText = errRt.gameObject.GetComponent<Text>();
            if (_errorText != null) _errorText.alignment = TextAnchor.MiddleCenter;
        }

        private void BuildMethodToggles(RectTransform panel, float pad, ref float y, float h)
        {
            var rowRt = MakeAnchored("MethodRow", panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(pad, -(y + h)), new Vector2(-pad, -y));
            y += h;

            var group = rowRt.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = false;
            var hlg = rowRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing             = 4f;
            hlg.childControlWidth   = true;
            hlg.childControlHeight  = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            rowRt.gameObject.AddComponent<Image>().color = Color.clear;

            _methodToggles = new Toggle[LoginMethods.Length];
            for (var i = 0; i < LoginMethods.Length; i++)
            {
                var idx   = i;
                var togGo = new GameObject($"Toggle_{i}");
                togGo.transform.SetParent(rowRt, false);
                var togRt = togGo.AddComponent<RectTransform>();
                togRt.localScale = Vector3.one;

                var togImg = togGo.AddComponent<Image>();
                togImg.color = i == 0 ? ColToggleOn : ColToggleOff;

                var tog = togGo.AddComponent<Toggle>();
                tog.group         = group;
                tog.isOn          = i == 0;
                tog.targetGraphic = togImg;
                var cb = ColorBlock.defaultColorBlock;
                cb.normalColor      = Color.white;
                cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
                cb.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
                cb.selectedColor    = Color.white;
                tog.colors = cb;

                var lblGo = new GameObject("Label");
                lblGo.transform.SetParent(togGo.transform, false);
                var lblRt = lblGo.AddComponent<RectTransform>();
                lblRt.anchorMin  = Vector2.zero; lblRt.anchorMax  = Vector2.one;
                lblRt.offsetMin  = Vector2.zero; lblRt.offsetMax  = Vector2.zero;
                lblRt.localScale = Vector3.one;
                var lbl = lblGo.AddComponent<Text>();
                lbl.text          = LoginMethods[i];
                lbl.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.fontSize      = 12;
                lbl.color         = i == 0 ? ColTextMain : ColTextSub;
                lbl.alignment     = TextAnchor.MiddleCenter;
                lbl.raycastTarget = false;

                tog.onValueChanged.AddListener(on =>
                {
                    togImg.color = on ? ColToggleOn : ColToggleOff;
                    lbl.color    = on ? ColTextMain : ColTextSub;
                    if (on) OnMethodChanged(idx);
                });
                _methodToggles[i] = tog;
            }
        }

        private void BuildInputField(RectTransform panel, float pad, ref float y, float h)
        {
            var rt = MakeAnchored("TokenInput", panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(pad, -(y + h)), new Vector2(-pad, -y));
            y += h;

            rt.gameObject.AddComponent<Image>().color = ColInputBg;

            // 左侧蓝色竖边装饰（3px）
            var accentBar = new GameObject("AccentBar");
            accentBar.transform.SetParent(rt, false);
            var abRt = accentBar.AddComponent<RectTransform>();
            abRt.anchorMin  = Vector2.zero; abRt.anchorMax  = new Vector2(0, 1);
            abRt.offsetMin  = Vector2.zero; abRt.offsetMax  = new Vector2(3, 0);
            abRt.localScale = Vector3.one;
            accentBar.AddComponent<Image>().color = ColAccent;

            _tokenInput = rt.gameObject.AddComponent<InputField>();
            _tokenInput.targetGraphic = rt.gameObject.GetComponent<Image>();
            _tokenInput.contentType   = InputField.ContentType.Password;
            _tokenInput.lineType      = InputField.LineType.SingleLine;

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(rt, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(8, 2); txtRt.offsetMax = new Vector2(-8, -2);
            txtRt.localScale = Vector3.one;
            var txt = txtGo.AddComponent<Text>();
            txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize  = 13;
            txt.color     = ColTextMain;
            txt.alignment = TextAnchor.MiddleLeft;
            _tokenInput.textComponent = txt;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(rt, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(8, 2); phRt.offsetMax = new Vector2(-8, -2);
            phRt.localScale = Vector3.one;
            var ph = phGo.AddComponent<Text>();
            ph.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ph.fontSize   = 13;
            ph.fontStyle  = FontStyle.Italic;
            ph.color      = new Color(0.42f, 0.44f, 0.50f);
            ph.alignment  = TextAnchor.MiddleLeft;
            ph.text       = "请粘贴 Token...";
            _tokenInput.placeholder = ph;

            _tokenInput.onSubmit.AddListener(_ => TryLogin());
        }

        private void BuildLoginButton(RectTransform panel, float pad, ref float y, float h)
        {
            var rt = MakeAnchored("LoginBtn", panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(pad, -(y + h)), new Vector2(-pad, -y));
            y += h;

            rt.gameObject.AddComponent<Image>().color = ColAccent;
            _loginBtn = rt.gameObject.AddComponent<Button>();
            _loginBtn.targetGraphic = rt.gameObject.GetComponent<Image>();
            _loginBtn.onClick.AddListener(TryLogin);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(rt, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;
            txtRt.localScale = Vector3.one;
            _loginBtnText = txtGo.AddComponent<Text>();
            _loginBtnText.text      = "登 录";
            _loginBtnText.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _loginBtnText.fontSize  = 16;
            _loginBtnText.fontStyle = FontStyle.Bold;
            _loginBtnText.color     = Color.white;
            _loginBtnText.alignment = TextAnchor.MiddleCenter;
            _loginBtnText.raycastTarget = false;
        }

        // ─── UI 辅助 ──────────────────────────────────────────────────────────
        private static RectTransform MakeRt(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.localScale = Vector3.one;
            return rt;
        }

        private static RectTransform MakeAnchored(string name, Transform parent,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = MakeRt(name, parent);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        private static void MakeText(GameObject go, string text, int fontSize,
            FontStyle style, TextAnchor align, Color color)
        {
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var rt = txtGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            var t = txtGo.AddComponent<Text>();
            t.text          = text;
            t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize      = fontSize;
            t.fontStyle     = style;
            t.alignment     = align;
            t.color         = color;
            t.raycastTarget = false;
        }

        private static Text AddLabelRow(string name, RectTransform panel, float pad,
            ref float y, float h, string text, int fontSize, Color color)
        {
            var rt = MakeAnchored(name, panel,
                new Vector2(0, 1), Vector2.one,
                new Vector2(pad, -(y + h)), new Vector2(-pad, -y));
            y += h;
            var t = rt.gameObject.AddComponent<Text>();
            t.text              = text;
            t.font              = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize          = fontSize;
            t.color             = color;
            t.alignment         = TextAnchor.MiddleLeft;
            t.raycastTarget     = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow  = VerticalWrapMode.Overflow;
            return t;
        }

        private static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void QuitApp()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ─── 业务逻辑 ─────────────────────────────────────────────────────────
        private void OnMethodChanged(int idx)
        {
            _method = idx;
            if (_hintText  != null) _hintText.text  = LoginHints[idx];
            if (_errorText != null) _errorText.text = "";
        }

        private void SetInteractable(bool v)
        {
            if (_loginBtn   != null) _loginBtn.interactable   = v;
            if (_tokenInput != null) _tokenInput.interactable = v;
            if (_methodToggles != null)
                foreach (var tog in _methodToggles)
                    if (tog != null) tog.interactable = v;
        }

        private void SetLoginBtnLabel(string s)
        {
            if (_loginBtnText != null) _loginBtnText.text = s;
        }

        private IEnumerator AutoValidateCachedToken()
        {
            var token = _tokenInput != null ? _tokenInput.text : AuthSession.Token ?? "";
            var passed = false;
            yield return BilibiliAuthValidator.Validate(
                token,
                onSuccess: (uname, mid) =>
                {
                    passed = true;
                    AuthSession.Login(token, uname, mid);
                    Debug.Log($"[LoginScreen] 缓存 token 验证通过：uname={uname}");
                },
                onFail: msg =>
                {
                    Debug.LogWarning($"[LoginScreen] 缓存 token 失效：{msg}");
                    AuthSession.Logout();
                    if (_tokenInput != null) _tokenInput.text = "";
                });

            _validating = false;
            SetInteractable(true);
            SetLoginBtnLabel("登 录");
            if (passed)
                FinishLogin(token, AuthSession.Nickname, AuthSession.Mid);
            else if (_errorText != null)
                _errorText.text = "上次登录已过期，请重新输入 Token。";
        }

        private void TryLogin()
        {
            if (_validating) return;
            var token = _tokenInput != null ? _tokenInput.text : "";
            if (string.IsNullOrWhiteSpace(token))
            {
                if (_errorText != null) _errorText.text = "Token 不能为空。";
                return;
            }
            if (_method == 2)
            {
                FinishLogin(token, "本地调试", 0L);
                return;
            }
            if (_errorText != null) _errorText.text = "";
            _validating = true;
            SetInteractable(false);
            SetLoginBtnLabel("验证中...");
            StartCoroutine(BilibiliAuthValidator.Validate(
                token,
                onSuccess: (uname, mid) =>
                {
                    _validating = false;
                    SetInteractable(true);
                    SetLoginBtnLabel("登 录");
                    Debug.Log($"[LoginScreen] B 站验证通过：uname={uname}, mid={mid}");
                    FinishLogin(token, uname, mid);
                },
                onFail: msg =>
                {
                    _validating = false;
                    SetInteractable(true);
                    SetLoginBtnLabel("登 录");
                    if (_errorText != null) _errorText.text = msg;
                    Debug.LogWarning($"[LoginScreen] B 站验证失败：{msg}");
                }));
        }

        private void FinishLogin(string token, string uname, long mid)
        {
            if (!AuthSession.Login(token, uname, mid))
            {
                if (_errorText != null) _errorText.text = "Token 不能为空。";
                return;
            }
            try { OnLoginComplete?.Invoke(); }
            finally { Destroy(gameObject); }
        }
    }
}
