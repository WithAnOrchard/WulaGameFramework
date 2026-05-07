using System;
using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.EssManagers.Presentation.UIManager.Entity
{
    /// <summary>
    ///     UI Entity - GameObjectDao
    /// </summary>
    public abstract class UIEntity : MonoBehaviour
    {
        /// <summary>
        ///     Dao
        /// </summary>
        [SerializeField] [HideInInspector] private UIComponent _dao;

        /// <summary>
        ///     Dao
        /// </summary>
        public UIComponent Dao
        {
            get => _dao;
            set => SetDao(value);
        }

        /// <summary>
        ///     Dao ID
        /// </summary>
        public string DaoId => _dao?.Id;

        /// <summary>
        ///     Dao
        /// </summary>
        public UIType DaoType => _dao?.Type ?? UIType.Button;

        protected virtual void Awake()
        {
            // 注意：Dao 在 Awake 之后才由 Factory 通过 entity.Dao = dao 赋值，
            // 这里不能注册（_dao 为 null）。RegisterEntity 改在 SetDao 里调用。
        }

        protected virtual void OnDestroy()
        {
            UnregisterEntity();
        }

        /// <summary>
        ///     Dao
        /// </summary>
        protected virtual void SetDao(UIComponent dao)
        {
            if (_dao == dao) return;
            UnregisterEntity();
            _dao = dao;
            if (_dao != null)
            {
                RegisterEntity();
                SyncFromDao();
            }
        }

        /// <summary>
        ///     Dao
        /// </summary>
        public virtual void SyncFromDao()
        {
            if (_dao == null) return;
            gameObject.name = _dao.Name ?? _dao.Id;
            gameObject.SetActive(_dao.Visible);

            // Sync RectTransform properties
            if (TryGetComponent<RectTransform>(out var rectTransform)) _dao.ApplyToRectTransform(rectTransform);

            // Sync interactability for UI components
            SyncInteractability();
        }

        /// <summary>
        ///     Sync interactability based on component type
        /// </summary>
        protected virtual void SyncInteractability()
        {
            if (_dao == null) return;

            // Handle Button interactability
            if (_dao.IsButton())
            {
                var button = GetComponent<Button>();
                if (button != null) button.interactable = _dao.Interactable;
            }

            // Handle CanvasGroup for general interactability
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.interactable = _dao.Interactable;
                canvasGroup.blocksRaycasts = _dao.Interactable;
            }
        }

        /// <summary>
        ///     Called when Dao property changes
        ///     Dao
        /// </summary>
        /// <param name="propertyName">Property name</param>
        /// <param name="value">New value</param>
        public virtual void OnDaoPropertyChanged(string propertyName, object value)
        {
            if (_dao == null) return;

            switch (propertyName)
            {
                case "Name":
                    gameObject.name = _dao.Name ?? _dao.Id;
                    break;

                case "Visible":
                    gameObject.SetActive(_dao.Visible);
                    break;

                case "Position":
                case "Size":
                case "Scale":
                    if (TryGetComponent<RectTransform>(out var rectTransform)) _dao.ApplyToRectTransform(rectTransform);
                    break;

                case "Interactable":
                    SyncInteractability();
                    break;

                case "BackgroundColor":
                    SyncBackgroundColor((Color)value);
                    break;

                case "Text":
                    SyncText((string)value);
                    break;

                case "FontSize":
                    SyncFontSize((int)value);
                    break;

                case "Color":
                    SyncTextColor((Color)value);
                    break;

                case "Alignment":
                    SyncTextAlignment((TextAnchor)value);
                    break;
            }
        }

        protected virtual void SyncBackgroundColor(Color color)
        {
            if (_dao?.IsPanel() == true)
            {
                var image = GetComponent<Image>();
                if (image != null) image.color = color;
            }
        }

        protected virtual void SyncText(string text)
        {
            if (_dao?.IsButton() == true || _dao?.IsText() == true)
            {
                var unityText = GetComponent<Text>();
                var tmpText = GetComponent<TextMeshProUGUI>();
                if (unityText != null) unityText.text = text ?? string.Empty;
                if (tmpText != null) tmpText.text = text ?? string.Empty;
            }
        }

        protected virtual void SyncFontSize(int fontSize)
        {
            if (_dao?.IsText() == true)
            {
                var text = GetComponent<Text>();
                var tmp = GetComponent<TextMeshProUGUI>();
                if (text != null) text.fontSize = fontSize;
                if (tmp != null) tmp.fontSize = fontSize;
            }
        }

        protected virtual void SyncTextColor(Color color)
        {
            if (_dao?.IsText() == true)
            {
                var text = GetComponent<Text>();
                var tmp = GetComponent<TextMeshProUGUI>();
                if (text != null) text.color = color;
                if (tmp != null) tmp.color = color;
            }
        }

        protected virtual void SyncTextAlignment(TextAnchor alignment)
        {
            if (_dao?.IsText() == true)
            {
                var text = GetComponent<Text>();
                var tmp = GetComponent<TextMeshProUGUI>();
                if (text != null) text.alignment = alignment;
                if (tmp != null) tmp.alignment = ConvertToTextMeshProAlignment(alignment);
            }
        }

        private TextAlignmentOptions ConvertToTextMeshProAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.Right,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }

        private void RegisterEntity()
        {
            if (_dao != null && !string.IsNullOrEmpty(_dao.Id)) UIService.Instance.RegisterUIEntity(_dao.Id, this);
        }

        private void UnregisterEntity()
        {
            if (_dao != null && !string.IsNullOrEmpty(_dao.Id)) UIService.Instance.UnregisterUIEntity(_dao.Id);
        }

        /// <summary>
        ///     DaoEntity
        /// </summary>
        public static UIEntity GetEntity(UIComponent dao)
        {
            if (dao == null || string.IsNullOrEmpty(dao.Id)) return null;
            return GetEntityById(dao.Id);
        }

        /// <summary>
        ///     IDEntity
        /// </summary>
        public static UIEntity GetEntityById(string daoId)
        {
            return UIService.Instance.GetUIEntity(daoId);
        }

        // [REMOVED] GetEntityByIdViaEvent -- caught by agent_lint [6]:
        //   referenced non-existent event "GetServiceDataById" (DataManager
        //   never declared this [Event]) and had zero callers project-wide.
        //   For external lookup use GetEntityById or UIService.Instance directly.

        /// <summary>
        ///     DaoGameObject
        /// </summary>
        public static GameObject GetGameObject(UIComponent dao)
        {
            var entity = GetEntity(dao);
            return entity?.gameObject;
        }

        /// <summary>
        ///     IDGameObject
        /// </summary>
        public static GameObject GetGameObjectById(string daoId)
        {
            var entity = GetEntityById(daoId);
            return entity?.gameObject;
        }

        /// <summary>
        ///     GameObjectDao
        /// </summary>
        public static UIComponent GetDao(GameObject gameObject)
        {
            if (gameObject == null) return null;
            var entity = gameObject.GetComponent<UIEntity>();
            return entity?.Dao;
        }

        /// <summary>
        ///     EntityDao
        /// </summary>
        public static UIComponent GetDao(UIEntity entity)
        {
            return entity?.Dao;
        }
    }
}