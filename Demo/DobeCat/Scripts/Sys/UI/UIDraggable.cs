using UnityEngine;
using UnityEngine.EventSystems;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 挂在任意 Canvas RectTransform 上，使其可被鼠标拖拽移动。
    /// 需要父层级存在 Canvas，面板背景需有 Graphic（Image）提供射线检测。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIDraggable : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [Tooltip("拖动时实际移动的目标（留空 = 本 GameObject 自身）。")]
        public RectTransform DragTarget;

        private RectTransform _rt;
        private RectTransform _canvasRt;
        private Vector2       _dragOffset;

        private void Awake()
        {
            _rt       = GetComponent<RectTransform>();
            var canvas = GetComponentInParent<Canvas>();
            _canvasRt  = canvas != null ? canvas.transform as RectTransform : null;
        }

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
    }
}
