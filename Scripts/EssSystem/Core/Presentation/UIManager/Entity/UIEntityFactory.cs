using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Dao;
using EssSystem.Core.Presentation.UIManager.Entity.CommonEntity;
using EssSystem.Core.Base.Event;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.UIManager.Entity
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
                UIType.Bar => gameObject.AddComponent<UIBarEntity>(),
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

                    // 如果ButtonSpriteId不为空，通过Event机制加载Sprite
                    if (dao is UIButtonComponent buttonComponent && !string.IsNullOrEmpty(buttonComponent.ButtonSpriteId))
                    {
                        LoadSpriteFromId(buttonComponent.ButtonSpriteId, image);
                    }

                    var textObject = new GameObject("Text");
                    textObject.transform.SetParent(gameObject.transform);
                    var textRect = textObject.AddComponent<RectTransform>();
                    // 让子 Text 贴满按钮（stretch）—— 零尺寸 rect 会让 uGUI Text 渲染异常
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = Vector2.zero;
                    textRect.offsetMax = Vector2.zero;
                    textRect.pivot = new Vector2(0.5f, 0.5f);

                    var text = textObject.AddComponent<Text>();
                    text.text = dao is UIButtonComponent buttonDao ? buttonDao.Text : "Button";
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 14;
                    text.color = Color.black;
                    text.alignment = TextAnchor.MiddleCenter;
                    break;

                case UIType.Panel:
                    // Panel必须先添加CanvasGroup，因为其他UI操作可能依赖它
                    var canvasGroup = gameObject.AddComponent<CanvasGroup>();
                    canvasGroup.alpha = dao.Visible ? 1f : 0f;
                    canvasGroup.interactable = canvasGroup.blocksRaycasts = dao.Interactable;

                    var panelImage = gameObject.AddComponent<Image>();
                    panelImage.color = dao is UIPanelComponent panelDao ? panelDao.BackgroundColor : Color.white;
                    // 如果BackgroundSpriteId不为空，通过Event机制加载Sprite
                    if (dao is UIPanelComponent panelComponent && !string.IsNullOrEmpty(panelComponent.BackgroundSpriteId))
                    {
                        LoadSpriteFromId(panelComponent.BackgroundSpriteId, panelImage);
                    }
                    break;

                case UIType.Text:
                    var textComponent = gameObject.AddComponent<Text>();
                    textComponent.text = dao is UITextComponent textDao ? textDao.Text : "Text";
                    textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    textComponent.fontSize = 14;
                    textComponent.color = Color.black;
                    textComponent.alignment = TextAnchor.MiddleCenter;
                    break;
                case UIType.Bar:
                    var barImage = gameObject.AddComponent<Image>();
                    barImage.color = dao is UIBarComponent barDao ? barDao.BackgroundColor : Color.black;
                    barImage.raycastTarget = false;
                    break;
            }
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

        private static void LoadSpriteFromId(string spriteId, Image targetImage)
        {
            try
            {
                

                var result = EventProcessor.Instance.TriggerEventMethod("GetResource",
                    new System.Collections.Generic.List<object> { spriteId, "Sprite", false });

                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                {
                    var sprite = result[1] as Sprite;
                    if (sprite != null && targetImage != null)
                    {
                        targetImage.sprite = sprite;
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"加载Sprite失败: {spriteId}, 错误: {ex.Message}");
            }
        }
    }
}