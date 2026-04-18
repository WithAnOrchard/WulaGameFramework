using System;
using System.Collections.Generic;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.Core.Manager;
using EssSystem.EssManager.InventoryManager.Dao;
using EssSystem.EssManager.InventoryManager.Entity;
using UnityEngine;
{
    /// <summary>
    /// InventoryManager - Unity MonoBehaviour singleton for inventory management
    /// InventoryManager - InventoryManager - Unity MonoBehaviour 
    /// </summary>
    [Manager(5)] // Set priority to 5 (after EventManager and DataManager)
    public class InventoryManager : Manager<InventoryManager>
    {
        private InventoryService _inventoryService;

        [SerializeField] private bool _enableLogging = true;
        [SerializeField] private bool _autoSaveInventory = true;
        [SerializeField] private float _autoSaveInterval = 60f;

        #region Properties

        /// <summary>
        /// Inventory service instance
        /// </summary>
        public InventoryService InventoryService => _inventoryService;

        /// <summary>
        /// Whether logging is enabled
        /// </summary>
        public bool EnableLogging => _enableLogging;

        #endregion

        #region Manager Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            
            _inventoryService = InventoryService.Instance;
            
            RegisterEventHandlers();
            
            if (_autoSaveInventory)
            {
                StartAutoSave();
            }
            
            Log("InventoryManager initialized");
        }

        protected override void Start()
        {
            base.Start();
            
            // Load saved inventory data
            LoadInventoryData();
            
            Log("InventoryManager started");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            // Save inventory data before destroy
            if (_autoSaveInventory)
            {
                SaveInventoryData();
            }
            
            Log("InventoryManager destroyed");
        }

        #endregion

        #region Inventory Operations

        /// <summary>
        /// Get player inventory
        /// </summary>
        /// <returns>Player inventory data</returns>
        public InventoryService.InventoryData GetPlayerInventory()
        {
            return _inventoryService.GetInventory(InventoryService.PLAYER_INVENTORY_ID);
        }

        /// <summary>
        /// Add item to player inventory
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <param name="amount">Amount to add</param>
        /// <returns>Add result</returns>
        public InventoryService.InventoryAddResult AddItemToPlayer(InventoryItem item, int amount = 1)
        {
            var result = _inventoryService.AddItem(InventoryService.PLAYER_INVENTORY_ID, item, amount);
            
            if (result.success)
            {
                Log($"Added {result.addedAmount}x {item.Name} to player inventory");
                
                if (_autoSaveInventory)
                {
                    SaveInventoryData();
                }
            }
            else
            {
                LogError($"Failed to add item to player inventory: {result.reason}");
            }
            
            return result;
        }

        /// <summary>
        /// Remove item from player inventory
        /// </summary>
        /// <param name="itemId">Item ID to remove</param>
        /// <param name="amount">Amount to remove</param>
        /// <returns>Remove result</returns>
        public InventoryService.InventoryRemoveResult RemoveItemFromPlayer(string itemId, int amount = 1)
        {
            var result = _inventoryService.RemoveItem(InventoryService.PLAYER_INVENTORY_ID, itemId, amount);
            
            if (result.success)
            {
                Log($"Removed {result.removedAmount}x item {itemId} from player inventory");
                
                if (_autoSaveInventory)
                {
                    SaveInventoryData();
                }
            }
            else
            {
                LogError($"Failed to remove item from player inventory: {result.reason}");
            }
            
            return result;
        }

        /// <summary>
        /// Move item in player inventory
        /// </summary>
        /// <param name="fromSlot">Source slot</param>
        /// <param name="toSlot">Target slot</param>
        /// <param name="amount">Amount to move</param>
        /// <returns>Move result</returns>
        public InventoryService.InventoryMoveResult MoveItemInPlayer(int fromSlot, int toSlot, int amount = -1)
        {
            var result = _inventoryService.MoveItem(InventoryService.PLAYER_INVENTORY_ID, fromSlot, toSlot, amount);
            
            if (result.success)
            {
                Log($"Moved item from slot {fromSlot} to slot {toSlot}");
                
                if (_autoSaveInventory)
                {
                    SaveInventoryData();
                }
            }
            else
            {
                LogError($"Failed to move item: {result.reason}");
            }
            
            return result;
        }

        /// <summary>
        /// Create new inventory
        /// </summary>
        /// <param name="name">Inventory name</param>
        /// <param name="maxSlots">Maximum slots</param>
        /// <param name="maxWeight">Maximum weight</param>
        /// <returns>Inventory ID</returns>
        public string CreateInventory(string name, int maxSlots = 20, float maxWeight = 100f)
        {
            var inventoryId = _inventoryService.CreateInventory(name, maxSlots, maxWeight);
            Log($"Created inventory '{name}' with ID: {inventoryId}");
            return inventoryId;
        }

        /// <summary>
        /// Get inventory by ID
        /// </summary>
        /// <param name="inventoryId">Inventory ID</param>
        /// <returns>Inventory data</returns>
        public InventoryService.InventoryData GetInventory(string inventoryId)
        {
            return _inventoryService.GetInventory(inventoryId);
        }

        #endregion

        #region Item Template Operations

        /// <summary>
        /// Register item template
        /// </summary>
        /// <param name="template">Item template</param>
        public void RegisterItemTemplate(InventoryItem template)
        {
            _inventoryService.RegisterItemTemplate(template);
        }

        /// <summary>
        /// Get item template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <returns>Item template</returns>
        public InventoryItem GetItemTemplate(string templateId)
        {
            return _inventoryService.GetItemTemplate(templateId);
        }

        /// <summary>
        /// Create item from template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <param name="amount">Amount</param>
        /// <returns>Created item</returns>
        public InventoryItem CreateItemFromTemplate(string templateId, int amount = 1)
        {
            return _inventoryService.CreateItemFromTemplate(templateId, amount);
        }

        /// <summary>
        /// Add item from template to player inventory
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <param name="amount">Amount</param>
        /// <returns>Add result</returns>
        public InventoryService.InventoryAddResult AddItemFromTemplateToPlayer(string templateId, int amount = 1)
        {
            var item = CreateItemFromTemplate(templateId, amount);
            if (item == null)
            {
                return new InventoryService.InventoryAddResult 
                { 
                    success = false, 
                    reason = "Failed to create item from template" 
                };
            }
            
            return AddItemToPlayer(item, amount);
        }

        #endregion

        #region Data Persistence

        /// <summary>
        /// Save inventory data using event system
        /// </summary>
        public void SaveInventoryData()
        {
            try
            {
                var eventManager = EventManager.Instance;
                var result = eventManager.TriggerEvent("SaveInventoryData", new List<object>());
                
                if (result != null && result.Count > 0 && result[0].ToString() == "Success")
                {
                    Log("Inventory data saved successfully via event system");
                }
                else
                {
                    LogError("Failed to save inventory data");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error saving inventory data: {ex.Message}");
            }
        }

        /// <summary>
        /// Load inventory data using event system
        /// </summary>
        public void LoadInventoryData()
        {
            try
            {
                var eventManager = EventManager.Instance;
                var result = eventManager.TriggerEvent("LoadInventoryData", new List<object>());
                
                if (result != null && result.Count > 0 && result[0].ToString() == "Success")
                {
                    Log("Inventory data loaded successfully via event system");
                }
                else if (result != null && result.Count > 0 && result[0].ToString() == "Warning")
                {
                    Log("No saved inventory data found, using default inventory");
                }
                else
                {
                    LogError("Failed to load inventory data");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error loading inventory data: {ex.Message}");
            }
        }

        /// <summary>
        /// Start auto save coroutine
        /// </summary>
        private void StartAutoSave()
        {
            if (_autoSaveInterval > 0)
            {
                InvokeRepeating(nameof(AutoSave), _autoSaveInterval, _autoSaveInterval);
            }
        }

        /// <summary>
        /// Auto save method
        /// </summary>
        private void AutoSave()
        {
            if (_autoSaveInventory)
            {
                SaveInventoryData();
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle inventory item added event
        /// </summary>
        [Event("InventoryItemAdded")]
        public List<object> OnInventoryItemAdded(List<object> data)
        {
            try
            {
                if (data.Count >= 4)
                {
                    string inventoryId = data[0].ToString();
                    string itemId = data[1].ToString();
                    int amount = Convert.ToInt32(data[2]);
                    
                    Log($"InventoryManager received item added event: {amount}x {itemId} to {inventoryId}");
                    
                    // Update UI entities if they exist
                    UpdateInventoryUI(inventoryId);
                    
                    return new List<object> { "Success", "InventoryManager processed event" };
                }
                return new List<object> { "Error", "Invalid data format" };
            }
            catch (Exception ex)
            {
                LogError($"Error handling inventory item added event: {ex.Message}");
                return new List<object> { "Error", ex.Message };
            }
        }

        /// <summary>
        /// Handle inventory item removed event
        /// </summary>
        [Event("InventoryItemRemoved")]
        public List<object> OnInventoryItemRemoved(List<object> data)
        {
            try
            {
                if (data.Count >= 4)
                {
                    string inventoryId = data[0].ToString();
                    string itemId = data[1].ToString();
                    int amount = Convert.ToInt32(data[2]);
                    
                    Log($"InventoryManager received item removed event: {amount}x {itemId} from {inventoryId}");
                    
                    // Update UI entities if they exist
                    UpdateInventoryUI(inventoryId);
                    
                    return new List<object> { "Success", "InventoryManager processed event" };
                }
                return new List<object> { "Error", "Invalid data format" };
            }
            catch (Exception ex)
            {
                LogError($"Error handling inventory item removed event: {ex.Message}");
                return new List<object> { "Error", ex.Message };
            }
        }

        /// <summary>
        /// Handle inventory item moved event
        /// </summary>
        [Event("InventoryItemMoved")]
        public List<object> OnInventoryItemMoved(List<object> data)
        {
            try
            {
                if (data.Count >= 5)
                {
                    string inventoryId = data[0].ToString();
                    int fromSlot = Convert.ToInt32(data[1]);
                    int toSlot = Convert.ToInt32(data[2]);
                    int amount = Convert.ToInt32(data[3]);
                    
                    Log($"InventoryManager received item moved event: {amount}x from slot {fromSlot} to {toSlot} in {inventoryId}");
                    
                    // Update UI entities if they exist
                    UpdateInventoryUI(inventoryId);
                    
                    return new List<object> { "Success", "InventoryManager processed event" };
                }
                return new List<object> { "Error", "Invalid data format" };
            }
            catch (Exception ex)
            {
                LogError($"Error handling inventory item moved event: {ex.Message}");
                return new List<object> { "Error", ex.Message };
            }
        }

        /// <summary>
        /// Update inventory UI entities
        /// </summary>
        /// <param name="inventoryId">Inventory ID</param>
        private void UpdateInventoryUI(string inventoryId)
        {
            try
            {
                // Get inventory entities from UIService
                var uiService = EssSystem.EssManager.UIManager.UIService.Instance;
                if (uiService != null)
                {
                    // This would be implemented based on UI integration requirements
                    Log("Inventory UI update triggered");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error updating inventory UI: {ex.Message}");
            }
        }

        #endregion

        #region Debug and Testing

        /// <summary>
        /// Add test items to player inventory
        /// </summary>
        [ContextMenu("Add Test Items")]
        public void AddTestItems()
        {
            // Add health potions
            var healthPotionResult = AddItemFromTemplateToPlayer("health_potion", 10);
            
            // Add iron sword
            var swordResult = AddItemFromTemplateToPlayer("iron_sword", 1);
            
            Log($"Added test items - Health Potions: {healthPotionResult.addedAmount}, Sword: {swordResult.addedAmount}");
        }

        /// <summary>
        /// Clear player inventory
        /// </summary>
        [ContextMenu(" Clear Player Inventory")]
        public void ClearPlayerInventory()
        {
            var inventory = GetPlayerInventory();
            if (inventory != null)
            {
                foreach (var slot in inventory.slots)
                {
                    if (!slot.isEmpty)
                    {
                        RemoveItemFromPlayer(slot.item.Id, slot.item.CurrentStack);
                    }
                }
                Log("Player inventory cleared");
            }
        }

        /// <summary>
        /// Show inventory info
        /// </summary>
        [ContextMenu("Show Inventory Info")]
        public void ShowInventoryInfo()
        {
            var inventory = GetPlayerInventory();
            if (inventory != null)
            {
                Log($"=== Player Inventory Info ===");
                Log($"Name: {inventory.name}");
                Log($"Slots: {inventory.slots.Count(s => !s.isEmpty)}/{inventory.maxSlots}");
                Log($"Weight: {inventory.currentWeight:F1}/{inventory.maxWeight:F1}");
                Log($"Last Modified: {inventory.lastModified}");
                
                int itemCount = 0;
                foreach (var slot in inventory.slots.Where(s => !s.isEmpty))
                {
                    Log($"  Slot {inventory.slots.IndexOf(slot)}: {slot.item.Name} x{slot.item.CurrentStack}");
                    itemCount++;
                }
                
                Log($"Total Items: {itemCount}");
            }
            else
            {
                LogError("Player inventory not found");
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Log message if logging is enabled
        /// </summary>
        private void Log(string message)
        {
            if (_enableLogging)
            {
                Debug.Log($"[InventoryManager] {message}");
            }
        }

        /// <summary>
        /// Log error message
        /// </summary>
        private void LogError(string message)
        {
            Debug.LogError($"[InventoryManager] {message}");
        }

        #endregion
    }
}
