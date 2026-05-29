using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EssSystem.Core.Presentation.UIManager;

namespace EssSystem.Core.Presentation.UIManager.Entity
{
    /// <summary>
    /// 仿原生 Windows 窗口交互行为：
    /// <list type="bullet">
    /// <item>内部区域拖拽：按住面板主体移动整个面板</item>
    /// <item>四边 / 四角边缘拖拽：同原生窗口一样拉伸调整尺寸</item>
    /// <item>滚轮缩放：悬停时滚动 ± ScaleStep，范围 ScaleMin–ScaleMax</item>
    /// <item>双击复位：快速双击将 Scale 恢复为 1</item>
    /// <item>系统光标：Windows Standalone 下自动切换到对应方向箭头（↔↕↗↙↖↘）</item>
    /// </list>
    /// 通过 <c>UIManager.EVT_ADD_WINDOW_BEHAVIOR</c> 事件添加到已注册面板，
    /// 也可直接 <c>go.AddComponent&lt;UIWindowBehavior&gt;()</c>。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIWindowBehavior : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IEndDragHandler,
        IScrollHandler, IPointerClickHandler, IPointerExitHandler
    {
        // ── 公开参数 ──────────────────────────────────────────────────────────
        [Header("General")]
        [Tooltip("操作目标（留空 = 本 GameObject 的 RectTransform）")]
        public RectTransform Target;

        [Tooltip("是否允许拖拽面板主体来移动整个面板（默认关闭，仅边缘缩放）")]
        public bool AllowBodyDrag = false;

        [Tooltip("窗口最小尺寸（canvas 像素）")]
        public Vector2 MinSize = new Vector2(120f, 60f);

        [Header("Resize Edges")]
        [Tooltip("边缘热区宽度（canvas 像素，默认 20）")]
        public float EdgeThreshold = 20f;

        [Header("Top Bar")]
        [Tooltip("启用 TopBar：TopBar 区域内拖拽可移动窗口；TopBar 顶部边缘拖拽可调整窗口高度。\n关闭后 TopBar 区域无任何行为。")]
        public bool EnableTopBar = true;

        [Tooltip("TopBar 高度（canvas 像素）。EdgeThreshold 以内的顶部区域为高度调整热区，其余为拖拽移动区。")]
        public float TitleBarHeight = 44f;

        [Header("Scroll Zoom")]
        [Tooltip("滚轮每步缩放量（默认 0.10 = 10%）")]
        public float ScaleStep = 0.10f;

        [Tooltip("缩放下限")]
        public float ScaleMin = 0.5f;

        [Tooltip("缩放上限")]
        public float ScaleMax = 2.0f;

        // ── 内部枚举 ──────────────────────────────────────────────────────────
        private enum ResizeEdge
        {
            None,
            Left, Right, Top, Bottom,
            TopLeft, TopRight, BottomLeft, BottomRight,
            TitleBar   // 标题栏中段：拖拽移动整个窗口
        }

        // ── 内部状态 ──────────────────────────────────────────────────────────
        private RectTransform _rt;
        private Canvas        _canvas;
        private ResizeEdge    _activeEdge;
        private Vector2       _downCanvasPos;
        private Vector2       _startSize;
        private Vector2       _startAnchoredPos;
        private bool          _isResizing;
        private ResizeEdge    _lastReported = (ResizeEdge)(-1);
        private float         _lastClickTime;
        private const float   DOUBLE_CLICK_SEC = 0.3f;
        private int           _instanceId; // 用于 UICursorManager 注册
        private static int    s_nextId;
        private Vector2       _origSize;   // 初始尺寸，用于双击复位和缩放限位

        // ── 子树快照（布局完成后缓存，用于等比缩放和复位） ────────────────────
        private struct ChildSnap
        {
            public RectTransform rt;
            public Vector2       origPos;
            public Vector2       origSize;
            public Text          text;
            public int           origFontSize;
        }
        private List<ChildSnap> _snaps;
        private bool            _snapsCached;

        // ── 生命周期 ──────────────────────────────────────────────────────────
        private void Awake()
        {
            _rt         = Target != null ? Target : GetComponent<RectTransform>();
            _canvas     = GetComponentInParent<Canvas>();
            _instanceId = System.Threading.Interlocked.Increment(ref s_nextId);
            EnsureRaycastTarget();
            // 延迟一帧记录初始尺寸，确保面板布局已完成
            _origSize   = Vector2.zero;
        }

        private void OnDisable()
        {
            _isResizing  = false;
            _lastReported = (ResizeEdge)(-1);
            UICursorManager.Release(_instanceId);
        }

        private void OnDestroy() => UICursorManager.Release(_instanceId);

        private void EnsureRaycastTarget()
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;
            var g = GetComponent<Graphic>();
            if (g != null) { g.raycastTarget = true; return; }
            var img = gameObject.AddComponent<Image>();
            img.color         = Color.clear;
            img.raycastTarget = true;
        }

