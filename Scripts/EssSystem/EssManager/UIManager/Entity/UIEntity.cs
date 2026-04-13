using EssSystem.UIManager.Dao;
using UnityEngine;

namespace EssSystem.EssManager.UIManager.Entity
{
    /// <summary>
    /// UI Entity - GameObjectDao
    /// </summary>
    public abstract class UIEntity : MonoBehaviour
    {
        /// <summary>
        /// Dao
        /// </summary>
        [SerializeField, HideInInspector]
        private UIComponent _dao;

        /// <summary>
        /// Dao
        /// </summary>
        public UIComponent Dao
        {
            get => _dao;
            set => SetDao(value);
        }

        /// <summary>
        /// Dao ID
        /// </summary>
        public string DaoId => _dao?.Id;

        /// <summary>
        /// Dao
        /// </summary>
        public UIType DaoType => _dao?.Type ?? UIType.Button;

        protected virtual void Awake()
        {
            RegisterEntity();
        }

        protected virtual void OnDestroy()
        {
            UnregisterEntity();
        }

        /// <summary>
        /// Dao
        /// </summary>
        protected virtual void SetDao(UIComponent dao)
        {
            if (_dao == dao) return;
            _dao = dao;
            if (_dao != null)
            {
                SyncFromDao();
            }
        }

        /// <summary>
        /// Dao
        /// </summary>
        protected virtual void SyncFromDao()
        {
            if (_dao == null) return;
            gameObject.name = _dao.Name ?? _dao.Id;
            gameObject.SetActive(_dao.Visible);
            
            // Sync RectTransform properties
            if (TryGetComponent<RectTransform>(out var rectTransform))
            {
                _dao.ApplyToRectTransform(rectTransform);
            }
            
            // Sync interactability for UI components
            SyncInteractability();
        }

        /// <summary>
        /// Sync interactability based on component type
        ///  
        /// </summary>
        protected virtual void SyncInteractability()
        {
            if (_dao == null) return;

            // Handle Button interactability
            if (_dao.IsButton())
            {
                var button = GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    button.interactable = _dao.Interactable;
                }
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
        /// Called when Dao property changes
        ///  Dao 
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
                    if (TryGetComponent<RectTransform>(out var rectTransform))
                    {
                        _dao.ApplyToRectTransform(rectTransform);
                    }
                    break;
                    
                case "Interactable":
                    SyncInteractability();
                    break;
                    
                case "BackgroundColor":
                    SyncBackgroundColor((UnityEngine.Color)value);
                    break;
                    
                case "Text":
                    SyncText((string)value);
                    break;
                    
                case "FontSize":
                    SyncFontSize((int)value);
                    break;
                    
                case "Color":
                    SyncTextColor((UnityEngine.Color)value);
                    break;
                    
                case "Alignment":
                    SyncTextAlignment((UnityEngine.TextAnchor)value);
                    break;
            }
        }

        protected virtual void SyncBackgroundColor(UnityEngine.Color color)
        {
            if (_dao?.IsPanel() == true)
            {
                var image = GetComponent<UnityEngine.UI.Image>();
                if (image != null) image.color = color;
            }
        }

        protected virtual void SyncText(string text)
        {
            if (_dao?.IsButton() == true || _dao?.IsText() == true)
            {
                var unityText = GetComponent<UnityEngine.UI.Text>();
                var tmpText = GetComponent<TMPro.TextMeshProUGUI>();
                if (unityText != null) unityText.text = text ?? string.Empty;
                if (tmpText != null) tmpText.text = text ?? string.Empty;
            }
        }

        protected virtual void SyncFontSize(int fontSize)
        {
            if (_dao?.IsText() == true)
            {
                var text = GetComponent<UnityEngine.UI.Text>();
                var tmp = GetComponent<TMPro.TextMeshProUGUI>();
                if (text != null) text.fontSize = fontSize;
                if (tmp != null) tmp.fontSize = fontSize;
            }
        }

