using EssSystem.Core.Application.SingleManagers.InventoryManager;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;
using Demo.DobeCat.Game;
using UnityEngine;

namespace Demo.DobeCat.Game.Farm
{
    public class HotbarSelectionDriver : MonoBehaviour
    {
        public static int SelectedSlot => InventoryManager.TryGetInstance()?.SelectedHotbarSlot ?? -1;

        public static InventoryItem HeldItem => InventoryManager.TryGetInstance()?.HotbarHeldItem;

        private void Start()
        {
            DobeCatGameContext.OnContextChanged += OnContextChanged;
        }

        private void OnDestroy()
        {
            DobeCatGameContext.OnContextChanged -= OnContextChanged;
        }

        public void ToggleSelection(int slotIndex)
        {
            InventoryManager.TryGetInstance()?.ToggleHotbarSelection(slotIndex);
        }

        public void ClearSelection()
        {
            InventoryManager.TryGetInstance()?.ClearHotbarSelection();
        }

        private static void OnContextChanged(bool active)
        {
            if (!active)
                InventoryManager.TryGetInstance()?.ClearHotbarSelection();
        }
    }
}
