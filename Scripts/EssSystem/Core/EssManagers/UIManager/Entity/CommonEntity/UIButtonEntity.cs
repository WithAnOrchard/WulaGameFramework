using EssSystem.Core.EssManagers.UIManager.Dao.CommonComponents;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.EssManagers.UIManager.Entity.CommonEntity
{
    /// <summary>
    ///     UI Entity
    /// </summary>
    public class UIButtonEntity : UIEntity
    {
        private Button _button;
        private Text _text;

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _button = gameObject.GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            _text = GetComponentInChildren<Text>() ?? CreateTextComponent();
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

            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;

            return text;
        }

        protected override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UIButtonComponent buttonDao)
            {
                if (_text != null) _text.text = buttonDao.Text;
                if (_button != null) _button.interactable = Dao.Interactable;
            }
        }

        private void OnButtonClick()
        {
            if (Dao is UIButtonComponent buttonDao) buttonDao.Click();
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