using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager.Dao.CommonComponents
{
    public class UIBarComponent : UIComponent
    {
        private float _value;
        private float _minValue;
        private float _maxValue = 1f;
        private Color _backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        private Color _fillColor = Color.green;
        private string _backgroundSpriteId;
        private string _fillSpriteId;
        private Vector2 _fillPadding = new Vector2(6f, 4f);

        public UIBarComponent(string id, string name = null)
            : base(id, UIType.Bar, name)
        {
        }

        public float Value
        {
            get => _value;
            set
            {
                var clamped = Mathf.Clamp(value, MinValue, MaxValue);
                if (!Mathf.Approximately(_value, clamped))
                {
                    _value = clamped;
                    OnValueChanged(_value);
                }
            }
        }

        public float MinValue
        {
            get => _minValue;
            set
            {
                var next = Mathf.Min(value, MaxValue);
                if (!Mathf.Approximately(_minValue, next))
                {
                    _minValue = next;
                    if (_value < _minValue) _value = _minValue;
                    OnRangeChanged();
                }
            }
        }

        public float MaxValue
        {
            get => _maxValue;
            set
            {
                var next = Mathf.Max(value, MinValue + 0.0001f);
                if (!Mathf.Approximately(_maxValue, next))
                {
                    _maxValue = next;
                    if (_value > _maxValue) _value = _maxValue;
                    OnRangeChanged();
                }
            }
        }

        public float Percent => Mathf.Approximately(MaxValue, MinValue)
            ? 0f
            : Mathf.Clamp01((Value - MinValue) / (MaxValue - MinValue));

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    NotifyEntityPropertyChanged("BackgroundColor", value);
                }
            }
        }

        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (_fillColor != value)
                {
                    _fillColor = value;
                    NotifyEntityPropertyChanged("FillColor", value);
                }
            }
        }

        public string BackgroundSpriteId
        {
            get => _backgroundSpriteId;
            set
            {
                if (_backgroundSpriteId != value)
                {
                    _backgroundSpriteId = value;
                    NotifyEntityPropertyChanged("BackgroundSpriteId", value);
                }
            }
        }

        public string FillSpriteId
        {
            get => _fillSpriteId;
            set
            {
                if (_fillSpriteId != value)
                {
                    _fillSpriteId = value;
                    NotifyEntityPropertyChanged("FillSpriteId", value);
                }
            }
        }

        public Vector2 FillPadding
        {
            get => _fillPadding;
            set
            {
                var next = new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y));
                if (_fillPadding != next)
                {
                    _fillPadding = next;
                    NotifyEntityPropertyChanged("FillPadding", _fillPadding);
                }
            }
        }

        public UIBarComponent SetValue(float value)
        {
            Value = value;
            return this;
        }

        public UIBarComponent SetValue(float value, float maxValue)
        {
            MaxValue = maxValue;
            Value = value;
            return this;
        }

        public UIBarComponent SetRange(float minValue, float maxValue)
        {
            _minValue = Mathf.Min(minValue, maxValue - 0.0001f);
            _maxValue = Mathf.Max(maxValue, _minValue + 0.0001f);
            _value = Mathf.Clamp(_value, _minValue, _maxValue);
            OnRangeChanged();
            return this;
        }

        public UIBarComponent SetBackgroundColor(Color color)
        {
            BackgroundColor = color;
            return this;
        }

        public UIBarComponent SetFillColor(Color color)
        {
            FillColor = color;
            return this;
        }

        public UIBarComponent SetBackgroundSpriteId(string spriteId)
        {
            BackgroundSpriteId = spriteId;
            return this;
        }

        public UIBarComponent SetFillSpriteId(string spriteId)
        {
            FillSpriteId = spriteId;
            return this;
        }

        public UIBarComponent SetFillPadding(float horizontal, float vertical)
        {
            FillPadding = new Vector2(horizontal, vertical);
            return this;
        }

        public UIBarComponent SetPosition(float x, float y)
        {
            return SetPosition<UIBarComponent>(x, y);
        }

        public UIBarComponent SetSize(float width, float height)
        {
            return SetSize<UIBarComponent>(width, height);
        }

        public UIBarComponent SetScale(float scaleX, float scaleY)
        {
            return SetScale<UIBarComponent>(scaleX, scaleY);
        }

        public UIBarComponent SetVisible(bool visible)
        {
            return SetVisible<UIBarComponent>(visible);
        }

        public UIBarComponent SetInteractable(bool interactable)
        {
            return SetInteractable<UIBarComponent>(interactable);
        }

        private void OnValueChanged(float value)
        {
            NotifyEntityPropertyChanged("Value", value);
            NotifyEntityPropertyChanged("Percent", Percent);
        }

        private void OnRangeChanged()
        {
            NotifyEntityPropertyChanged("Range", null);
            NotifyEntityPropertyChanged("Percent", Percent);
        }
    }
}
