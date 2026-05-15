using UnityEngine;

namespace EssSystem.Core.EssManagers.Presentation.UIManager.Dao
{
    /// <summary>
    ///     UI adjustable base class - provides position, size, and scale properties
    ///     UI adjustable base class -  position, size, scale
    /// </summary>
    public abstract class Adjustable
    {
        #region Utility Methods

        /// <summary>
        ///     Apply properties to RectTransform
        ///     RectTransform
        /// </summary>
        /// <param name="rectTransform">Target RectTransform</param>
        public virtual void ApplyToRectTransform(RectTransform rectTransform)
        {
            if (rectTransform == null) return;

            rectTransform.anchoredPosition = Position;
            rectTransform.sizeDelta = Size;
            rectTransform.localScale = new Vector3(Scale.x, Scale.y, 1f);
        }

        #endregion

        #region Properties

        private Vector2 _position;
        private Vector2 _size = Vector2.one;
        private Vector2 _scale = Vector2.one;

        /// <summary>
        ///     Position
        /// </summary>
        public Vector2 Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    OnPositionChanged(value);
                }
            }
        }

        /// <summary>
        ///     Size
        /// </summary>
        public Vector2 Size
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnSizeChanged(value);
                }
            }
        }

        /// <summary>
        ///     Scale
        /// </summary>
        public Vector2 Scale
        {
            get => _scale;
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    OnScaleChanged(value);
                }
            }
        }

        #endregion

        #region Virtual Methods

        /// <summary>
        ///     直接写入 _position 字段，**不触发** <see cref="OnPositionChanged"/> 回调和事件广播。
        ///     用于每帧高频定位（如 HUD 跟随世界坐标），避免 <c>EVT_DAO_PROPERTY_CHANGED</c> 事件噪声。
        ///     <para>调用方通常已持有 RectTransform 引用，直接写 <c>anchoredPosition</c>；
        ///     本方法仅保持 DAO 字段与实际 UI 状态同步。</para>
        /// </summary>
        public void SetPositionSilent(float x, float y)
        {
            _position = new Vector2(x, y);
        }

        /// <summary>
        ///     Called when position changes
        ///     position
        /// </summary>
        /// <param name="newPosition">New position</param>
        protected virtual void OnPositionChanged(Vector2 newPosition)
        {
        }

        /// <summary>
        ///     Called when size changes
        ///     size
        /// </summary>
        /// <param name="newSize">New size</param>
        protected virtual void OnSizeChanged(Vector2 newSize)
        {
        }

        /// <summary>
        ///     Called when scale changes
        ///     scale
        /// </summary>
        /// <param name="newScale">New scale</param>
        protected virtual void OnScaleChanged(Vector2 newScale)
        {
        }

        #endregion
    }
}