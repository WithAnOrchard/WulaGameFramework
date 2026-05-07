using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager
{
    /// <summary>
    /// 槽位拖拽处理器 — 挂在 Slot 的 GameObject 上，提供「按住拖动 → 释放交换/堆叠/搬运」功能。
    /// <para>数据层走 <see cref="InventoryService.MoveItem"/>；UI 通过 <c>EVT_CHANGED</c> 广播自动刷新。</para>
    /// </summary>
    public class InventorySlotDragHandler : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        /// <summary>本槽所属容器 ID</summary>
        public string InventoryId;

        /// <summary>本槽在容器中的索引</summary>
        public int SlotIndex;

        /// <summary>用于生成拖拽幽灵图的源 Image（slot 内部图标 Image）</summary>
        public Image SourceIconImage;

        private GameObject _ghost;
        private Canvas     _rootCanvas;

        public void OnBeginDrag(PointerEventData eventData)
        {
            var inv  = InventoryService.Instance?.GetInventory(InventoryId);
            var slot = inv?.GetSlot(SlotIndex);
            if (slot == null || slot.IsEmpty) return;

            _rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            if (_rootCanvas == null) return;

            _ghost = new GameObject("InventoryDragGhost");
            _ghost.transform.SetParent(_rootCanvas.transform, false);

            var rect = _ghost.AddComponent<RectTransform>();
            rect.sizeDelta = SourceIconImage != null
                ? SourceIconImage.rectTransform.sizeDelta
                : new Vector2(64f, 64f);

            var img = _ghost.AddComponent<Image>();
            if (SourceIconImage != null)
            {
                img.sprite = SourceIconImage.sprite;
                img.preserveAspect = true;
            }
            img.color = new Color(1f, 1f, 1f, 0.85f);
            img.raycastTarget = false; // 幽灵不能拦截 OnDrop

            UpdateGhostPosition(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghost != null) UpdateGhostPosition(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var src = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<InventorySlotDragHandler>()
                : null;
            if (src == null || src == this) return;
            // 暂只支持同容器内移动；跨容器需扩展 InventoryService
            if (src.InventoryId != InventoryId) return;

            InventoryService.Instance?.MoveItem(InventoryId, src.SlotIndex, SlotIndex);
        }

        private void UpdateGhostPosition(PointerEventData eventData)
        {
            if (_ghost == null || _rootCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.transform as RectTransform,
                eventData.position,
                _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : eventData.pressEventCamera,
                out var localPoint);
            _ghost.transform.localPosition = localPoint;
        }
    }
}
