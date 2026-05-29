using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.UIManager.Entity.CommonEntity
{
    /// <summary>
    /// 输入框 Entity — 对应 <see cref="UIInputComponent"/>，封装 legacy <see cref="InputField"/>。
    /// 使用内置字体，不依赖 TMP Essentials，与 <see cref="UIButtonEntity"/> 保持一致的技术栈。
    /// 实现 <see cref="ISelectHandler"/> 以在获得焦点时通知平台层（BringToForeground）。
    /// </summary>
    public class UIInputEntity : UIEntity, ISelectHandler
    {
        /// <summary>任意输入框被选中时触发（供平台层抢焦点用）。</summary>
        public static event System.Action OnAnyInputSelected;

        private InputField _inputField;
        private Text       _textLegacy;
        private Text       _placeholderLegacy;

        private static Font _builtinFont;
        
        private static Font GetBuiltinFont()
        {
            if (_builtinFont == null)
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            if (_inputField != null) return;

            // ── 1. 背景 Image ───────────────────────────────────────────────────
            var bg = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.18f, 0.24f, 1f);

            // ── 2. Placeholder ─────────────────────────────────────────────────
            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(transform, false);
            var phRt = phGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero; phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(6f, 2f); phRt.offsetMax = new Vector2(-6f, -2f);
            _placeholderLegacy = phGo.AddComponent<Text>();
            _placeholderLegacy.font           = GetBuiltinFont();
            _placeholderLegacy.color          = new Color(0.9f, 0.9f, 0.9f, 0.45f);
            _placeholderLegacy.fontSize       = 14;
            _placeholderLegacy.alignment      = TextAnchor.MiddleCenter;
            _placeholderLegacy.raycastTarget  = false;
            _placeholderLegacy.supportRichText = false;

            // ── 3. Text ────────────────────────────────────────────────────────
            var tGo = new GameObject("Text");
            tGo.transform.SetParent(transform, false);
            var tRt = tGo.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(6f, 2f); tRt.offsetMax = new Vector2(-6f, -2f);
            _textLegacy = tGo.AddComponent<Text>();
            _textLegacy.font           = GetBuiltinFont();
            _textLegacy.color          = Color.white;
            _textLegacy.fontSize       = 14;
            _textLegacy.alignment      = TextAnchor.MiddleCenter;
            _textLegacy.raycastTarget  = false;
            _textLegacy.supportRichText = false;

            // ── 4. InputField（legacy）─────────────────────────────────────────
            _inputField = gameObject.AddComponent<InputField>();
            _inputField.targetGraphic = bg;
            _inputField.textComponent = _textLegacy;
            _inputField.placeholder   = _placeholderLegacy;
            _inputField.transition    = Selectable.Transition.None;

            // ── 5. 事件监听 ────────────────────────────────────────────────────
            _inputField.onValueChanged.AddListener(OnValueChangedHandler);
            _inputField.onEndEdit.AddListener(OnEndEditHandler);
        }

        // ISelectHandler：InputField 获得焦点时 EventSystem 也会通知此 GO
        public void OnSelect(BaseEventData eventData)
        {
            OnAnyInputSelected?.Invoke();
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is not UIInputComponent dao || _inputField == null) return;

            if (_inputField.targetGraphic is Image img) img.color = dao.BgColor;

            if (_textLegacy != null)
            {
                _textLegacy.color    = dao.TextColor;
                _textLegacy.fontSize = Mathf.Max(1, dao.FontSize);
            }

            if (_placeholderLegacy != null)
            {
                _placeholderLegacy.text     = dao.Placeholder;
                _placeholderLegacy.color    = new Color(dao.TextColor.r, dao.TextColor.g, dao.TextColor.b, 0.45f);
                _placeholderLegacy.fontSize = Mathf.Max(1, dao.FontSize);
            }

            _inputField.characterLimit = dao.CharacterLimit;
            _inputField.contentType    = MapContentType(dao.ContentType);
            _inputField.text           = dao.Text;
        }

        public override void OnDaoPropertyChanged(string propertyName, object value)
        {
            base.OnDaoPropertyChanged(propertyName, value);
            if (_inputField == null || Dao is not UIInputComponent dao) return;

            switch (propertyName)
            {
                case "Text":
                    if (_inputField.text != dao.Text)
                        _inputField.text = dao.Text;
                    break;
                case "Placeholder":
                    if (_placeholderLegacy != null) _placeholderLegacy.text = dao.Placeholder;
                    break;
                case "BgColor":
                    if (_inputField.targetGraphic is Image img) img.color = dao.BgColor;
                    break;
                case "TextColor":
                    if (_textLegacy != null) _textLegacy.color = dao.TextColor;
                    break;
            }
        }

        private void OnValueChangedHandler(string v)
        {
            if (Dao is UIInputComponent dao)
            {
                dao.Text = v;
                dao.RaiseValueChanged(v);
            }
        }

        private void OnEndEditHandler(string v)
        {
            if (Dao is UIInputComponent dao) dao.RaiseEndEdit(v);
        }

        private static InputField.ContentType MapContentType(UIInputComponent.InputType t) => t switch
        {
            UIInputComponent.InputType.Integer  => InputField.ContentType.IntegerNumber,
            UIInputComponent.InputType.Decimal  => InputField.ContentType.DecimalNumber,
            UIInputComponent.InputType.Password => InputField.ContentType.Password,
            _                                   => InputField.ContentType.Standard,
        };
    }
}
