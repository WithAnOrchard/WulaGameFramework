using System;
using System.Collections.Generic;
using System.Linq;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.Core.Manager;
using EssSystem.EssManager.InventoryManager.Dao;
using EssSystem.EssManager.InventoryManager.Entity;
using UnityEngine;
{
    /// <summary>
    /// InventoryService - Inventory system business logic and data management
    /// InventoryService - InventoryService - InventoryService
    /// </summary>
    public class InventoryService : Service<InventoryService>
    {
        public const string INVENTORY_CATEGORY = "Inventories";
        public const string ITEM_TEMPLATES_CATEGORY = "ItemTemplates";
        public const string PLAYER_INVENTORY_ID = "PlayerInventory";
        public const string INVENTORY_ENTITIES_CATEGORY = "InventoryEntities";
        public const string INVENTORY_SLOT_ENTITIES_CATEGORY = "InventorySlotEntities";

        /// <summary>
        /// Inventory data structure
        /// </summary>
        [Serializable]
        public class InventoryData
        {
            public string id;
            public string name;
            public int maxSlots;
            public List<InventorySlot> slots;
            public float maxWeight;
            public float currentWeight;
            public DateTime lastModified;

            public InventoryData()
            {
                id = System.Guid.NewGuid().ToString();
                name = "New Inventory";
                maxSlots = 20;
                slots = new List<InventorySlot>();
                maxWeight = 100f;
                currentWeight = 0f;
                lastModified = DateTime.Now;
                
                // Initialize empty slots
                for (int i = 0; i < maxSlots; i++)
                {
                    slots.Add(new InventorySlot(i));
                }
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            InitializeDefaultInventory();
            InitializeItemTemplates();
        }

        #region Inventory Management

        /// <summary>
        /// Create new inventory
        /// </summary>
        /// <param name="name">Inventory name</param>
        /// <param name="maxSlots">Maximum slots</param>
        /// <param name="maxWeight">Maximum weight</param>
        /// <returns>Inventory ID</returns>
        public string CreateInventory(string name, int maxSlots = 20, float maxWeight = 100f)
        {
            var inventory = new InventoryData
            {
                name = name,
                maxSlots = maxSlots,
                maxWeight = maxWeight
            };

            SetData(INVENTORY_CATEGORY, inventory.id, inventory);
            Log($"Inventory '{name}' created with ID: {inventory.id}");
            
            return inventory.id;
        }

        /// <summary>
        /// Get inventory by ID
        /// </summary>
        /// <param name="inventoryId">Inventory ID</param>
        /// <returns>Inventory data</returns>
        public InventoryData GetInventory(string inventoryId)
        {
            return GetData<InventoryData>(INVENTORY_CATEGORY, inventoryId);
        }

        #region Entity Management

        /// <summary>
        /// Register inventory entity
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <param name="entity">Inventory entity</param>
        public void RegisterInventoryEntity(string itemId, InventoryEntity entity)
        {
            if (string.IsNullOrEmpty(itemId) || entity == null) return;
            SetData(INVENTORY_ENTITIES_CATEGORY, itemId, entity);
        }

        /// <summary>
        /// Get inventory entity
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Inventory entity</returns>
        public InventoryEntity GetInventoryEntity(string itemId)
        {
            return GetData<InventoryEntity>(INVENTORY_ENTITIES_CATEGORY, itemId);
        }

        /// <summary>
        /// Unregister inventory entity
        /// </summary>
        /// <param name="itemId">Item ID</param>
        public void UnregisterInventoryEntity(string itemId)
        {
            RemoveData(INVENTORY_ENTITIES_CATEGORY, itemId);
        }

        /// <summary>
        /// Register inventory slot entity
        /// </summary>
        /// <param name="slotIndex">Slot index</param>
        /// <param name="entity">Inventory slot entity</param>
        public void RegisterInventorySlotEntity(int slotIndex, InventorySlotEntity entity)
        {
            if (slotIndex < 0 || entity == null) return;
            SetData(INVENTORY_SLOT_ENTITIES_CATEGORY, slotIndex.ToString(), entity);
        }

        /// <summary>
        /// Get inventory slot entity
        /// </summary>
        /// <param name="slotIndex">Slot index</param>
        /// <returns>Inventory slot entity</returns>
        public InventorySlotEntity GetInventorySlotEntity(int slotIndex)
        {
            return GetData<InventorySlotEntity>(INVENTORY_SLOT_ENTITIES_CATEGORY, slotIndex.ToString());
        }

        /// <summary>
        /// Unregister inventory slot entity
        /// </summary>
        /// <param name="slotIndex">Slot index</param>
        public void UnregisterInventorySlotEntity(int slotIndex)
        {
            RemoveData(INVENTORY_SLOT_ENTITIES_CATEGORY, slotIndex.ToString());
        }

        #endregion

        #region Inventory Operations

        /// <summary>
        /// Add item to inventory
        /// </summary>
        /// <param name="inventoryId">Inventory ID</param>
        /// <param name="item">Item to add</param>
        /// <param name="amount">Amount to add</param>
        /// <returns>Add result</returns>
        public InventoryAddResult AddItem(string inventoryId, InventoryItem item, int amount = 1)
        {
            var inventory = GetInventory(inventoryId);
            if (inventory == null)
            {
                return new InventoryAddResult { success = false, reason = "Inventory not found" };
            }

            if (item == null || amount <= 0)
            {
                return new InventoryAddResult { success = false, reason = "Invalid item or amount" };
            }

            int remainingAmount = amount;
            int addedAmount = 0;

            // Try to stack with existing items first
            if (item.IsStackable)
            {
                foreach (var slot in inventory.slots.Where(s => !s.isEmpty && !s.isLocked))
                {
                    if (slot.item.CanStackWith(item))
                    {
                        int stackAdded = slot.item.AddToStack(remainingAmount);
                        addedAmount += stackAdded;
                        remainingAmount -= stackAdded;

                        if (remainingAmount <= 0) break;
                    }
                }
            }

            // Add remaining items to empty slots
            if (remainingAmount > 0)
            {
                foreach (var slot in inventory.slots.Where(s => s.isEmpty && !s.isLocked))
                {
                    var newItem = item.Clone();
                    newItem.CurrentStack = Math.Min(remainingAmount, newItem.StackSize);
                    
                    slot.item = newItem;
                    slot.isEmpty = false;
                    slot.slotIndex = inventory.slots.IndexOf(slot);

                    addedAmount += newItem.CurrentStack;
                    remainingAmount -= newItem.CurrentStack;

                    if (remainingAmount <= 0) break;
                }
            }

            // Update inventory weight
            UpdateInventoryWeight(inventory);
            inventory.lastModified = DateTime.Now;

            // Save inventory
            SetData(INVENTORY_CATEGORY, inventoryId, inventory);

            // Trigger event
            TriggerInventoryItemAddedEvent(inventoryId, item, addedAmount);

            if (remainingAmount > 0)
            {
                return new InventoryAddResult 
                { 
                    success = true, 
                    addedAmount = addedAmount,
                    remainingAmount = remainingAmount,
                    reason = "Inventory full, some items not added"
                };
            }

            return new InventoryAddResult 
            { 
                success = true, 
                addedAmount = addedAmount,
                reason = "All items added successfully"
            };
        }

        /// <summary>
        /// Remove item from inventory
        /// </summary>
        /// <param name="inventoryId">Inventory ID</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="amount">Amount to remove</param>
        /// <returns>Remove result</returns>
        public InventoryRemoveResult RemoveItem(string inventoryId, string itemId, int amount = 1)
        {
            var inventory = GetInventory(inventoryId);
            if (inventory == null)
            {
                return new InventoryRemoveResult { success = false, reason = "Inventory not found" };
            }

            if (string.IsNullOrEmpty(itemId) || amount <= 0)
            {
                return new InventoryRemoveResult { success = false, reason = "Invalid item ID or amount" };
            }

            int remainingAmount = amount;
            int removedAmount = 0;
            List<InventoryItem> removedItems = new List<InventoryItem>();

            // Find and remove items
            foreach (var slot in inventory.slots.Where(s => !s.isEmpty && !s.isLocked))
            {
                if (slot.item.Id == itemId)
                {
                    int stackRemoved = slot.item.RemoveFromStack(remainingAmount);
                    removedAmount += stackRemoved;
                    remainingAmount -= stackRemoved;

                    if (slot.item.IsStackEmpty)
                    {
                        // Clear empty slot
                        removedItems.Add(slot.item);
                        slot.item = null;
                        slot.isEmpty = true;
                        slot.slotIndex = -1;
                    }

                    if (remainingAmount <= 0) break;
                }
            }

            // Update inventory weight
            UpdateInventoryWeight(inventory);
            inventory.lastModified = DateTime.Now;

            // Save inventory
            SetData(INVENTORY_CATEGORY, inventoryId, inventory);

            // Trigger event
            TriggerInventoryItemRemovedEvent(inventoryId, itemId, removedAmount);

            if (remainingAmount > 0)
            {
                return new InventoryRemoveResult 
                { 
                    success = false, 
                    removedAmount = removedAmount,
                    remainingAmount = remainingAmount,
                    reason = "Not enough items to remove"
                };
            }

            return new InventoryRemoveResult 
            { 
                success = true, 
                removedAmount = removedAmount,
                reason = "Items removed successfully"
            };
        }

        /// <summary>
        /// Move item between slots
        /// </summary>
        /// <param name="inventoryId">Inventory ID</param>
        /// <param name="fromSlot">Source slot index</param>
        /// <param name="toSlot">Target slot index</param>
        /// <param name="amount">Amount to move (for stackable items)</param>
        /// <returns>Move result</returns>
        public InventoryMoveResult MoveItem(string inventoryId, int fromSlot, int toSlot, int amount = -1)
        {
            var inventory = GetInventory(inventoryId);
            if (inventory == null)
            {
                return new InventoryMoveResult { success = false, reason = "Inventory not found" };
            }

            if (fromSlot < 0 || fromSlot >= inventory.slots.Count || 
                toSlot < 0 || toSlot >= inventory.slots.Count)
            {
                return new InventoryMoveResult { success = false, reason = "Invalid slot index" };
            }

            var fromSlotData = inventory.slots[fromSlot];
            var toSlotData = inventory.slots[toSlot];

            if (fromSlotData.isEmpty || fromSlotData.isLocked)
            {
                return new InventoryMoveResult { success = false, reason = "Source slot is empty or locked" };
            }

            if (toSlotData.isLocked)
            {
                return new InventoryMoveResult { success = false, reason = "Target slot is locked" };
            }

            // Handle different move scenarios
            if (toSlotData.isEmpty)
            {
                // Move to empty slot
                if (amount == -1 || amount >= fromSlotData.item.CurrentStack)
                {
                    // Move entire stack
                    toSlotData.item = fromSlotData.item;
                    toSlotData.isEmpty = false;
                    toSlotData.slotIndex = toSlot;

                    fromSlotData.item = null;
                    fromSlotData.isEmpty = true;
                    fromSlotData.slotIndex = -1;
                }
                else
                {
                    // Split stack
                    var splitItem = fromSlotData.item.SplitStack(amount);
                    if (splitItem != null)
                    {
                        toSlotData.item = splitItem;
                        toSlotData.isEmpty = false;
                        toSlotData.slotIndex = toSlot;
                    }
                }
            }
            else if (fromSlotData.item.CanStackWith(toSlotData.item))
            {
                // Stack with existing item
                int stackAdded = toSlotData.item.AddToStack(amount == -1 ? fromSlotData.item.CurrentStack : amount);
                fromSlotData.item.RemoveFromStack(stackAdded);

                if (fromSlotData.item.IsStackEmpty)
                {
                    fromSlotData.item = null;
                    fromSlotData.isEmpty = true;
                    fromSlotData.slotIndex = -1;
                }
            }
            else
            {
                // Swap items
                var tempItem = fromSlotData.item;
                var tempIndex = fromSlotData.slotIndex;

                fromSlotData.item = toSlotData.item;
                fromSlotData.slotIndex = toSlot;

                toSlotData.item = tempItem;
                toSlotData.slotIndex = tempIndex;
            }

            // Update inventory
            inventory.lastModified = DateTime.Now;
            SetData(INVENTORY_CATEGORY, inventoryId, inventory);

            // Trigger event
            TriggerInventoryItemMovedEvent(inventoryId, fromSlot, toSlot, amount);

            return new InventoryMoveResult { success = true, reason = "Item moved successfully" };
        }

        #endregion

        #region Item Template Management

        /// <summary>
        /// Register item template
        /// </summary>
        /// <param name="template">Item template</param>
        public void RegisterItemTemplate(InventoryItem template)
        {
            if (template == null || string.IsNullOrEmpty(template.Id))
            {
                LogError("Invalid item template");
                return;
            }

            SetData(ITEM_TEMPLATES_CATEGORY, template.Id, template);
            Log($"Item template '{template.Name}' registered");
        }

        /// <summary>
        /// Get item template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <returns>Item template</returns>
        public InventoryItem GetItemTemplate(string templateId)
        {
            return GetData<InventoryItem>(ITEM_TEMPLATES_CATEGORY, templateId);
        }

        /// <summary>
        /// Create item from template
        /// </summary>
        /// <param name="templateId">Template ID</param>
        /// <param name="amount">Amount</param>
        /// <returns>Created item</returns>
        public InventoryItem CreateItemFromTemplate(string templateId, int amount = 1)
        {
            var template = GetItemTemplate(templateId);
            if (template == null)
            {
                LogError($"Item template '{templateId}' not found");
                return null;
            }

            var item = template.Clone();
            item.CurrentStack = Math.Min(amount, item.StackSize);
            
            return item;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Update inventory weight
        /// </summary>
        /// <param name="inventory">Inventory to update</param>
        private void UpdateInventoryWeight(InventoryData inventory)
        {
            inventory.currentWeight = inventory.slots
                .Where(s => !s.isEmpty && s.item != null)
                .Sum(s => s.item.TotalWeight);
        }

        /// <summary>
        /// Initialize default player inventory
        /// </summary>
        private void InitializeDefaultInventory()
        {
            var existingInventory = GetInventory(PLAYER_INVENTORY_ID);
            if (existingInventory == null)
            {
                CreateInventory("Player Inventory", 30, 150f);
                SetData(INVENTORY_CATEGORY, PLAYER_INVENTORY_ID, GetInventory(PLAYER_INVENTORY_ID));
            }
        }

        /// <summary>
        /// Initialize default item templates
        /// </summary>
        private void InitializeItemTemplates()
        {
            // Add some basic item templates
            var healthPotion = new InventoryItem("health_potion", "Health Potion", "Restores 50 HP", 99, true)
            {
                Type = InventoryItemType.Consumable,
                Weight = 0.5f,
                Value = 25
            };
            RegisterItemTemplate(healthPotion);

            var sword = new InventoryItem("iron_sword", "Iron Sword", "A basic iron sword", 1, false)
            {
                Type = InventoryItemType.Weapon,
                Weight = 3.0f,
                Value = 100
            };
            RegisterItemTemplate(sword);
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
                    DateTime timestamp = Convert.ToDateTime(data[3]);
                    
                    Log($"Item added event processed: {amount}x {itemId} to {inventoryId} at {timestamp}");
                    return new List<object> { "Success", "Event processed" };
                }
                return new List<object> { "Error", "Invalid data format" };
            }
            catch (Exception ex)
            {
                LogError($"Error processing inventory item added event: {ex.Message}");
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
                    DateTime timestamp = Convert.ToDateTime(data[3]);
                    
                    Log($"Item removed event processed: {amount}x {itemId} from {inventoryId} at {timestamp}");
                    return new List<object> { "Success", "Event processed" };
                }
                return new List<object> { "Error", "Invalid data format" };
            }
            catch (Exception ex)
            {
                LogError($"Error processing inventory item removed event: {ex.Message}");
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
                    DateTime timestamp = Convert.ToDateTime(data[4]);
                    
                    Log($"Item moved event processed: {amount}x from slot {fromSlot} to {toSlot} in {inventoryId} at {timestamp}");
                    return new List<object> { "Success", "Event processed" };
                }
                return new List<object> { "Error", "Invalid data format" };
            }
            catch (Exception ex)
            {
                LogError($"Error processing inventory item moved event: {ex.Message}");
                return new List<object> { "Error", ex.Message };
            }
        }

        /// <summary>
        /// Save inventory data event handler
        /// </summary>
        [Event("SaveInventoryData")]
        public List<object> SaveInventoryData(List<object> data)
        {
            try
            {
                var inventories = new Dictionary<string, object>();
                var inventoryKeys = GetKeys(INVENTORY_CATEGORY);
                
                foreach (var key in inventoryKeys)
                {
                    var inventory = GetData<InventoryData>(INVENTORY_CATEGORY, key);
                    if (inventory != null)
                    {
                        inventories[key] = inventory;
                    }
                }
                
                SetData(INVENTORY_CATEGORY, "all_inventories", inventories);
                Log("Inventory data saved via event system");
                return new List<object> { "Success", "Inventory data saved" };
            }
            catch (Exception ex)
            {
                LogError($"Error saving inventory data via event: {ex.Message}");
                return new List<object> { "Error", ex.Message };
            }
        }

        /// <summary>
        /// Load inventory data event handler
        /// </summary>
        [Event("LoadInventoryData")]
        public List<object> LoadInventoryData(List<object> data)
        {
            try
            {
                var inventories = GetData<Dictionary<string, object>>(INVENTORY_CATEGORY, "all_inventories");
                if (inventories != null)
                {
                    foreach (var kvp in inventories)
                    {
                        SetData(INVENTORY_CATEGORY, kvp.Key, kvp.Value);
                    }
                    Log("Inventory data loaded via event system");
                    return new List<object> { "Success", "Inventory data loaded" };
                }
                
                Log("No saved inventory data found");
                return new List<object> { "Warning", "No saved data found" };
            }
            catch (Exception ex)
            {
                LogError($"Error loading inventory data via event: {ex.Message}");
                return new List<object> { "Error", ex.Message };
            }
        }

        #endregion

        #region Event Triggers

        /// <summary>
        /// Trigger inventory item added event
        /// </summary>
        private void TriggerInventoryItemAddedEvent(string inventoryId, InventoryItem item, int amount)
        {
            var eventManager = EventManager.Instance;
            eventManager.TriggerEvent("InventoryItemAdded", new List<object>
            {
                inventoryId,
                item.Id,
                amount,
                DateTime.Now
            });
        }

        /// <summary>
        /// Trigger inventory item removed event
        /// </summary>
        private void TriggerInventoryItemRemovedEvent(string inventoryId, string itemId, int amount)
        {
            var eventManager = EventManager.Instance;
            eventManager.TriggerEvent("InventoryItemRemoved", new List<object>
            {
                inventoryId,
                itemId,
                amount,
                DateTime.Now
            });
        }

        /// <summary>
        /// Trigger inventory item moved event
        /// </summary>
        private void TriggerInventoryItemMovedEvent(string inventoryId, int fromSlot, int toSlot, int amount)
        {
            var eventManager = EventManager.Instance;
            eventManager.TriggerEvent("InventoryItemMoved", new List<object>
            {
                inventoryId,
                fromSlot,
                toSlot,
                amount,
                DateTime.Now
            });
        }

        #endregion
    }

    #region Result Structures

    /// <summary>
    /// Inventory add result
    /// </summary>
    [Serializable]
    public class InventoryAddResult
    {
        public bool success;
        public int addedAmount;
        public int remainingAmount;
        public string reason;
    }

    /// <summary>
    /// Inventory remove result
    /// </summary>
    [Serializable]
    public class InventoryRemoveResult
    {
        public bool success;
        public int removedAmount;
        public int remainingAmount;
        public string reason;
    }

    /// <summary>
    /// Inventory move result
    /// </summary>
    [Serializable]
    public class InventoryMoveResult
    {
        public bool success;
        public string reason;
    }

    #endregion
}
