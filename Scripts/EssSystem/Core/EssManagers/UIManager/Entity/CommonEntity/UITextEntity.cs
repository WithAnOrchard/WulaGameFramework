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

            // 使用中心锚点 + 中心 pivot，使 Adjustable.ApplyToRectTransform 中的
            // sizeDelta / anchoredPosition 表达绝对宽高与位置（与 Panel/Button 一致）。
            // 之前的 stretch 锚点会让 DAO 的 Size/Position 失效。
            var rectTransform = _text.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot     = new Vector2(0.5f, 0.5f);

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

        public Text GetText()
        {
            return _text;
        }
    }
}