using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.UIManager.Entity.CommonEntity
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

            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 14;
            _text.color = Color.black;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.raycastTarget = false; // 装饰性文字默认不拦截点击，避免遮挡父按钮
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

        public override void OnDaoPropertyChanged(string propertyName, object value)
        {
            base.OnDaoPropertyChanged(propertyName, value);
            if (propertyName == "Text" && _text != null)
                _text.text = value as string ?? "";
        }

        public Text GetText()
        {
            return _text;
        }
    }
}