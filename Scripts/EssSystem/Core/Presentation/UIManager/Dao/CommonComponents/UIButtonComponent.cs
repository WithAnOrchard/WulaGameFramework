using System;
using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager.Dao.CommonComponents
{
    /// <summary>
    ///     UI按钮组件
    /// </summary>
    public class UIButtonComponent : UIComponent
    {
        /// <summary>
        ///     按钮文本
        /// </summary>
        private string _text = string.Empty;

        /// <summary>
        ///     按钮背景 Sprite ID
        /// </summary>
        private string _buttonSpriteId;

        /// <summary>
        ///     按钮背景 Image 颜色（作为 Unity Image.color 应用；sprite 有色套白、无 sprite 可作纯色块）。
        /// </summary>
        private Color _buttonColor = Color.white;

        /// <summary>  
        ///     构造函数
        /// </summary>
        /// <param name="id">组件ID</param>
        /// <param name="name">组件名称</param>
        /// <param name="text">按钮文本</param>
        public UIButtonComponent(string id, string name = null, string text = "")
            : base(id, UIType.Button, name)
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

        /// <summary>
        ///     按钮背景 Sprite ID
        /// </summary>
        public string ButtonSpriteId
        {
            get => _buttonSpriteId;
            set
            {
                if (_buttonSpriteId != value)
                {
                    _buttonSpriteId = value;
                    OnButtonSpriteIdChanged(value);
                }
            }
        }

        /// <summary>
        ///     按钮背景 Image 颜色。与 <see cref="ButtonSpriteId"/> 联合作为 Image.color 应用（色套白）。
        /// </summary>
        public Color ButtonColor
        {
            get => _buttonColor;
            set
            {
                if (_buttonColor != value)
                {
                    _buttonColor = value;
                    OnButtonColorChanged(value);
                }
            }
        }

        /// <summary>
        ///     点击事件
        /// </summary>
        public event Action<UIButtonComponent> OnClick;

        /// <summary>
        ///     设置文本
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIButtonComponent SetText(string text)
        {
            Text = text ?? string.Empty;
            return this;
        }

        /// <summary>
        ///     设置按钮背景 Sprite ID
        /// </summary>
        /// <param name="spriteId">Sprite ID</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIButtonComponent SetButtonSpriteId(string spriteId)
        {
            ButtonSpriteId = spriteId;
            return this;
        }

        /// <summary>
        ///     设置按钮背景颜色（Image.color）。使用现成 Sprite 时该色作为色套；无 Sprite 时作为纯色块。
        /// </summary>
        public UIButtonComponent SetButtonColor(Color color)
        {
            ButtonColor = color;
            return this;
        }

        protected virtual void OnTextChanged(string newText)
        {
            NotifyEntityPropertyChanged("Text", newText);
        }

        protected virtual void OnButtonSpriteIdChanged(string newId)
        {
            NotifyEntityPropertyChanged("ButtonSpriteId", newId);
        }

        protected virtual void OnButtonColorChanged(Color newColor)
        {
            NotifyEntityPropertyChanged("ButtonColor", newColor);
        }

        // Simplified chain methods for better usability
        public UIButtonComponent SetPosition(float x, float y)
        {
            return SetPosition<UIButtonComponent>(x, y);
        }

        public UIButtonComponent SetSize(float width, float height)
        {
            return SetSize<UIButtonComponent>(width, height);
        }

        public UIButtonComponent SetScale(float scaleX, float scaleY)
        {
            return SetScale<UIButtonComponent>(scaleX, scaleY);
        }

        public UIButtonComponent SetVisible(bool visible)
        {
            return SetVisible<UIButtonComponent>(visible);
        }

        public UIButtonComponent SetInteractable(bool interactable)
        {
            return SetInteractable<UIButtonComponent>(interactable);
        }

        /// <summary>
        ///     模拟点击
        /// </summary>
        /// <returns>当前组件，支持链式调用</returns>
        public UIButtonComponent Click()
        {
            if (Interactable) OnClick?.Invoke(this);
            return this;
        }

        /// <summary>
        ///     字符串表示
        /// </summary>
        /// <returns>字符串</returns>
        public override string ToString()
        {
            return $"UIButtonComponent[Id={Id}, Text={Text}]";
        }
    }
}