        // ── 每帧更新光标（LateUpdate，在 Unity 内部光标处理后执行） ──────────
        private void LateUpdate()
        {
            // 延迟缓存（首帧布局完成后）
            if (!_snapsCached && _rt != null && _rt.sizeDelta != Vector2.zero)
            {
                _origSize    = _rt.sizeDelta;
                _snapsCached = true;
                BuildSnapshots();
            }
            if (_isResizing) return;
            var edge    = GetEdgeAtCursor();
            var style   = EdgeToStyle(edge);
            if (style == EdgeToStyle(_lastReported)) return;
            _lastReported = edge;
            UICursorManager.Request(_instanceId, style);
        }

        // ── 按下：判断拖动 or 边缘缩放 ────────────────────────────────────────
        public void OnPointerDown(PointerEventData e)
        {
            if (_canvas == null || _rt == null) return;
            _activeEdge = GetEdgeAtCursor();
            _isResizing = _activeEdge != ResizeEdge.None;
            ToCanvasLocal(e.position, e.pressEventCamera, out _downCanvasPos);
            _startSize        = _rt.sizeDelta;
            _startAnchoredPos = _rt.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (_canvas == null || _rt == null) return;
            ToCanvasLocal(e.position, e.pressEventCamera, out var cur);
            var delta = cur - _downCanvasPos;
            if (_isResizing)
            {
                if (IsTitleBarEdge(_activeEdge)) _rt.anchoredPosition = _startAnchoredPos + delta;
                else                             ApplyResize(delta);
            }
            else if (AllowBodyDrag) _rt.anchoredPosition = _startAnchoredPos + delta;
        }

        public void OnEndDrag(PointerEventData e) => _isResizing = false;

        private static bool IsTitleBarEdge(ResizeEdge e) => e == ResizeEdge.TitleBar;

        // ── 边缘缩放逻辑 ──────────────────────────────────────────────────────
        private void ApplyResize(Vector2 d)
        {
            var sz  = _startSize;
            var pos = _startAnchoredPos;

            switch (_activeEdge)
            {
                case ResizeEdge.Right:       sz.x += d.x;  pos.x += d.x * .5f; break;
                case ResizeEdge.Left:        sz.x -= d.x;  pos.x += d.x * .5f; break;
                case ResizeEdge.Top:         sz.y += d.y;  pos.y += d.y * .5f; break;
                case ResizeEdge.Bottom:      sz.y -= d.y;  pos.y += d.y * .5f; break;
                case ResizeEdge.TopRight:    sz.x += d.x; sz.y += d.y; pos.x += d.x*.5f; pos.y += d.y*.5f; break;
                case ResizeEdge.TopLeft:     sz.x -= d.x; sz.y += d.y; pos.x += d.x*.5f; pos.y += d.y*.5f; break;
                case ResizeEdge.BottomRight: sz.x += d.x; sz.y -= d.y; pos.x += d.x*.5f; pos.y += d.y*.5f; break;
                case ResizeEdge.BottomLeft:  sz.x -= d.x; sz.y -= d.y; pos.x += d.x*.5f; pos.y += d.y*.5f; break;
            }

            sz.x = Mathf.Max(sz.x, MinSize.x);
            sz.y = Mathf.Max(sz.y, MinSize.y);
            _rt.sizeDelta        = sz;
            _rt.anchoredPosition = pos;
            ApplyContentScale(); // 边缘 resize 也同步内容
        }

        // ── 滚轮缩放 ─────────────────────────────────────────────────────────
        public void OnScroll(PointerEventData e)
        {
            if (_rt == null || !_snapsCached) return;
            float factor = 1f + ScaleStep * Mathf.Sign(e.scrollDelta.y);
            var sz = _rt.sizeDelta * factor;
            sz.x = Mathf.Clamp(sz.x, _origSize.x * ScaleMin, _origSize.x * ScaleMax);
            sz.y = Mathf.Clamp(sz.y, _origSize.y * ScaleMin, _origSize.y * ScaleMax);
            sz.x = Mathf.Max(sz.x, MinSize.x);
            sz.y = Mathf.Max(sz.y, MinSize.y);
            _rt.sizeDelta = sz;
            ApplyContentScale();
        }