        protected virtual void SyncTextColor(UnityEngine.Color color)
        {
            if (_dao?.IsText() == true)
            {
                var text = GetComponent<UnityEngine.UI.Text>();
                var tmp = GetComponent<TMPro.TextMeshProUGUI>();
                if (text != null) text.color = color;
                if (tmp != null) tmp.color = color;
            }
        }

        protected virtual void SyncTextAlignment(UnityEngine.TextAnchor alignment)
        {
            if (_dao?.IsText() == true)
            {
                var text = GetComponent<UnityEngine.UI.Text>();
                var tmp = GetComponent<TMPro.TextMeshProUGUI>();
                if (text != null) text.alignment = alignment;
                if (tmp != null) tmp.alignment = ConvertToTextMeshProAlignment(alignment);
            }
        }

        private TMPro.TextAlignmentOptions ConvertToTextMeshProAlignment(UnityEngine.TextAnchor alignment) => alignment switch
        {
            UnityEngine.TextAnchor.UpperLeft => TMPro.TextAlignmentOptions.TopLeft,
            UnityEngine.TextAnchor.UpperCenter => TMPro.TextAlignmentOptions.Top,
            UnityEngine.TextAnchor.UpperRight => TMPro.TextAlignmentOptions.TopRight,
            UnityEngine.TextAnchor.MiddleLeft => TMPro.TextAlignmentOptions.Left,
            UnityEngine.TextAnchor.MiddleCenter => TMPro.TextAlignmentOptions.Center,
            UnityEngine.TextAnchor.MiddleRight => TMPro.TextAlignmentOptions.Right,
            UnityEngine.TextAnchor.LowerLeft => TMPro.TextAlignmentOptions.BottomLeft,
            UnityEngine.TextAnchor.LowerCenter => TMPro.TextAlignmentOptions.Bottom,
            UnityEngine.TextAnchor.LowerRight => TMPro.TextAlignmentOptions.BottomRight,
            _ => TMPro.TextAlignmentOptions.Center
        };

        private void RegisterEntity()
        {
            if (_dao != null && !string.IsNullOrEmpty(_dao.Id))
            {
                UIService.Instance.RegisterUIEntity(_dao.Id, this);
            }
        }

        private void UnregisterEntity()
        {
            if (_dao != null && !string.IsNullOrEmpty(_dao.Id))
            {
                UIService.Instance.UnregisterUIEntity(_dao.Id);
            }
        }

        /// <summary>
        /// DaoEntity
        /// </summary>
        public static UIEntity GetEntity(UIComponent dao)
        {
            if (dao == null || string.IsNullOrEmpty(dao.Id)) return null;
            return GetEntityById(dao.Id);
        }

        /// <summary>
        /// IDEntity
        /// </summary>
        public static UIEntity GetEntityById(string daoId)
        {
            return UIService.Instance.GetUIEntity(daoId);
        }

        /// <summary>
        /// 通过DataManager事件获取Entity（用于外部系统调用）
        /// </summary>
        public static UIEntity GetEntityByIdViaEvent(string daoId)
        {
            try
            {
                var eventManager = EssSystem.Core.Event.EventManager.Instance;
                var result = eventManager.TriggerEvent("GetServiceDataById", new System.Collections.Generic.List<object> 
                { 
                    "UIService", 
                    "UIEntities", 
                    daoId 
                });
                
                if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
                {
                    return result[1] as UIEntity;
                }
                
                return null;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"通过Event获取UIEntity失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// DaoGameObject
        /// </summary>
        public static GameObject GetGameObject(UIComponent dao)
        {
            var entity = GetEntity(dao);
            return entity?.gameObject;
        }

        /// <summary>
        /// IDGameObject
        /// </summary>
        public static GameObject GetGameObjectById(string daoId)
        {
            var entity = GetEntityById(daoId);
            return entity?.gameObject;
        }

        /// <summary>
        /// GameObjectDao
        /// </summary>
        public static UIComponent GetDao(GameObject gameObject)
        {
            if (gameObject == null) return null;
            var entity = gameObject.GetComponent<UIEntity>();
            return entity?.Dao;
        }

        /// <summary>
        /// EntityDao
        /// </summary>
        public static UIComponent GetDao(UIEntity entity)
        {
            return entity?.Dao;
        }
    }
}
