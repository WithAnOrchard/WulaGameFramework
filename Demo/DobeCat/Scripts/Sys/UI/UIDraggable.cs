using UnityEngine;
using UnityEngine.EventSystems;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 挂在任意 Canvas RectTransform 上，使其可被鼠标拖拽移动，并支持滚轮缩放。
    /// <list type="bullet">
    /// <item>拖拽：按住标题栏移动整个面板</item>
    /// <item>滚轮缩放：鼠标悬停时滚动 ±10%，范围 50%–200%</item>
    /// <item>双击复位：双击标题栏恢复 100%</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class UIDraggable : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        [Tooltip("拖动 / 缩放时实际操作的目标（留空 = 本 GameObject 自身）。")]
        public RectTransform DragTarget;

        [Tooltip("滚轮每步缩放百分比（默认 0.10 = 10%）。")]
        public float ScaleStep = 0.10f;

        [Tooltip("最小缩放比（默认 0.5 = 50%）。")]
        public float ScaleMin = 0.5f;

        [Tooltip("最大缩放比（默认 2.0 = 200%）。")]
        public float ScaleMax = 2.0f;

        private RectTransform _rt;
        private RectTransform _canvasRt;
        private Vector2       _dragOffset;

        private float _lastClickTime;
        private const float DoubleClickInterval = 0.3f;

        private void Awake()
        {
            _rt       = GetComponent<RectTransform>();
            var canvas = GetComponentInParent<Canvas>();
            _canvasRt  = canvas != null ? canvas.transform as RectTransform : null;
        }

        // ── 拖拽 ─────────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            if (_canvasRt == null) return;
            var target = DragTarget != null ? DragTarget : _rt;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRt, e.position, e.pressEventCamera, out var local);
            _dragOffset = local - target.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (_canvasRt == null) return;
            var target = DragTarget != null ? DragTarget : _rt;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRt, e.position, e.pressEventCamera, out var local);
            target.anchoredPosition = local - _dragOffset;
        }

        // ── 滚轮缩放 ─────────────────────────────────────────────────────────

        public void OnScroll(PointerEventData e)
        {
            var target = DragTarget != null ? DragTarget : _rt;
            float cur  = target.localScale.x;
            float next = Mathf.Clamp(cur + ScaleStep * Mathf.Sign(e.scrollDelta.y), ScaleMin, ScaleMax);
            target.localScale = Vector3.one * next;
        }

        // ── 双击复位 ─────────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData e)
        {
            float now = Time.unscaledTime;
            if (now - _lastClickTime < DoubleClickInterval)
            {
                var target = DragTarget != null ? DragTarget : _rt;
                target.localScale = Vector3.one;
            }
            _lastClickTime = now;
        }
    }
}

