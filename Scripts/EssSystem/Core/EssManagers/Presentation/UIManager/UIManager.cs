using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao;
using EssSystem.Core;

namespace EssSystem.Core.EssManagers.Presentation.UIManager
{
    /// <summary>
    ///     UI管理器 - Unity MonoBehaviour单例，用于UI管理
    /// </summary>
    [Manager(5)]
    public class UIManager : Manager<UIManager>
    {
        // ─── Event 名常量（供调用方使用，避免魔法字符串）
        public const string EVT_REGISTER_ENTITY      = "RegisterUIEntity";
        public const string EVT_GET_ENTITY           = "GetUIEntity";
        public const string EVT_UNREGISTER_ENTITY    = "UnregisterUIEntity";
        public const string EVT_HOT_RELOAD           = "HotReloadUIConfigs";
        /// <summary>获取 UI Canvas 根 Transform。data: 空。返回 Ok(Transform).</summary>
        public const string EVT_GET_CANVAS_TRANSFORM = "GetUICanvasTransform";
        /// <summary>按 daoId 查 UI GameObject。data: [id]。返回 Ok(GameObject) / Fail。</summary>
        public const string EVT_GET_UI_GAMEOBJECT    = "GetUIGameObject";
        /// <summary>UIComponent 属性变更广播。data: [daoId, propName, value]。UIService 内部转发给对应 UIEntity。</summary>
        public const string EVT_DAO_PROPERTY_CHANGED = "UIDaoPropertyChanged";

        [Header("UI Canvas")]
        [SerializeField] private Canvas _uiCanvas;

        protected override void Initialize()
        {
            base.Initialize();

            // 如果Canvas不存在，创建一个
            if (_uiCanvas == null)
            {
                _uiCanvas = CreateCanvas();
                Log("UIManager 自动创建Canvas", Color.green);
            }

            // 确保场景里有 EventSystem，否则 UGUI 按钮全都收不到点击
            EnsureEventSystem();

            Log("UIManager 初始化完成", Color.green);
        }

        /// <summary>
        /// 确保场景里存在 EventSystem（缺一个 UGUI 全场不响应输入）。
        /// 已存在则不动；不存在则创建带 StandaloneInputModule 的新 GameObject。
        /// </summary>
        private void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Log("UIManager 自动创建 EventSystem (StandaloneInputModule)", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region Canvas

        /// <summary>
        /// 获取 Canvas Transform — 供 UIService 直接调用，替代反射
        /// </summary>
        public Transform GetCanvasTransform()
        {
            return _uiCanvas != null ? _uiCanvas.transform : transform;
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
            // 不开 pixelPerfect —— 业务层用 2× 超采样（FontSize×2 + Scale 0.5）实现高清文字，
            // 强制像素对齐会和非整数 Scale 冲突，反而让超采样后的文本边缘出现毛刺
            canvas.pixelPerfect = false;

            var canvasScaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            // MatchWidthOrHeight + 0.5：宽高比偏移时按 50% 取平均，避免极端宽屏/窄屏 UI 被裁掉或溢出
            canvasScaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            return canvas;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Event Handlers (外部通过 EventProcessor 调用，内部直接调用 UIService)

        /// <summary>
        ///     注册UI实体
        /// </summary>
        [Event(EVT_REGISTER_ENTITY)]
        public List<object> RegisterUIEntity(List<object> data)
        {
            var daoId = data[0] as string;
            var component = data[1] as UIComponent;

            if (string.IsNullOrEmpty(daoId) || component == null)
                return ResultCode.Fail("参数无效");

            var canvasTransform = GetCanvasTransform();
            var entity = UIService.Instance.RegisterUIEntity(daoId, component, canvasTransform);
            return entity != null ? ResultCode.Ok(daoId) : ResultCode.Fail("创建UIEntity失败");
        }

        /// <summary>
        ///     获取UI实体
        /// </summary>
        [Event(EVT_GET_ENTITY)]
        public List<object> GetUIEntity(List<object> data)
        {
            var daoId = data[0] as string;
            if (string.IsNullOrEmpty(daoId))
                return ResultCode.Fail("参数无效");

            var entity = UIService.Instance.GetUIEntity(daoId);
            return entity != null ? ResultCode.Ok(entity) : ResultCode.Fail("未找到实体");
        }

        /// <summary>
        ///     注销UI实体
        /// </summary>
        [Event(EVT_UNREGISTER_ENTITY)]
        public List<object> UnregisterUIEntity(List<object> data)
        {
            var daoId = data[0] as string;
            if (string.IsNullOrEmpty(daoId))
                return ResultCode.Fail("参数无效");

            UIService.Instance.DestroyUIEntity(daoId);
            return ResultCode.Ok();
        }

        /// <summary>返回 UI Canvas 根 Transform——供外部模块读取逻辑尺寸、父接临时元素等。</summary>
        [Event(EVT_GET_CANVAS_TRANSFORM)]
        public List<object> OnEventGetCanvasTransform(List<object> data)
        {
            var t = GetCanvasTransform();
            return t != null ? ResultCode.Ok(t) : ResultCode.Fail("Canvas 未初始化");
        }

        /// <summary>按 daoId 返回其运行时 GameObject（Unity 原生类型，不泄露 UIEntity 类型）。</summary>
        [Event(EVT_GET_UI_GAMEOBJECT)]
        public List<object> OnEventGetUIGameObject(List<object> data)
        {
            var daoId = data != null && data.Count > 0 ? data[0] as string : null;
            if (string.IsNullOrEmpty(daoId)) return ResultCode.Fail("参数无效");
            var entity = UIService.Instance.GetUIEntity(daoId);
            var go = entity != null ? entity.gameObject : null;
            return go != null ? ResultCode.Ok(go) : ResultCode.Fail($"UI GameObject 不存在: {daoId}");
        }

        /// <summary>UIComponent 广播属性变更。转发给 UIService 查到的 UIEntity。</summary>
        [Event(EVT_DAO_PROPERTY_CHANGED)]
        public List<object> OnEventDaoPropertyChanged(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效");
            var daoId    = data[0] as string;
            var propName = data[1] as string;
            var value    = data.Count > 2 ? data[2] : null;
            if (string.IsNullOrEmpty(daoId)) return ResultCode.Fail("daoId 为空");
            var entity = UIService.Instance.GetUIEntity(daoId);
            entity?.OnDaoPropertyChanged(propName, value);
            return ResultCode.Ok();
        }

        /// <summary>
        ///     热重载UI配置
        /// </summary>
        [Event(EVT_HOT_RELOAD)]
        public List<object> HotReloadUIConfigs(List<object> data)
        {
            var success = UIService.Instance.HotReloadConfigs();
            if (success)
                Log("UIManager UI配置热重载成功", Color.green);
            else
                Log("UIManager UI配置热重载失败", Color.red);

            return success ? ResultCode.Ok() : ResultCode.Fail("热重载失败");
        }

        #endregion

        #region Editor Methods

        /// <summary>
        /// 在Inspector中热重载UI配置
        /// </summary>
        [ContextMenu("热重载UI配置")]
        private void EditorHotReloadUIConfigs()
        {
            UIService.Instance.HotReloadConfigs();
        }

        #endregion
    }
}