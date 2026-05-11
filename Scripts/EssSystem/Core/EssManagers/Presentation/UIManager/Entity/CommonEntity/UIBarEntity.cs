using EssSystem.Core;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Event;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.EssManagers.Presentation.UIManager.Entity.CommonEntity
{
    public class UIBarEntity : UIEntity
    {
        private Image _backgroundImage;
        private Image _fillImage;
        private RectTransform _fillRect;

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _backgroundImage = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _backgroundImage.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(transform, false);
            _fillRect = fillGo.AddComponent<RectTransform>();
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;

            _fillImage = fillGo.AddComponent<Image>();
            _fillImage.raycastTarget = false;
        }

        public override void SyncFromDao()
        {
            base.SyncFromDao();
            SyncBar();
        }

        public override void OnDaoPropertyChanged(string propertyName, object value)
        {
            base.OnDaoPropertyChanged(propertyName, value);
            switch (propertyName)
            {
                case "Value":
                case "Range":
                case "Percent":
                case "Size":
                    SyncFill();
                    break;
                case "BackgroundColor":
                case "FillColor":
                    SyncColors();
                    break;
                case "BackgroundSpriteId":
                case "FillSpriteId":
                    SyncSprites();
                    break;
            }
        }

        private void SyncBar()
        {
            SyncColors();
            SyncSprites();
            SyncFill();
        }

        private void SyncColors()
        {
            if (Dao is not UIBarComponent barDao) return;
            if (_backgroundImage != null) _backgroundImage.color = barDao.BackgroundColor;
            if (_fillImage != null) _fillImage.color = barDao.FillColor;
        }

        private void SyncSprites()
        {
            if (Dao is not UIBarComponent barDao) return;
            ApplySprite(barDao.BackgroundSpriteId, _backgroundImage);
            ApplySprite(barDao.FillSpriteId, _fillImage);
        }

        private void SyncFill()
        {
            if (Dao is not UIBarComponent barDao || _fillRect == null) return;
            if (!TryGetComponent<RectTransform>(out var rect)) return;

            var padding = barDao.FillPadding;
            var availableWidth = Mathf.Max(0f, rect.rect.width - padding.x * 2f);
            var availableHeight = Mathf.Max(0f, rect.rect.height - padding.y * 2f);
            _fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, availableWidth * barDao.Percent);
            _fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, availableHeight);
            _fillRect.anchoredPosition = new Vector2(padding.x, 0f);
        }

        private static void ApplySprite(string spriteId, Image image)
        {
            if (image == null) return;
            if (string.IsNullOrEmpty(spriteId))
            {
                image.sprite = null;
                return;
            }

            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetResource",
                new System.Collections.Generic.List<object> { spriteId, "Sprite", false });
            if (!ResultCode.IsOk(result) || result.Count < 2) return;
            if (result[1] is Sprite sprite) image.sprite = sprite;
        }
    }
}
