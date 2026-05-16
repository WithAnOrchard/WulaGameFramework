using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao;

namespace EssSystem.Core.Presentation.UIManager
{
    /// <summary>
    /// UI 管理器 — UGUI Canvas + EventSystem 自动建立，事件接口委托给 <see cref="UIService"/>。
    /// </summary>
    [Manager(5)]
    public class UIManager : Manager<UIManager>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_REGISTER_ENTITY      = "RegisterUIEntity";
        public const string EVT_GET_ENTITY           = "GetUIEntity";
        public const string EVT_UNREGISTER_ENTITY    = "UnregisterUIEntity";
        public const string EVT_HOT_RELOAD           = "HotReloadUIConfigs";
        /// <summary>获取 UI Canvas 根 Transform。data: 空。返回 Ok(Transform)。</summary>
        public const string EVT_GET_CANVAS_TRANSFORM = "GetUICanvasTransform";
        /// <summary>按 daoId 查 UI GameObject。data: [id]。返回 Ok(GameObject) / Fail。</summary>
        public const string EVT_GET_UI_GAMEOBJECT    = "GetUIGameObject";
        /// <summary>UIComponent 属性变更广播。data: [daoId, propName, value]，UIService 内部转发给对应 UIEntity。</summary>
        public const string EVT_DAO_PROPERTY_CHANGED = "UIDaoPropertyChanged";

        // ============================================================
        // Inspector 字段
        // ============================================================
        [Header("UI Canvas")]
        [SerializeField] private Canvas _uiCanvas;

        [Tooltip("CanvasScaler 参考分辨率，默认 1920×1080")]
        [SerializeField] private Vector2 _referenceResolution = new Vector2(1920, 1080);

        [Tooltip("CanvasScaler MatchWidthOrHeight：0=按宽，1=按高，0.5=折中")]
        [SerializeField, Range(0f, 1f)] private float _matchWidthOrHeight = 0.5f;

        // ============================================================
        // 生命周期
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            if (_uiCanvas == null) _uiCanvas = CreateCanvas();
            EnsureEventSystem();
            Log("UIManager 初始化完成", Color.green);
        }

        // ============================================================
        // Event API
        // ============================================================
        [Event(EVT_REGISTER_ENTITY)]
        public List<object> RegisterUIEntity(List<object> data)
        {
            if (!TryGetId(data, out var daoId)) return ResultCode.Fail("参数无效");
            var component = data.Count > 1 ? data[1] as UIComponent : null;
            if (component == null) return ResultCode.Fail("UIComponent 缺失");

            var entity = UIService.Instance.RegisterUIEntity(daoId, component, GetCanvasTransform());
            return entity != null ? ResultCode.Ok(daoId) : ResultCode.Fail("创建 UIEntity 失败");
        }

        [Event(EVT_GET_ENTITY)]
        public List<object> GetUIEntity(List<object> data)
        {
            if (!TryGetId(data, out var daoId)) return ResultCode.Fail("参数无效");
            var entity = UIService.Instance.GetUIEntity(daoId);
            return entity != null ? ResultCode.Ok(entity) : ResultCode.Fail("未找到实体");
        }

        [Event(EVT_UNREGISTER_ENTITY)]
        public List<object> UnregisterUIEntity(List<object> data)
        {
            if (!TryGetId(data, out var daoId)) return ResultCode.Fail("参数无效");
            UIService.Instance.DestroyUIEntity(daoId);
            return ResultCode.Ok();
        }

        [Event(EVT_GET_CANVAS_TRANSFORM)]
        public List<object> GetUICanvasTransform(List<object> data)
        {
            var t = GetCanvasTransform();
            return t != null ? ResultCode.Ok(t) : ResultCode.Fail("Canvas 未初始化");
        }

        [Event(EVT_GET_UI_GAMEOBJECT)]
        public List<object> GetUIGameObject(List<object> data)
        {
            if (!TryGetId(data, out var daoId)) return ResultCode.Fail("参数无效");
            var go = UIService.Instance.GetUIEntity(daoId)?.gameObject;
            return go != null ? ResultCode.Ok(go) : ResultCode.Fail($"UI GameObject 不存在: {daoId}");
        }

        /// <summary>UIComponent 广播属性变更，转发给对应 UIEntity。</summary>
        [Event(EVT_DAO_PROPERTY_CHANGED)]
        public List<object> OnDaoPropertyChanged(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效");
            if (!TryGetId(data, out var daoId)) return ResultCode.Fail("daoId 为空");

            var propName = data[1] as string;
            var value = data.Count > 2 ? data[2] : null;
            UIService.Instance.GetUIEntity(daoId)?.OnDaoPropertyChanged(propName, value);
            return ResultCode.Ok();
        }

        [Event(EVT_HOT_RELOAD)]
        public List<object> HotReloadUIConfigs(List<object> data)
        {
            var ok = UIService.Instance.HotReloadConfigs();
            Log(ok ? "UI 配置热重载成功" : "UI 配置热重载失败", ok ? Color.green : Color.red);
            return ok ? ResultCode.Ok() : ResultCode.Fail("热重载失败");
        }

        // ============================================================
        // 内部辅助
        // ============================================================
        /// <summary>取 data[0] 作为 string id；空/非法返回 false。</summary>
        private static bool TryGetId(List<object> data, out string id)
        {
            id = data != null && data.Count > 0 ? data[0] as string : null;
            return !string.IsNullOrEmpty(id);
        }

        /// <summary>UI Canvas 根 Transform —— 没有时回落到本组件 transform。</summary>
        private Transform GetCanvasTransform() => _uiCanvas != null ? _uiCanvas.transform : transform;

        /// <summary>启动时自动建立 Canvas（ScreenSpaceOverlay + ScaleWithScreenSize）。</summary>
        private Canvas CreateCanvas()
        {
            var go = new GameObject("UICanvas");
            go.transform.SetParent(transform);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            // 不开 pixelPerfect —— 业务用 2× 超采样实现高清文字（FontSize×2 + Scale 0.5）；
            // 强制像素对齐与非整数 Scale 冲突，反让超采样后的边缘出现毛刺。
            canvas.pixelPerfect = false;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = _referenceResolution;
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = _matchWidthOrHeight;

            go.AddComponent<GraphicRaycaster>();
            Log("UIManager 自动创建 Canvas", Color.green);
            return canvas;
        }

        /// <summary>确保场景里有 EventSystem —— 缺失会让所有 UGUI 失去输入响应。</summary>
        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Log("UIManager 自动创建 EventSystem", Color.green);
        }

        // ============================================================
        // Editor
        // ============================================================
        [ContextMenu("热重载 UI 配置")]
        private void EditorHotReloadUIConfigs() => UIService.Instance.HotReloadConfigs();
    }
}
