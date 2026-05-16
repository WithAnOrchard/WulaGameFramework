using System;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao
{
    /// <summary>
    /// 物品类型 — 标签用，业务可自由扩展
    /// </summary>
    public enum InventoryItemType
    {
        /// <summary>杂项（默认）</summary>
        Misc = 0,
        /// <summary>消耗品</summary>
        Consumable,
        /// <summary>武器</summary>
        Weapon,
        /// <summary>防具</summary>
        Armor,
        /// <summary>装备（通用）</summary>
        Equipment,
        /// <summary>材料</summary>
        Material,
        /// <summary>任务物品（不可丢弃）</summary>
        Quest
    }

    /// <summary>
    /// 物品 Dao — 纯数据类，支持链式构造
    /// <para>
    /// 模板与实例共用同一个类型：<see cref="MaxStack"/> > 1 表示可堆叠，
    /// <see cref="CurrentStack"/> 仅对放入背包的"实例"有意义。
    /// </para>
    /// </summary>
    [Serializable]
    public class InventoryItem
    {
        #region Fields (public for serialization)

        public string Id;
        public string Name;
        public string Description;
        public InventoryItemType Type;
        public int Value;
        /// <summary>图标 Sprite ID（由 ResourceManager 解析，与 <c>UIButtonComponent.ButtonSpriteId</c> 同一约定）</summary>
        public string IconSpriteId;
        /// <summary>堆叠上限，=1 表示不可堆叠</summary>
        public int MaxStack = 1;
        /// <summary>当前堆叠数量（模板里通常为 0）</summary>
        public int CurrentStack;

        #endregion

        #region Derived Properties

        /// <summary>是否可堆叠（MaxStack > 1）</summary>
        public bool IsStackable => MaxStack > 1;

        /// <summary>堆叠是否已空</summary>
        public bool IsEmpty => CurrentStack <= 0;

        /// <summary>堆叠是否已满</summary>
        public bool IsFull => CurrentStack >= MaxStack;

        #endregion

        #region Constructors

        public InventoryItem() { }

        public InventoryItem(string id)
        {
            Id = id;
        }

        public InventoryItem(string id, string name)
        {
            Id = id;
            Name = name;
        }

        #endregion

        #region Chain API (Template Building)

        public InventoryItem WithName(string name) { Name = name; return this; }
        public InventoryItem WithDescription(string desc) { Description = desc; return this; }
        public InventoryItem WithType(InventoryItemType type) { Type = type; return this; }
        public InventoryItem WithValue(int value) { Value = value; return this; }
        public InventoryItem WithIcon(string spriteId) { IconSpriteId = spriteId; return this; }
        public InventoryItem WithMaxStack(int max) { MaxStack = Math.Max(1, max); return this; }
        public InventoryItem WithCurrentStack(int current) { CurrentStack = current; return this; }

        #endregion

        #region Stack Operations

        /// <summary>判断两个物品是否可以堆叠</summary>
        public bool CanStackWith(InventoryItem other)
        {
            if (other == null) return false;
            if (Id != other.Id) return false;
            if (!IsStackable || !other.IsStackable) return false;
            return !IsFull;
        }

        /// <summary>
        /// 向当前堆叠加 amount 个，返回**实际加入**的数量
        /// </summary>
        public int TryAdd(int amount)
        {
            if (amount <= 0) return 0;
            int space = MaxStack - CurrentStack;
            int added = Math.Min(space, amount);
            CurrentStack += added;
            return added;
        }

        /// <summary>
        /// 从当前堆叠移除 amount 个，返回**实际移除**的数量
        /// </summary>
        public int TryRemove(int amount)
        {
            if (amount <= 0) return 0;
            int removed = Math.Min(CurrentStack, amount);
            CurrentStack -= removed;
            return removed;
        }

        /// <summary>
        /// 拆分堆叠：从当前堆叠移出 amount 个，返回新实例
        /// </summary>
        public InventoryItem Split(int amount)
        {
            if (amount <= 0 || amount >= CurrentStack) return null;
            var piece = CloneTemplate();
            piece.CurrentStack = amount;
            CurrentStack -= amount;
            return piece;
        }

        #endregion

        #region Cloning

        /// <summary>
        /// 从模板实例化：返回一个 CurrentStack=<paramref name="count"/> 的新实例
        /// </summary>
        public InventoryItem Instantiate(int count = 1)
        {
            var inst = CloneTemplate();
            inst.CurrentStack = Math.Min(Math.Max(1, count), inst.MaxStack);
            return inst;
        }

        /// <summary>浅拷贝模板字段（CurrentStack=0）</summary>
        public InventoryItem CloneTemplate()
        {
            return new InventoryItem
            {
                Id = Id,
                Name = Name,
                Description = Description,
                Type = Type,
                Value = Value,
                IconSpriteId = IconSpriteId,
                MaxStack = MaxStack,
                CurrentStack = 0
            };
        }

        #endregion

        public override string ToString() =>
            $"InventoryItem[{Id} {Name} x{CurrentStack}/{MaxStack}]";
    }
}
