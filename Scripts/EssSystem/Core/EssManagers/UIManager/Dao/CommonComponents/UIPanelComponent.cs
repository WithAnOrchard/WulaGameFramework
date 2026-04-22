using System.Linq;
using UnityEngine;

namespace EssSystem.UIManager.Dao
{
    /// <summary>
    ///     UI面板组件 - 容器类UI组件
    /// </summary>
    public class UIPanelComponent : UIComponent
    {
        /// <summary>
        ///     背景颜色
        /// </summary>
        private Color _backgroundColor = Color.clear;

        /// <summary>
        ///     构造函数
        /// </summary>
        /// <param name="id">组件ID</param>
        /// <param name="name">组件名称</param>
        public UIPanelComponent(string id, string name = null)
            : base(id, UIType.Panel, name)
        {
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    OnBackgroundColorChanged(value);
                }
            }
        }

        /// <summary>
        ///     设置背景颜色
        /// </summary>
        /// <param name="color">颜色</param>
        /// <returns>当前组件，支持链式调用</returns>
        public UIPanelComponent SetBackgroundColor(Color color)
        {
            BackgroundColor = color;
            return this;
        }

        protected virtual void OnBackgroundColorChanged(Color newColor)
        {
            NotifyEntityPropertyChanged("BackgroundColor", newColor);
        }

        // Simplified chain methods for better usability
        public new UIPanelComponent SetPosition(float x, float y)
        {
            return SetPosition<UIPanelComponent>(x, y);
        }

        public new UIPanelComponent SetSize(float width, float height)
        {
            return SetSize<UIPanelComponent>(width, height);
        }

        public new UIPanelComponent SetScale(float scaleX, float scaleY)
        {
            return SetScale<UIPanelComponent>(scaleX, scaleY);
        }

        public new UIPanelComponent SetVisible(bool visible)
        {
            return SetVisible<UIPanelComponent>(visible);
        }

        public new UIPanelComponent SetInteractable(bool interactable)
        {
            return SetInteractable<UIPanelComponent>(interactable);
        }

        /// <summary>
        ///     字符串表示
        /// </summary>
        /// <returns>字符串</returns>
        public override string ToString()
        {
            return $"UIPanelComponent[Id={Id}, Children={GetChildren().Count()}]";
        }
    }
}