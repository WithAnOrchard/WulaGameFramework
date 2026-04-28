using EssSystem.Core.EssManagers.UIManager.Dao.CommonComponents;
using EssSystem.Core.EssManagers.UIManager.Entity;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.EssManagers.UIManager.Entity
{
    /// <summary>
    ///     UI Entity
    /// </summary>
    public class UITextEntity : UIEntity
    {
        private Text _text;

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _text = gameObject.GetComponent<Text>() ?? gameObject.AddComponent<Text>();

            var rectTransform = _text.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.fontSize = 14;
            _text.color = Color.black;
            _text.alignment = TextAnchor.MiddleCenter;
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UITextComponent textDao && _text != null)
            {
                _text.text = textDao.Text;
                _text.fontSize = textDao.FontSize;
                _text.color = textDao.Color;
                _text.alignment = textDao.Alignment;
            }
        }

        public Text GetText()
        {
            return _text;
        }
    }
}