using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.Core.EssManagers.UIManager
{
    /// <summary>
    ///     UI管理器 - Unity MonoBehaviour单例，用于UI管理
    /// </summary>
    [Manager(5)]
    public class UIManager : Manager<UIManager>
    {
        [Header("UI Canvas")]
        [SerializeField] private Canvas _uiCanvas;

 

        private readonly Dictionary<string, GameObject> _inventoryUIs = new Dictionary<string, GameObject>();

        protected override void Initialize()
        {
            base.Initialize();

            // 如果Canvas不存在，创建一个
            if (_uiCanvas == null)
            {
                _uiCanvas = CreateCanvas();
                Log("UIManager 自动创建Canvas", UnityEngine.Color.green);
            }

            Log("UIManager 初始化完成", UnityEngine.Color.green);
        }

        /// <summary>
        /// 创建Canvas
        /// </summary>
        private Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("UICanvas");
            canvasObject.transform.SetParent(transform);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var canvasScaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);

            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            return canvas;
        }
  

        /// <summary>
        ///     注册UI实体
        /// </summary>
        [Event("RegisterUIEntity")]
        public System.Collections.Generic.List<object> RegisterUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;
            var component = data[1] as Dao.UIComponent;

            if (string.IsNullOrEmpty(daoId) || component == null)
            {
                Log($"UIManager.RegisterUIEntity 参数无效: daoId={daoId}, component={(component != null)}", UnityEngine.Color.red);
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            Log($"UIManager.RegisterUIEntity 调用ServiceRegisterUIEntity: {daoId}", UnityEngine.Color.yellow);

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceRegisterUIEntity",
                new System.Collections.Generic.List<object> { daoId, component });

            Log($"UIManager.RegisterUIEntity ServiceRegisterUIEntity返回: {(result != null ? result[0] : "null")}", UnityEngine.Color.yellow);

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }

        /// <summary>
        ///     获取UI实体
        /// </summary>
        [Event("GetUIEntity")]
        public System.Collections.Generic.List<object> GetUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;

            if (string.IsNullOrEmpty(daoId))
            {
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceGetUIEntity",
                new System.Collections.Generic.List<object> { daoId });

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }

        /// <summary>
        ///     注销UI实体
        /// </summary>
        [Event("UnregisterUIEntity")]
        public System.Collections.Generic.List<object> UnregisterUIEntity(System.Collections.Generic.List<object> data)
        {
            string daoId = data[0] as string;

            if (string.IsNullOrEmpty(daoId))
            {
                Log($"UIManager.UnregisterUIEntity 参数无效: daoId={daoId}", UnityEngine.Color.red);
                return new System.Collections.Generic.List<object> { "参数无效" };
            }

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceUnregisterUIEntity",
                new System.Collections.Generic.List<object> { daoId });

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }

        /// <summary>
        ///     热重载UI配置
        /// </summary>
        [Event("HotReloadUIConfigs")]
        public System.Collections.Generic.List<object> HotReloadUIConfigs(System.Collections.Generic.List<object> data)
        {
            Log("UIManager 开始热重载UI配置", UnityEngine.Color.yellow);

            // 通过 EventProcessor 调用本地 Service
            var result = EventProcessor.Instance.TriggerEventMethod("ServiceHotReloadUIConfigs",
                new System.Collections.Generic.List<object> { });

            if (result != null && result.Count >= 1 && result[0].ToString() == "成功")
            {
                Log("UIManager UI配置热重载成功", UnityEngine.Color.green);
            }
            else
            {
                Log($"UIManager UI配置热重载失败: {(result != null ? result[0] : "null")}", UnityEngine.Color.red);
            }

            return result ?? new System.Collections.Generic.List<object> { "调用失败" };
        }

        private static List<object> Ok(object data) => new List<object> { "成功", data };
        private static List<object> Fail(string msg) => new List<object> { "错误", msg };

        #region Editor Methods

        /// <summary>
        /// 在Inspector中热重载UI配置
        /// </summary>
        [ContextMenu("热重载UI配置")]
        private void EditorHotReloadUIConfigs()
        {
            HotReloadUIConfigs(new System.Collections.Generic.List<object> { });
        }

        #endregion
    }
}