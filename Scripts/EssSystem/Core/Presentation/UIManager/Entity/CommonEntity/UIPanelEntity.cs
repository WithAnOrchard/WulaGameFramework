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
    public class UIPanelEntity : UIEntity
    {
        private Image _image;

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _image.color = Color.clear;
            _image.raycastTarget = false; // 背景面板默认不拦截点击；需要交互的请用 UIButtonComponent
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UIPanelComponent panelDao && _image != null)
            {
                _image.color = panelDao.BackgroundColor;

                // 通过Event机制从ResourceManager加载Sprite
                if (!string.IsNullOrEmpty(panelDao.BackgroundSpriteId))
                {
                    LoadSpriteFromId(panelDao.BackgroundSpriteId);
                }
            }
        }

        public override void OnDaoPropertyChanged(string propertyName, object value)
        {
            base.OnDaoPropertyChanged(propertyName, value);

            if (Dao is UIPanelComponent panelDao && _image != null)
            {
                switch (propertyName)
                {
                    case "BackgroundColor":
                        _image.color = panelDao.BackgroundColor;
                        break;

                    case "BackgroundSpriteId":
                        if (!string.IsNullOrEmpty(panelDao.BackgroundSpriteId))
                        {
                            LoadSpriteFromId(panelDao.BackgroundSpriteId);
                        }
                        else
                        {
                            _image.sprite = null;
                        }
                        break;
                }
            }
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

        public Image GetImage()
        {
            return _image;
        }
    }
}