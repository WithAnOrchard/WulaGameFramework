using System;

namespace EssSystem.UIManager.Dao
{
    /// <summary>
    /// UI按钮组件
    /// </summary>
    public class UIButtonComponent : UIComponent
    {
        /// <summary>
        /// 按钮文本
        /// </summary>
        private string _text = string.Empty;
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
        /// 点击事件
        /// </summary>
        public event Action<UIButtonComponent> OnClick;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="id">组件ID</param>
        /// <param name="name">组件名称</param>
        /// <param name="text">按钮文本</param>
        public UIButtonComponent(string id, string name = null, string text = "") 
            : base(id, UIType.Button, name)
        {
            Text = text;
        }

        /// <summary>
        /// 设置文本
        /// </summary>
        /// <param name="text">文本内容</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIButtonComponent SetText(string text)
        {
            Text = text ?? string.Empty;
            return this;
        }

        protected virtual void OnTextChanged(string newText)
        {
            NotifyEntityPropertyChanged("Text", newText);
        }

        // Simplified chain methods for better usability
        public new UIButtonComponent SetPosition(float x, float y) => SetPosition<UIButtonComponent>(x, y);
        public new UIButtonComponent SetSize(float width, float height) => SetSize<UIButtonComponent>(width, height);
        public new UIButtonComponent SetScale(float scaleX, float scaleY) => SetScale<UIButtonComponent>(scaleX, scaleY);
        public new UIButtonComponent SetVisible(bool visible) => SetVisible<UIButtonComponent>(visible);
        public new UIButtonComponent SetInteractable(bool interactable) => SetInteractable<UIButtonComponent>(interactable);

        /// <summary>
        /// 模拟点击
        /// </summary>
        /// <returns>当前组件，支持链式调用</returns>
        public UIButtonComponent Click()
        {
            if (Interactable)
            {
                OnClick?.Invoke(this);
            }
            return this;
        }

        /// <summary>
        /// 字符串表示
        /// </summary>
        /// <returns>字符串</returns>
        public override string ToString()
        {
            return $"UIButtonComponent[Id={Id}, Text={Text}]";
        }
    }
}
