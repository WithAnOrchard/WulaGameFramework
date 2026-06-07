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
        }

        private Image EnsureImage()
        {
            _image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _image.raycastTarget = false; // 背景面板默认不拦截点击；需要交互的请用 UIButtonComponent
            return _image;
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UIPanelComponent panelDao)
            {
                if (!HasGraphic(panelDao))
                {
                    ClearGraphic();
                    return;
                }

                EnsureImage();
                _image.color = string.IsNullOrEmpty(panelDao.BackgroundSpriteId)
                    ? panelDao.BackgroundColor
                    : Color.clear;

                // 通过Event机制从ResourceManager加载Sprite
                if (!string.IsNullOrEmpty(panelDao.BackgroundSpriteId))
                {
                    LoadSpriteFromId(panelDao.BackgroundSpriteId, panelDao.BackgroundColor);
                }
            }
        }

        public override void OnDaoPropertyChanged(string propertyName, object value)
        {
            base.OnDaoPropertyChanged(propertyName, value);

            if (Dao is UIPanelComponent panelDao)
            {
                switch (propertyName)
                {
                    case "BackgroundColor":
                        if (!HasGraphic(panelDao))
                        {
                            ClearGraphic();
                            break;
                        }
                        EnsureImage();
                        _image.color = string.IsNullOrEmpty(panelDao.BackgroundSpriteId)
                            ? panelDao.BackgroundColor
                            : (_image.sprite != null ? panelDao.BackgroundColor : Color.clear);
                        break;

                    case "BackgroundSpriteId":
                        if (!string.IsNullOrEmpty(panelDao.BackgroundSpriteId))
                        {
                            EnsureImage();
                            _image.sprite = null;
                            _image.color = Color.clear;
                            LoadSpriteFromId(panelDao.BackgroundSpriteId, panelDao.BackgroundColor);
                        }
                        else if (_image != null)
                        {
                            _image.sprite = null;
                            if (!HasGraphic(panelDao))
                                ClearGraphic();
                        }
                        break;
                }
            }
        }

        private void LoadSpriteFromId(string spriteId, Color tint)
        {
            if (string.IsNullOrEmpty(spriteId) || _image == null) return;
            
            EventProcessor.Instance.TriggerEventMethod("GetSpriteAsync",
                new System.Collections.Generic.List<object> { spriteId });
                
            AsyncSpriteLoader.StartLoad(spriteId, _image, tint);
        }

        public Image GetImage()
        {
            return _image;
        }

        private void ClearGraphic()
        {
            _image = gameObject.GetComponent<Image>();
            if (_image == null) return;
            _image.sprite = null;
            _image.color = Color.clear;
            _image.raycastTarget = false;
        }

        private static bool HasGraphic(UIPanelComponent panel)
        {
            return panel != null
                   && (!string.IsNullOrEmpty(panel.BackgroundSpriteId) || panel.BackgroundColor.a > 0f);
        }
    }
}
