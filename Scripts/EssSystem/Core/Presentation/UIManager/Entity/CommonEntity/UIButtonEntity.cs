using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Base.Event;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.UIManager.Entity.CommonEntity
{
    /// <summary>
    ///     UI Entity
    /// </summary>
    public class UIButtonEntity : UIEntity
    {
        private Button _button;
        private Text _text;
        private Image _image;

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _button = gameObject.GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            _image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _text = GetComponentInChildren<Text>() ?? CreateTextComponent();
            if (_text != null) _text.raycastTarget = false;
            _button.onClick.AddListener(OnButtonClick);
        }

        private Text CreateTextComponent()
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;

            var text = textObj.AddComponent<Text>();
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.pivot = new Vector2(0.5f, 0.5f);

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;

            return text;
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UIButtonComponent buttonDao)
            {
                if (_text != null) _text.text = buttonDao.Text;
                if (_button != null) _button.interactable = Dao.Interactable;
                if (_image != null) _image.color = buttonDao.ButtonColor;

                // 通过Event机制从ResourceManager加载Sprite
                if (!string.IsNullOrEmpty(buttonDao.ButtonSpriteId) && _image != null)
                {
                    LoadSpriteFromId(buttonDao.ButtonSpriteId);
                }
            }
        }

        public override void OnDaoPropertyChanged(string propertyName, object value)
        {
            base.OnDaoPropertyChanged(propertyName, value);

            if (Dao is UIButtonComponent buttonDao && _image != null)
            {
                switch (propertyName)
                {
                    case "Text":
                        if (_text != null) _text.text = buttonDao.Text;
                        break;

                    case "ButtonSpriteId":
                        if (!string.IsNullOrEmpty(buttonDao.ButtonSpriteId))
                        {
                            LoadSpriteFromId(buttonDao.ButtonSpriteId);
                        }
                        else
                        {
                            _image.sprite = null;
                        }
                        break;

                    case "ButtonColor":
                        _image.color = buttonDao.ButtonColor;
                        break;
                }
            }
        }

        private void OnButtonClick()
        {
            if (Dao is UIButtonComponent buttonDao) buttonDao.Click();
        }

        private void LoadSpriteFromId(string spriteId)
        {
            try
            {
                var result = EventProcessor.Instance.TriggerEventMethod("GetResource",
                    new System.Collections.Generic.List<object> { spriteId, "Sprite", false });

                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                {
                    var sprite = result[1] as Sprite;
                    if (sprite != null && _image != null)
                    {
                        _image.sprite = sprite;
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"加载Sprite失败: {spriteId}, 错误: {ex.Message}");
            }
        }

        public Button GetButton()
        {
            return _button;
        }

        public Text GetText()
        {
            return _text;
        }
    }
}