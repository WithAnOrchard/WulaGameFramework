using System;
using System.Collections;
using BiliBiliDanmu.Auth;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using EssSystem.Core.Presentation.UIManager.Entity.CommonEntity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 启动期登录界面 —— 走 UIManager 体系（UIPanel/UIText/UIInput/UIButton DAO 树）。
    /// <list type="bullet">
    /// <item>挂在透明叠加层之上的 <see cref="DobeCatCanvasProvider"/> Canvas，居中显示 460×400 面板。</item>
    /// <item>3 个登录方式按钮（视觉 Tab）+ Token 输入框 + 登录按钮 + 错误提示。</item>
    /// <item>登录通过后回调 <see cref="OnLoginComplete"/>，由 DobeCatGameManager.RunAfterLogin 接续。</item>
    /// </list>
    /// </summary>
    public class LoginScreen : MonoBehaviour
    {
        private const string DAO_ROOT = "login-root";
        private const float PW = 460f, PH = 520f, PAD = 22f;

        private static readonly string[] LoginMethods = { "B 站 Cookie", "OpenLive" };
        private static readonly string[] LoginHints =
        {
            "浏览器登录 bilibili.com → F12 → Application → Cookies → 复制 SESSDATA 值后粘贴到下方",
            "OpenLive 模式需要主播身份码 + appKey，详见 B 站开放平台文档；M2 接入",
        };

        // ─── 颜色由 DobeCatTheme 提供（BuildUI 内读 Current；事件处理器用属性）───
        private static Color ColTabOn  => DobeCatTheme.Current.Accent;
        private static Color ColTabOff => DobeCatTheme.Current.ButtonBg;
        private static Color ColError  => DobeCatTheme.Current.ButtonRed;

        public Action OnLoginComplete;

        private int _method;
        private bool _validating;

        // 每帧穿透检测
        private PointerEventData _uiPointerData;
        private readonly System.Collections.Generic.List<RaycastResult> _uiRaycastResults =
            new System.Collections.Generic.List<RaycastResult>();

        // DAO 引用（用于运行时更新文字 / 颜色）
        private UITextComponent   _hintDao;
        private UITextComponent   _errorDao;
        private UITextComponent   _loginBtnLabelDao;
        private UIButtonComponent _loginBtnDao;
        private UIInputComponent  _tokenDao;
        private UIButtonComponent[] _tabBtns;

        // Entity 引用（用于直接操作 GO，比如隐藏整个面板）
        private UIEntity _rootEntity;
        private UIInputEntity _tokenEntity;

        private void Awake()
        {
            EnsureEventSystem();
            BuildUI();
            // 加载缓存 token（若有）
            if (_tokenDao != null) _tokenDao.SetText(BilibiliAuthSession.Token ?? string.Empty);
            DobeCatTheme.OnThemeChanged += RebuildUI;
        }

        private void Update()
        {
            if (EventSystem.current == null) return;
            var screenPos = EssSystem.Core.Platform.Windows.DesktopOverlay.GetGlobalCursorScreenPos();
            if (_uiPointerData == null)
                _uiPointerData = new PointerEventData(EventSystem.current);
            _uiPointerData.position = screenPos;
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(_uiPointerData, _uiRaycastResults);
            EssSystem.Core.Platform.Windows.DesktopOverlay.SetClickThrough(_uiRaycastResults.Count == 0);
        }

        private void OnDestroy()
        {
            DobeCatTheme.OnThemeChanged -= RebuildUI;
            EssSystem.Core.Platform.Windows.DesktopOverlay.SetClickThrough(false);
        }

        private void RebuildUI()
        {
            var savedToken  = _tokenDao?.Text ?? "";
            var savedMethod = _method;
            if (_rootEntity != null && _rootEntity.gameObject != null)
                Destroy(_rootEntity.gameObject);
            if (UIService.HasInstance) UIService.Instance.DestroyUIEntity(DAO_ROOT);
            _rootEntity  = null;
            _tokenEntity = null;
            BuildUI();
            if (_tokenDao != null && !string.IsNullOrEmpty(savedToken)) _tokenDao.SetText(savedToken);
            if (savedMethod != 0) OnMethodChanged(savedMethod);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI 构建
        // ─────────────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) { Debug.LogWarning("[LoginScreen] Canvas 未就绪"); return; }

            var t    = DobeCatTheme.Current;
            var cBg  = t.Background;
            var cHdr = t.Header;
            var cAcc = t.Accent;
            var cDiv = t.Divider;
            var cTM  = t.TextOnHeader;
            var cTS  = t.TextSub;

            // 根面板
            var root = new UIPanelComponent(DAO_ROOT)
                .SetBackgroundColor(cBg)
                .SetSize(PW, PH)
                .SetPosition(PW / 2f, PH / 2f); // 临时坐标，注册后改锚点居中

            // 标题栏（44px）
            const float titleBarH = 44f;
            var titleBar = new UIPanelComponent("login-header")
                .SetBackgroundColor(cHdr)
                .SetSize(PW, titleBarH)
                .SetPosition(PW / 2f, PH - titleBarH / 2f);
            root.AddChild(titleBar);

            titleBar.AddChild(new UITextComponent("login-title", text: "DobeCat 弹幕桌宠")
                .SetSize(300f, titleBarH)
                .SetPosition(PAD + 150f, titleBarH / 2f)
                .SetFontSize(14)
                .SetColor(cTM)
                .SetAlignment(TextAnchor.MiddleLeft));

            var closeBtn = new UIButtonComponent("login-close", text: "×")
                .SetSize(44f, titleBarH)
                .SetPosition(PW - 22f, titleBarH / 2f)
                .SetButtonColor(t.Close)
                .SetFontSize(20);
            closeBtn.OnClick += _ => QuitApp();
            titleBar.AddChild(closeBtn);

            // 分割线
            float yTop = PH - titleBarH; // 内容区顶
            root.AddChild(new UIPanelComponent("login-divider")
                .SetBackgroundColor(cDiv)
                .SetSize(PW, 1f)
                .SetPosition(PW / 2f, yTop - 0.5f));

            // 内容区域：从 yTop 往下排
            float y = yTop - 16f; // 距分割线 16px 留白

            // 「登录方式」标签
            const float labelH = 18f;
            root.AddChild(new UITextComponent("login-method-label", text: "登录方式")
                .SetSize(PW - PAD * 2, labelH)
                .SetPosition(PW / 2f, y - labelH / 2f)
                .SetFontSize(11)
                .SetColor(cTS)
                .SetAlignment(TextAnchor.MiddleLeft));
            y -= labelH + 5f;

            // 3 个 Tab 按钮
            const float tabH = 36f;
            float tabRowW = PW - PAD * 2;
            float tabW = (tabRowW - 4f * 2f) / 3f; // 4px gap × 2
            _tabBtns = new UIButtonComponent[LoginMethods.Length];
            for (int i = 0; i < LoginMethods.Length; i++)
            {
                int idx = i;
                var btn = new UIButtonComponent($"login-tab-{i}", text: LoginMethods[i])
                    .SetSize(tabW, tabH)
                    .SetPosition(PAD + tabW / 2f + i * (tabW + 4f), y - tabH / 2f)
                    .SetButtonColor(i == 0 ? ColTabOn : ColTabOff)
                    .SetFontSize(12);
                btn.OnClick += _ => OnMethodChanged(idx);
                root.AddChild(btn);
                _tabBtns[i] = btn;
            }
            y -= tabH + 10f;

            // 提示文字（40px 高，会换行）
            _hintDao = new UITextComponent("login-hint", text: LoginHints[0])
                .SetSize(PW - PAD * 2, 64f)
                .SetPosition(PW / 2f, y - 32f)
                .SetFontSize(11)
                .SetColor(cTS)
                .SetAlignment(TextAnchor.UpperLeft);
            root.AddChild(_hintDao);
            y -= 64f + 12f;

            // 「Token」标签
            root.AddChild(new UITextComponent("login-token-label", text: "Token")
                .SetSize(PW - PAD * 2, labelH)
                .SetPosition(PW / 2f, y - labelH / 2f)
                .SetFontSize(11)
                .SetColor(cTS)
                .SetAlignment(TextAnchor.MiddleLeft));
            y -= labelH + 5f;

            // Token 输入框
            const float inputH = 42f;
            _tokenDao = new UIInputComponent("login-token", placeholder: "请粘贴 Token...")
                .SetSize(PW - PAD * 2, inputH)
                .SetPosition(PW / 2f, y - inputH / 2f)
                .SetFontSize(13)
                .SetContentType(UIInputComponent.InputType.Password);
            _tokenDao.OnEndEdit += _ => TryLogin();
            root.AddChild(_tokenDao);
            y -= inputH + 12f;

            // 登录按钮
            const float btnH = 48f;
            _loginBtnDao = new UIButtonComponent("login-btn", text: "登 录")
                .SetSize(PW - PAD * 2, btnH)
                .SetPosition(PW / 2f, y - btnH / 2f)
                .SetButtonColor(cAcc)
                .SetFontSize(16);
            _loginBtnDao.OnClick += _ => TryLogin();
            root.AddChild(_loginBtnDao);
            y -= btnH + 8f;

            // 错误提示
            _errorDao = new UITextComponent("login-error", text: "")
                .SetSize(PW - PAD * 2, 20f)
                .SetPosition(PW / 2f, y - 10f)
                .SetFontSize(11)
                .SetColor(ColError)
                .SetAlignment(TextAnchor.MiddleCenter);
            root.AddChild(_errorDao);

            // ── 注册到 UIManager ──
            _rootEntity = UIService.Instance.RegisterUIEntity(DAO_ROOT, root, canvasT);
            if (_rootEntity == null) { Debug.LogWarning("[LoginScreen] RegisterUIEntity 失败"); return; }

            // 整体居中：anchor & pivot 中心，sizeDelta 固定 PW×PH
            var rt = _rootEntity.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(PW, PH);

            var wb = _rootEntity.gameObject.AddComponent<UIWindowBehavior>();
            wb.EnableTopBar   = true;
            wb.TitleBarHeight = titleBarH; // 44px

            _tokenEntity = UIService.Instance.GetUIEntity("login-token") as UIInputEntity;

            // 登录按钮文字 entity 引用（用于切换 "登 录" / "验证中..."）
            var loginBtnEntity = UIService.Instance.GetUIEntity("login-btn");
            if (loginBtnEntity != null)
            {
                var txt = loginBtnEntity.GetComponentInChildren<Text>();
                if (txt != null) { txt.text = "登 录"; txt.fontSize = 16; txt.fontStyle = FontStyle.Bold; txt.color = Color.white; }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 事件处理
        // ─────────────────────────────────────────────────────────────────────
        private void OnMethodChanged(int idx)
        {
            _method = idx;
            for (int i = 0; i < _tabBtns.Length; i++)
                _tabBtns[i].SetButtonColor(i == idx ? ColTabOn : ColTabOff);
            if (_hintDao != null) _hintDao.SetText(LoginHints[idx]);
            if (_errorDao != null) _errorDao.SetText("");
        }

        private void SetInteractable(bool v)
        {
            if (_loginBtnDao != null) _loginBtnDao.SetInteractable(v);
            if (_tokenDao != null) _tokenDao.SetInteractable(v);
            if (_tabBtns != null)
                foreach (var b in _tabBtns) b?.SetInteractable(v);
        }

        private void SetLoginBtnLabel(string s)
        {
            if (_loginBtnDao != null) _loginBtnDao.SetText(s);
        }

        private void TryLogin()
        {
            if (_validating) return;
            var token = _tokenDao != null ? _tokenDao.Text : "";
            if (string.IsNullOrWhiteSpace(token))
            {
                if (_errorDao != null) _errorDao.SetText("Token 不能为空。");
                return;
            }
            if (_errorDao != null) _errorDao.SetText("");
            _validating = true;
            SetInteractable(false);
            SetLoginBtnLabel("验证中...");
            StartCoroutine(BiliBiliDanmu.Auth.BilibiliAuthValidator.Validate(
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
                    if (_errorDao != null) _errorDao.SetText(msg);
                    Debug.LogWarning($"[LoginScreen] B 站验证失败：{msg}");
                }));
        }

        private void FinishLogin(string token, string uname, long mid)
        {
            if (!BilibiliAuthSession.Login(token, uname, mid))
            {
                if (_errorDao != null) _errorDao.SetText("Token 不能为空。");
                return;
            }
            // 立即销毁面板：UIService 会清理缓存与 GameObject
            if (_rootEntity != null && _rootEntity.gameObject != null)
                _rootEntity.gameObject.SetActive(false);
            try { OnLoginComplete?.Invoke(); }
            finally
            {
                if (UIService.HasInstance) UIService.Instance.DestroyUIEntity(DAO_ROOT);
                Destroy(gameObject);
            }
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
    }
}
