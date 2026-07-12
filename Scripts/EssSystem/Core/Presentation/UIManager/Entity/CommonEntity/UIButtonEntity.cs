using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Base.Event;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace EssSystem.Core.Presentation.UIManager.Entity.CommonEntity
{
    /// <summary>
    ///     UI Entity
    /// </summary>
    public class UIButtonEntity : UIEntity, IPointerEnterHandler, IPointerExitHandler
    {
        private Button _button;
        private TextMeshProUGUI _text;
        private Text _legacyText;
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
            DisableKeyboardNavigation(_button);
            _legacyText = GetComponentInChildren<Text>();
            _text = GetComponentInChildren<TextMeshProUGUI>();
            // 仅在既无 legacy 又无 TMP 时才尝试创建 TMP；UIEntityFactory 已为 Button 创建 legacy Text，
            // 正常路径下走 _legacyText，避免 TMP Essentials 未导入时的 NullReferenceException 中断 Awake。
            if (_text == null && _legacyText == null)
            {
                try { _text = CreateTextComponent(); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[UIButtonEntity] 创建 TMP Text 失败（请在 Editor 中 Window → TextMeshPro → Import TMP Essential Resources）: {e.Message}");
                }
            }
            if (_text != null) _text.raycastTarget = false;
            if (_legacyText != null) _legacyText.raycastTarget = false;
            _button.onClick.AddListener(OnButtonClick);
        }

        private TextMeshProUGUI CreateTextComponent()
        {
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(transform);
            textObj.transform.localPosition = Vector3.zero;

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            textRect.pivot     = new Vector2(0.5f, 0.5f);

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize          = 14;
            text.enableAutoSizing  = true;          // SDF 自适应大小，零模糊
            text.fontSizeMin       = 1;
            text.fontSizeMax       = 300;
            text.color             = Color.white;
            text.alignment         = TextAlignmentOptions.Center;
            text.overflowMode      = TextOverflowModes.Overflow;

            return text;
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UIButtonComponent buttonDao)
            {
                if (_text != null) _text.text     = buttonDao.Text;
                if (_text != null) _text.fontSizeMin = buttonDao.FontSize;
                if (_legacyText != null) { _legacyText.text = buttonDao.Text; _legacyText.fontSize = Mathf.Max(1, buttonDao.FontSize); }
                if (_button != null) _button.interactable = Dao.Interactable;
                ApplyButtonColor(buttonDao.ButtonColor);

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
                        if (_legacyText != null) _legacyText.text = buttonDao.Text;
                        break;

                    case "FontSize":
                        if (_text != null) _text.fontSizeMin = buttonDao.FontSize;
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
                        ApplyButtonColor(buttonDao.ButtonColor);
                        break;
                }
            }
        }

        private void OnButtonClick()
        {
            if (Dao is UIButtonComponent buttonDao) buttonDao.Click();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Dao is UIButtonComponent buttonDao) buttonDao.HoverEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Dao is UIButtonComponent buttonDao) buttonDao.HoverExit();
        }

        private static void DisableKeyboardNavigation(Button button)
        {
            if (button == null) return;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        /// <summary>
        /// 同时更新 Image.color 与 Button ColorBlock，防止 ColorTint 过渡覆盖颜色。
        /// </summary>
        private void ApplyButtonColor(Color c)
        {
            if (_image  != null) _image.color = c;
            if (_button != null)
            {
                var cb = _button.colors;
                cb.normalColor      = c;
                cb.highlightedColor = new Color(Mathf.Min(c.r * 1.15f, 1f), Mathf.Min(c.g * 1.15f, 1f), Mathf.Min(c.b * 1.15f, 1f), c.a);
                cb.pressedColor     = new Color(c.r * 0.80f, c.g * 0.80f, c.b * 0.80f, c.a);
                cb.selectedColor    = c;
                cb.disabledColor    = new Color(c.r * 0.50f, c.g * 0.50f, c.b * 0.50f, c.a * 0.60f);
                _button.colors      = cb;
            }
        }

        private void LoadSpriteFromId(string spriteId)
        {
            if (string.IsNullOrEmpty(spriteId) || _image == null) return;
            
            EventProcessor.Instance.TriggerEventMethod("GetSpriteAsync",
                new System.Collections.Generic.List<object> { spriteId });
                
            AsyncSpriteLoader.StartLoad(spriteId, _image);
        }

        public Button GetButton()
        {
            return _button;
        }

        public TextMeshProUGUI GetText()
        {
            return _text;
        }
    }
}
