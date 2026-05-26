using System;
using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager.Dao.CommonComponents
{
    /// <summary>
    /// 输入框 DAO — 对应 <see cref="UIType.InputField"/>（TMP_InputField）。
    /// </summary>
    public class UIInputComponent : UIComponent
    {
        public enum InputType { Standard, Integer, Decimal, Password }

        private string _text        = "";
        private string _placeholder = "";
        private int    _charLimit   = 0;
        private Color  _bgColor     = new Color(0.15f, 0.18f, 0.24f, 1f);
        private Color  _textColor   = new Color(0.90f, 0.90f, 0.90f, 1f);
        private int    _fontSize    = 14;

        public InputType ContentType { get; set; } = InputType.Standard;

        public string Text
        {
            get => _text;
            set { if (_text != value) { _text = value ?? ""; NotifyEntityPropertyChanged("Text", _text); } }
        }

        public string Placeholder
        {
            get => _placeholder;
            set { if (_placeholder != value) { _placeholder = value ?? ""; NotifyEntityPropertyChanged("Placeholder", _placeholder); } }
        }

        public int CharacterLimit
        {
            get => _charLimit;
            set { if (_charLimit != value) { _charLimit = value; NotifyEntityPropertyChanged("CharacterLimit", _charLimit); } }
        }

        public Color BgColor
        {
            get => _bgColor;
            set { if (_bgColor != value) { _bgColor = value; NotifyEntityPropertyChanged("BgColor", _bgColor); } }
        }

        public Color TextColor
        {
            get => _textColor;
            set { if (_textColor != value) { _textColor = value; NotifyEntityPropertyChanged("TextColor", _textColor); } }
        }

        public int FontSize
        {
            get => _fontSize;
            set { if (_fontSize != value) { _fontSize = value; NotifyEntityPropertyChanged("FontSize", _fontSize); } }
        }

        /// <summary>每次文字变化时触发。</summary>
        public event Action<string> OnValueChanged;
        /// <summary>失焦或按 Enter 确认时触发。</summary>
        public event Action<string> OnEndEdit;

        internal void RaiseValueChanged(string v) => OnValueChanged?.Invoke(v);
        internal void RaiseEndEdit(string v)      => OnEndEdit?.Invoke(v);

        public UIInputComponent(string id, string placeholder = "", string name = null)
            : base(id, UIType.InputField, name)
        {
            _placeholder = placeholder;
        }

        public UIInputComponent SetText(string text)               { Text = text; return this; }
        public UIInputComponent SetPlaceholder(string ph)          { Placeholder = ph; return this; }
        public UIInputComponent SetCharLimit(int n)                { CharacterLimit = n; return this; }
        public UIInputComponent SetBgColor(Color c)                { BgColor = c; return this; }
        public UIInputComponent SetTextColor(Color c)              { TextColor = c; return this; }
        public UIInputComponent SetFontSize(int s)                 { FontSize = s; return this; }
        public UIInputComponent SetContentType(InputType t)        { ContentType = t; return this; }

        public UIInputComponent SetPosition(float x, float y)     => SetPosition<UIInputComponent>(x, y);
        public UIInputComponent SetSize(float w, float h)          => SetSize<UIInputComponent>(w, h);
        public UIInputComponent SetVisible(bool v)                 => SetVisible<UIInputComponent>(v);
        public UIInputComponent SetInteractable(bool v)            => SetInteractable<UIInputComponent>(v);
    }
}
