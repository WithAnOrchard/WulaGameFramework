using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Platform.Windows;
using Demo.DobeCat.Game;

namespace Demo.DobeCat.Game.Farm
{
    /// <summary>
    /// 快捷栏选择驱动器。
    /// <list type="bullet">
    /// <item>按 1~9 选中对应快捷栏槽位，再次按同键取消选中。</item>
    /// <item><see cref="HeldItem"/> 暴露当前"持有"的物品，供 <see cref="FarmWorldController"/> 等系统使用。</item>
    /// <item>选中格高亮变色；退出游戏上下文时自动复位选中与高亮。</item>
    /// </list>
    /// </summary>
    public class HotbarSelectionDriver : MonoBehaviour
    {
        // ── 静态访问 ──────────────────────────────────────────────
        public static int SelectedSlot { get; private set; } = -1;

        /// <summary>当前持有物品；未选中或槽位为空时返回 null。</summary>
        public static InventoryItem HeldItem
        {
            get
            {
                if (SelectedSlot < 0 || !InventoryService.HasInstance) return null;
                return InventoryService.Instance
                    .GetInventory(InventoryManager.ID_HOTBAR)
                    ?.GetSlot(SelectedSlot)?.Item;
            }
        }

        // ── 高亮配色 ──────────────────────────────────────────────
        private static readonly Color NormalColor   = Color.white;
        private static readonly Color SelectedColor = new Color(0.35f, 0.90f, 1.00f, 1f);

        // ── 槽位 Image 缓存（运行时按需获取，上下文切换时清空）──
        private Image[] _slotImages;

        // ── 数字键边沿检测（GetAsyncKeyState 是连续电平，需自行记录上一帧状态）──
        private readonly bool[] _wasNumKey = new bool[9];

        private void Start()
        {
            DobeCatGameContext.OnContextChanged += OnContextChanged;
        }

        private void OnDestroy()
        {
            DobeCatGameContext.OnContextChanged -= OnContextChanged;
        }

        private void OnContextChanged(bool active)
        {
            _slotImages = null;
            if (!active)
            {
                var prev = SelectedSlot;
                SelectedSlot = -1;
                // 清掉旧高亮（此时 UI 可能已隐藏，但还在内存中；设回白色不影响隐藏状态）
                if (prev >= 0) ApplyColor(prev, NormalColor);
            }
        }

        private void Update()
        {
            for (var i = 0; i < 9; i++)
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                var down = (Win32Native.GetAsyncKeyState(Win32Native.VK_1 + i) & 0x8000) != 0;
                var triggered = down && !_wasNumKey[i];
                _wasNumKey[i] = down;
#else
                var triggered = Input.GetKeyDown(KeyCode.Alpha1 + i);
#endif
                if (!triggered) continue;
                var prev = SelectedSlot;
                SelectedSlot = (SelectedSlot == i) ? -1 : i;
                RefreshHighlight(prev, SelectedSlot);
                break;
            }
        }

        // ── 高亮逻辑 ──────────────────────────────────────────────

        private void RefreshHighlight(int prev, int next)
        {
            if (prev >= 0) ApplyColor(prev, NormalColor);
            if (next >= 0) ApplyColor(next, SelectedColor);
        }

        private void ApplyColor(int slot, Color color)
        {
            EnsureSlotImages();
            if (_slotImages == null || slot < 0 || slot >= _slotImages.Length) return;
            if (_slotImages[slot] != null) _slotImages[slot].color = color;
        }

        private void EnsureSlotImages()
        {
            if (_slotImages != null && _slotImages.Length > 0 && _slotImages[0] != null) return;
            if (!EventProcessor.HasInstance) return;

            _slotImages = new Image[9];
            for (var i = 0; i < 9; i++)
            {
                var r = EventProcessor.Instance.TriggerEventMethod(
                    "GetUIGameObject",
                    new List<object> { $"{InventoryManager.ID_HOTBAR}_Slot_{i}" });
                var go = ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as GameObject : null;
                if (go != null) _slotImages[i] = go.GetComponent<Image>();
            }
        }
    }
}
