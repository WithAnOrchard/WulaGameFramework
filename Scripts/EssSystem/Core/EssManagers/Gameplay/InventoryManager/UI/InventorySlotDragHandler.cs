using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager
{
    /// <summary>
    /// 槽位拖拽处理器 — 挂在 Slot 的 GameObject 上，提供「按住拖动 → 释放交换/堆叠/搬运」功能。
    /// <para>强解耦：拖落时通过 <see cref="InventoryService.EVT_MOVE"/> 事件触发，<b>不直接调 Service API</b>。UI 通过 <c>EVT_CHANGED</c> 广播自动刷新。</para>
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
            if (!EventProcessor.HasInstance) return;
            // 强解耦：通过 EVT_QUERY 取容器；不直接 InventoryService.Instance
            var qr = EventProcessor.Instance.TriggerEventMethod(
                InventoryService.EVT_QUERY, new List<object> { InventoryId });
            if (!ResultCode.IsOk(qr) || qr.Count < 2) return;
            var inv  = qr[1] as Inventory;
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
            if (!EventProcessor.HasInstance) return;

            // 强解耦：走 EVT_MOVE 事件（跨容器 5 参形），不直调 Service API
            EventProcessor.Instance.TriggerEventMethod(
                InventoryService.EVT_MOVE,
                new List<object> { src.InventoryId, src.SlotIndex, InventoryId, SlotIndex, -1 });
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
