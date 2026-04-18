using System;
using System.Collections.Generic;
using System.Linq;

namespace EssSystem.EssManager.InventoryManager.Dao
{
    /// <summary>
    /// 背包槽位 — 容器的最小单元
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        /// <summary>槽位索引</summary>
        public int Index;

        /// <summary>槽内物品，null 表示空</summary>
        public InventoryItem Item;

        /// <summary>是否被锁定（不可操作，业务自定义）</summary>
        public bool Locked;

        public bool IsEmpty => Item == null || Item.IsEmpty;

        public InventorySlot() { }

        public InventorySlot(int index)
        {
            Index = index;
        }

        public void Clear()
        {
            Item = null;
        }
    }

    /// <summary>
    /// 背包 Dao — 一个有限槽位 + 权重上限的物品容器
    /// </summary>
    [Serializable]
    public class Inventory
    {
        #region Fields

        public string Id;
        public string Name;
        public int MaxSlots;
        public float MaxWeight;
        public DateTime LastModified;
        public List<InventorySlot> Slots;

        #endregion

        #region Derived

        /// <summary>当前总权重（随物品即时计算）</summary>
        public float CurrentWeight => Slots == null
            ? 0f
            : Slots.Where(s => !s.IsEmpty).Sum(s => s.Item.TotalWeight);

        /// <summary>已占用槽位数</summary>
        public int UsedSlots => Slots == null ? 0 : Slots.Count(s => !s.IsEmpty);

        /// <summary>权重是否已达上限</summary>
        public bool IsOverweight => MaxWeight > 0 && CurrentWeight > MaxWeight;

        #endregion

        #region Constructors

        /// <summary>反序列化用</summary>
        public Inventory() { }

        /// <summary>新建空背包</summary>
        public Inventory(string id, string name, int maxSlots = 20, float maxWeight = 100f)
        {
            Id = id;
            Name = name ?? id;
            MaxSlots = Math.Max(1, maxSlots);
            MaxWeight = Math.Max(0f, maxWeight);
            LastModified = DateTime.Now;
            Slots = new List<InventorySlot>(MaxSlots);
            for (int i = 0; i < MaxSlots; i++) Slots.Add(new InventorySlot(i));
        }

        #endregion

        #region Slot Access

        public InventorySlot GetSlot(int index) =>
            (index >= 0 && index < Slots.Count) ? Slots[index] : null;

        public IEnumerable<InventorySlot> GetEmptySlots() =>
            Slots.Where(s => s.IsEmpty && !s.Locked);

        public IEnumerable<InventorySlot> GetOccupiedSlots() =>
            Slots.Where(s => !s.IsEmpty && !s.Locked);

        /// <summary>查指定 itemId 的所有槽位</summary>
        public IEnumerable<InventorySlot> FindSlotsOf(string itemId) =>
            Slots.Where(s => !s.IsEmpty && s.Item.Id == itemId);

        /// <summary>查指定 itemId 的总数</summary>
        public int CountOf(string itemId) =>
            FindSlotsOf(itemId).Sum(s => s.Item.CurrentStack);

        public void Touch() => LastModified = DateTime.Now;

        #endregion
    }
}