        // ── 双击复位 ─────────────────────────────────────────────────────────
        public void OnPointerClick(PointerEventData e)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime < DOUBLE_CLICK_SEC && _rt != null && _snapsCached)
            {
                _rt.sizeDelta = _origSize;
                ApplyContentScale(); // fx=fy=1 → 完全还原
            }
            _lastClickTime = now;
        }

        // ── 离开面板时立即释放光标 ───────────────────────────────────────────
        public void OnPointerExit(PointerEventData e)
        {
            if (_isResizing) return;
            _lastReported = (ResizeEdge)(-1);
            UICursorManager.Release(_instanceId);
        }

        // ── 边缘检测 ─────────────────────────────────────────────────────────
        private ResizeEdge GetEdgeAtCursor()
        {
            if (_rt == null || _canvas == null) return ResizeEdge.None;
            var cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, Input.mousePosition, cam, out var lp))
                return ResizeEdge.None;

            float t    = EdgeThreshold;
            var   rect = _rt.rect;
            if (!rect.Contains(lp)) return ResizeEdge.None;

            bool L  = lp.x - rect.xMin < t;
            bool R  = rect.xMax - lp.x < t;
            bool B  = lp.y - rect.yMin < t;
            // T：顶部 EdgeThreshold 区（向上拉伸），仅 EnableTopBar 时生效
            bool T  = EnableTopBar && rect.yMax - lp.y < t;
            // TB：TopBar 中段（非顶边热区），仅 EnableTopBar 时生效
            bool TB = EnableTopBar && rect.yMax - lp.y < TitleBarHeight && !T;

            if (L && T) return ResizeEdge.TopLeft;
            if (R && T) return ResizeEdge.TopRight;
            if (L && B) return ResizeEdge.BottomLeft;
            if (R && B) return ResizeEdge.BottomRight;
            if (L)      return ResizeEdge.Left;
            if (R)      return ResizeEdge.Right;
            if (T)      return ResizeEdge.Top;      // TopBar 顶边：向上拉伸高度
            if (B)      return ResizeEdge.Bottom;
            if (TB)     return ResizeEdge.TitleBar;  // TopBar 主体：拖拽移动窗口
            return ResizeEdge.None;
        }

        // ── 边缘 → 光标样式 ──────────────────────────────────────────────────
        private static UICursorManager.CursorStyle EdgeToStyle(ResizeEdge edge) => edge switch
        {
            ResizeEdge.Left  or ResizeEdge.Right          => UICursorManager.CursorStyle.ResizeWE,
            ResizeEdge.Top   or ResizeEdge.Bottom         => UICursorManager.CursorStyle.ResizeNS,
            ResizeEdge.TopLeft or ResizeEdge.BottomRight  => UICursorManager.CursorStyle.ResizeNWSE,
            ResizeEdge.TopRight or ResizeEdge.BottomLeft  => UICursorManager.CursorStyle.ResizeNESW,
            ResizeEdge.TitleBar                           => UICursorManager.CursorStyle.Move,
            _                                             => UICursorManager.CursorStyle.Default,
        };

        // ── 子树快照构建 ─────────────────────────────────────────────────────
        private void BuildSnapshots()
        {
            _snaps = new List<ChildSnap>();
            BuildRecursive(_rt);
        }

        private void BuildRecursive(RectTransform parent)
        {
            foreach (Transform child in parent)
            {
                var crt = child as RectTransform;
                if (crt == null) continue;
                var snap = new ChildSnap
                {
                    rt       = crt,
                    origPos  = crt.anchoredPosition,
                    origSize = crt.sizeDelta,
                };
                var txt = crt.GetComponent<Text>();
                if (txt != null) { snap.text = txt; snap.origFontSize = txt.fontSize; }
                _snaps.Add(snap);
                BuildRecursive(crt);
            }
        }

        // ── 统一内容缩放（从快照原始值 × 当前/原始尺寸比）─────────────────────
        private void ApplyContentScale()
        {
            if (!_snapsCached || _snaps == null || _origSize.x <= 0 || _origSize.y <= 0) return;
            float fx = _rt.sizeDelta.x / _origSize.x;
            float fy = _rt.sizeDelta.y / _origSize.y;
            float ft = Mathf.Min(fx, fy); // 字号取较小轴，避免溢出
            foreach (var s in _snaps)
            {
                if (s.rt == null) continue;
                s.rt.anchoredPosition = new Vector2(s.origPos.x  * fx, s.origPos.y  * fy);
                s.rt.sizeDelta        = new Vector2(s.origSize.x * fx, s.origSize.y * fy);
                if (s.text != null)
                    s.text.fontSize = Mathf.Max(1, Mathf.RoundToInt(s.origFontSize * ft));
            }
        }

        // ── 辅助 ─────────────────────────────────────────────────────────────
        private void ToCanvasLocal(Vector2 screenPos, Camera cam, out Vector2 local)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, screenPos, cam, out local);
        }
    }
}
