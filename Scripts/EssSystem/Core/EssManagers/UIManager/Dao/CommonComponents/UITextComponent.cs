using System;
using EssSystem.Core.UI.Dao;
using UnityEngine;

namespace EssSystem.Core.UI.Dao.CommonComponents
{
    /// <summary>
    ///     UI文本组件
    /// </summary>
    public class UITextComponent : UIComponent
    {
        /// <summary>
        ///     文本对齐方式
        /// </summary>
        private TextAnchor _alignment = TextAnchor.UpperLeft;

        /// <summary>
        ///     文本颜色
        /// </summary>
        private Color _color = Color.black;

        /// <summary>
        ///     字体大小
        /// </summary>
        private int _fontSize = 14;

        /// <summary>
        ///     文本内容
        /// </summary>
        private string _text = string.Empty;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="id">组件ID</param>
        /// <param name="name">组件名称</param>
        /// <param name="text">文本内容</param>
        public UITextComponent(string id, string name = null, string text = "")
            : base(id, UIType.Text, name)
        {
            Text = text;
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value ?? string.Empty;
                    OnTextChanged(_text);
                }
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = Mathf.Max(1, value);
                    OnFontSizeChanged(_fontSize);
                }
            }
        }

        public Color Color
        {
            get => _color;
            set
            {
                if (_color != value)
                {
                    _color = value;
                    OnColorChanged(_color);
                }
            }
        }

        public TextAnchor Alignment
        {
            get => _alignment;
            set
            {
                if (_alignment != value)
                {
                    _alignment = value;
                    OnAlignmentChanged(_alignment);
                }
            }
        }

        /// <summary>
        ///     设置文本
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UITextComponent SetText(string text)
        {
            Text = text ?? string.Empty;
            return this;
        }

        /// <summary>
        ///     设置字体大小
        /// </summary>
        /// <param name="fontSize">字体大小</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UITextComponent SetFontSize(int fontSize)
        {
            FontSize = fontSize;
            return this;
        }

        /// <summary>
        ///     设置文本颜色
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UITextComponent SetColor(Color color)
        {
            Color = color;
            return this;
        }

        /// <summary>
        ///     设置对齐方式
        /// </summary>
        /// <param name="alignment">对齐方式</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UITextComponent SetAlignment(TextAnchor alignment)
        {
            Alignment = alignment;
            return this;
        }

        /// <summary>
        ///     字符串表示
        /// </summary>
        /// <returns>字符串</returns>
        public override string ToString()
        {
            return $"UITextComponent[Id={Id}, Text={Text.Substring(0, Math.Min(20, Text.Length))}...]";
        }

        #region Property Change Notifications

        protected virtual void OnTextChanged(string newText)
        {
            NotifyEntityPropertyChanged("Text", newText);
        }

        protected virtual void OnFontSizeChanged(int newFontSize)
        {
            NotifyEntityPropertyChanged("FontSize", newFontSize);
        }

        protected virtual void OnColorChanged(Color newColor)
        {
            NotifyEntityPropertyChanged("Color", newColor);
        }

        protected virtual void OnAlignmentChanged(TextAnchor newAlignment)
        {
            NotifyEntityPropertyChanged("Alignment", newAlignment);
        }

        // Simplified chain methods for better usability
        public UITextComponent SetPosition(float x, float y)
        {
            return SetPosition<UITextComponent>(x, y);
        }

        public UITextComponent SetSize(float width, float height)
        {
            return SetSize<UITextComponent>(width, height);
        }

        public UITextComponent SetScale(float scaleX, float scaleY)
        {
            return SetScale<UITextComponent>(scaleX, scaleY);
        }

        public UITextComponent SetVisible(bool visible)
        {
            return SetVisible<UITextComponent>(visible);
        }

        public UITextComponent SetInteractable(bool interactable)
        {
            return SetInteractable<UITextComponent>(interactable);
        }

        #endregion
    }
}