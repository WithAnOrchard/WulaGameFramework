using EssSystem.Core.EssManagers.UIManager.Dao.CommonComponents;
using EssSystem.Core.EssManagers.UIManager.Entity;
using EssSystem.Core.EssManagers.UIManager.Dao;
using EssSystem.Core.EssManagers.UIManager.Entity.CommonEntity;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.EssManagers.UIManager.Entity
{
    /// <summary>
    ///     UI Entity
    /// </summary>
    public static class UIEntityFactory
    {
        /// <summary>
        ///     UI Entity
        /// </summary>
        public static UIEntity CreateEntity(UIComponent dao, Transform parent = null)
        {
            if (dao == null) return null;

            var gameObject = new GameObject(dao.Name ?? dao.Id);
            gameObject.transform.SetParent(parent);

            var rectTransform = gameObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);

            dao.ApplyToRectTransform(rectTransform);

            // Add appropriate Unity UI components based on Dao type
            AddUnityUIComponents(gameObject, dao);

            var entity = dao.Type switch
            {
                UIType.Button => gameObject.AddComponent<UIButtonEntity>(),
                UIType.Panel => gameObject.AddComponent<UIPanelEntity>(),
                UIType.Text => gameObject.AddComponent<UITextEntity>(),
                _ => gameObject.AddComponent<UIEntity>()
            };

            entity.Dao = dao;
            return entity;
        }

        private static void AddUnityUIComponents(GameObject gameObject, UIComponent dao)
        {
            switch (dao.Type)
            {
                case UIType.Button:
                    var image = gameObject.AddComponent<Image>();
                    image.color = Color.white;
                    var button = gameObject.AddComponent<Button>();
                    button.targetGraphic = image;

                    var textObject = new GameObject("Text");
                    textObject.transform.SetParent(gameObject.transform);
                    var textRect = textObject.AddComponent<RectTransform>();
                    textRect.anchorMin = textRect.anchorMax = Vector2.zero;
                    textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                    textRect.pivot = new Vector2(0.5f, 0.5f);

                    var text = textObject.AddComponent<Text>();
                    text.text = dao is UIButtonComponent buttonDao ? buttonDao.Text : "Button";
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = 14;
                    text.color = Color.black;
                    text.alignment = TextAnchor.MiddleCenter;
                    break;

                case UIType.Panel:
                    var panelImage = gameObject.AddComponent<Image>();
                    panelImage.color = dao is UIPanelComponent panelDao ? panelDao.BackgroundColor : Color.clear;
                    break;

                case UIType.Text:
                    var textComponent = gameObject.AddComponent<Text>();
                    textComponent.text = dao is UITextComponent textDao ? textDao.Text : "Text";
                    textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    textComponent.fontSize = 14;
                    textComponent.color = Color.black;
                    textComponent.alignment = TextAnchor.MiddleCenter;
                    break;
            }

            var canvasGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = dao.Visible ? 1f : 0f;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = dao.Interactable;
        }

        /// <summary>
        ///     GameObject
        /// </summary>
        public static GameObject CreateGameObject(UIComponent dao, Transform parent = null)
        {
            var entity = CreateEntity(dao, parent);
            return entity?.gameObject;
        }

        /// <summary>
        /// </summary>
        public static UIEntity CreateHierarchy(UIComponent rootDao, Transform parent = null)
        {
            if (rootDao == null) return null;

            var rootEntity = CreateEntity(rootDao, parent);
            if (rootEntity == null) return null;

            CreateChildrenRecursive(rootDao, rootEntity);
            return rootEntity;
        }

        private static void CreateChildrenRecursive(UIComponent parentDao, UIEntity parentEntity)
        {
            foreach (var childDao in parentDao.GetChildren())
            {
                var childEntity = CreateEntity(childDao, parentEntity.transform);
                if (childEntity != null) CreateChildrenRecursive(childDao, childEntity);
            }
        }
    }
}