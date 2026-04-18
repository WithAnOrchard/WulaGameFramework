using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.Core.Manager;
using EssSystem.EssManager.InventoryManager.Dao;
using EssSystem.EssManager.InventoryManager.Entity;

namespace EssSystem.EssManager.InventoryManager
{
    /// <summary>
    /// 背包业务服务
    /// <list type="bullet">
    /// <item>所有持久化数据走 <c>_dataStorage</c>（由 DataService 自动扫描存档）</item>
    /// <item>Unity 端 Entity 注册表走内存字典，不参与序列化</item>
    /// <item>事件以 <c>[Event("Inventory*")]</c> 统一在此注册，供外部触发</item>
    /// </list>
    /// </summary>
    public class InventoryService : Service<InventoryService>
    {
        #region Categories (stored in _dataStorage, auto-persisted)

        public const string CAT_INVENTORIES = "Inventories";
        public const string CAT_TEMPLATES   = "Templates";

        #endregion

        #region Event Names

        public const string EVT_ADD     = "InventoryAdd";
        public const string EVT_REMOVE  = "InventoryRemove";
        public const string EVT_MOVE    = "InventoryMove";
        public const string EVT_CHANGED = "InventoryChanged";
        public const string EVT_QUERY   = "InventoryQuery";

        #endregion

        /// <summary>运行时 Entity 注册表（不持久化）</summary>
        private readonly Dictionary<string, InventoryEntity> _entities =
            new Dictionary<string, InventoryEntity>();

        protected override void Initialize()
        {
            base.Initialize();
            Log("InventoryService 初始化完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region Inventory CRUD

        /// <summary>创建新背包，返回 Inventory（若 Id 已存在则直接返回已有）</summary>
        public Inventory CreateInventory(string id, string name = null, int maxSlots = 20, float maxWeight = 100f)
        {
            var existing = GetInventory(id);
            if (existing != null) return existing;

            var inv = new Inventory(id, name, maxSlots, maxWeight);
            SetData(CAT_INVENTORIES, id, inv);
            Log($"新建背包 {id}（{maxSlots} 槽，权重 {maxWeight}）", Color.cyan);
            return inv;
        }

        /// <summary>按 ID 取背包，不存在返回 null</summary>
        public Inventory GetInventory(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return GetData<Inventory>(CAT_INVENTORIES, id);
        }

        /// <summary>枚举所有背包</summary>
        public IEnumerable<Inventory> GetAllInventories()
        {
            foreach (var key in GetKeys(CAT_INVENTORIES))
            {
                var inv = GetInventory(key);
                if (inv != null) yield return inv;
            }
        }

        /// <summary>删除背包</summary>
        public bool DeleteInventory(string id)
        {
            UnregisterEntity(id);
            return RemoveData(CAT_INVENTORIES, id);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Item Templates

        /// <summary>注册物品模板</summary>
        public void RegisterTemplate(InventoryItem template)
        {
            if (template == null || string.IsNullOrEmpty(template.Id))
            {
                LogWarning("忽略空模板或缺 Id 的模板");
                return;
            }
            SetData(CAT_TEMPLATES, template.Id, template);
            Log($"注册物品模板: {template.Id} ({template.Name})", Color.blue);
        }

        /// <summary>查模板</summary>
        public InventoryItem GetTemplate(string templateId) =>
            GetData<InventoryItem>(CAT_TEMPLATES, templateId);

        /// <summary>基于模板创建物品实例</summary>
        public InventoryItem InstantiateTemplate(string templateId, int count = 1)
        {
            var tpl = GetTemplate(templateId);
            return tpl?.Instantiate(count);
        }

        /// <summary>枚举所有模板</summary>
        public IEnumerable<InventoryItem> GetAllTemplates()
        {
            foreach (var key in GetKeys(CAT_TEMPLATES))
            {
                var tpl = GetTemplate(key);
                if (tpl != null) yield return tpl;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Core Operations

        /// <summary>向背包添加物品</summary>
        public InventoryResult AddItem(string inventoryId, InventoryItem item, int amount = 1)
        {
            var inv = GetInventory(inventoryId);
            if (inv == null) return InventoryResult.Fail("背包不存在");
            if (item == null || amount <= 0) return InventoryResult.Fail("物品或数量不合法");

            int remaining = amount;
            int added = 0;

            // 1) 先尝试堆叠到现有槽位
            if (item.IsStackable)
            {
                foreach (var slot in inv.GetOccupiedSlots())
                {
                    if (!slot.Item.CanStackWith(item)) continue;
                    int got = slot.Item.TryAdd(remaining);
                    added += got;
                    remaining -= got;
                    if (remaining <= 0) break;
                }
            }

            // 2) 剩余的塞到空槽
            if (remaining > 0)
            {
                foreach (var slot in inv.GetEmptySlots())
                {
                    if (remaining <= 0) break;
                    var piece = item.CloneTemplate();
                    piece.CurrentStack = Math.Min(remaining, piece.MaxStack);
                    slot.Item = piece;
                    added += piece.CurrentStack;
                    remaining -= piece.CurrentStack;
                }
            }

            inv.Touch();
            SetData(CAT_INVENTORIES, inventoryId, inv); // 触发持久化标记

            BroadcastChanged(inventoryId, "add", item.Id, added);

            return remaining > 0
                ? InventoryResult.Partial(added, remaining, "背包已满，部分未放入")
                : InventoryResult.Ok(added);
        }

        /// <summary>从背包移除物品</summary>
        public InventoryResult RemoveItem(string inventoryId, string itemId, int amount = 1)
        {
            var inv = GetInventory(inventoryId);
            if (inv == null) return InventoryResult.Fail("背包不存在");
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return InventoryResult.Fail("参数不合法");

            int remaining = amount;
            int removed = 0;

            foreach (var slot in inv.FindSlotsOf(itemId).Where(s => !s.Locked).ToList())
            {
                if (remaining <= 0) break;
                int got = slot.Item.TryRemove(remaining);
                removed += got;
                remaining -= got;
                if (slot.Item.IsEmpty) slot.Clear();
            }

            inv.Touch();
            SetData(CAT_INVENTORIES, inventoryId, inv);

            BroadcastChanged(inventoryId, "remove", itemId, removed);

            return remaining > 0
                ? InventoryResult.Partial(removed, remaining, "物品不足，未完全移除")
                : InventoryResult.Ok(removed);
        }

        /// <summary>在两个槽位之间移动物品（amount=-1 表示全部）</summary>
        public InventoryResult MoveItem(string inventoryId, int fromIdx, int toIdx, int amount = -1)
        {
            var inv = GetInventory(inventoryId);
            if (inv == null) return InventoryResult.Fail("背包不存在");

            var from = inv.GetSlot(fromIdx);
            var to   = inv.GetSlot(toIdx);
            if (from == null || to == null) return InventoryResult.Fail("槽位索引越界");
            if (from.IsEmpty) return InventoryResult.Fail("源槽为空");
            if (from.Locked || to.Locked) return InventoryResult.Fail("槽位被锁定");
            if (fromIdx == toIdx) return InventoryResult.Ok(0);

            int moveAmount = (amount < 0 || amount >= from.Item.CurrentStack)
                ? from.Item.CurrentStack : amount;

            if (to.IsEmpty)
            {
                // 空槽：整堆移动 or 拆堆
                if (moveAmount >= from.Item.CurrentStack)
                {
                    to.Item = from.Item;
                    from.Clear();
                }
                else
                {
                    to.Item = from.Item.Split(moveAmount);
                }
            }
            else if (from.Item.CanStackWith(to.Item))
            {
                // 堆叠
                int added = to.Item.TryAdd(moveAmount);
                from.Item.TryRemove(added);
                if (from.Item.IsEmpty) from.Clear();
            }
            else
            {
                // 交换
                (from.Item, to.Item) = (to.Item, from.Item);
            }

            inv.Touch();
            SetData(CAT_INVENTORIES, inventoryId, inv);

            BroadcastChanged(inventoryId, "move", null, moveAmount);

            return InventoryResult.Ok(moveAmount);
        }

        private void BroadcastChanged(string invId, string op, string itemId, int amount)
        {
            EventManager.Instance.TriggerEvent(EVT_CHANGED,
                new List<object> { invId, op, itemId ?? string.Empty, amount });
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Entity Registry (runtime only, not persisted)

        public void RegisterEntity(string inventoryId, InventoryEntity entity)
        {
            if (string.IsNullOrEmpty(inventoryId) || entity == null) return;
            _entities[inventoryId] = entity;
        }

        public InventoryEntity GetEntity(string inventoryId) =>
            (!string.IsNullOrEmpty(inventoryId) && _entities.TryGetValue(inventoryId, out var e)) ? e : null;

        public void UnregisterEntity(string inventoryId)
        {
            if (!string.IsNullOrEmpty(inventoryId)) _entities.Remove(inventoryId);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Event Handlers ([Event] auto-registered)

        /// <summary>事件: 向背包添加物品</summary>
        /// <param name="args">[inventoryId, itemIdOrItem, amount]</param>
        [Event(EVT_ADD)]
        public List<object> OnEventAdd(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 2) return Fail("参数不足");
                var invId = args[0]?.ToString();
                var amount = args.Count >= 3 ? Convert.ToInt32(args[2]) : 1;

                InventoryItem item = args[1] as InventoryItem;
                if (item == null && args[1] is string templateId)
                    item = InstantiateTemplate(templateId, amount);

                if (item == null) return Fail("未知物品或模板 ID");

                var result = AddItem(invId, item, amount);
                return result.Success ? Ok(result) : Fail(result.Message);
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        /// <summary>事件: 从背包移除物品</summary>
        /// <param name="args">[inventoryId, itemId, amount]</param>
        [Event(EVT_REMOVE)]
        public List<object> OnEventRemove(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 2) return Fail("参数不足");
                var result = RemoveItem(
                    args[0]?.ToString(),
                    args[1]?.ToString(),
                    args.Count >= 3 ? Convert.ToInt32(args[2]) : 1);
                return result.Success ? Ok(result) : Fail(result.Message);
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        /// <summary>事件: 移动物品</summary>
        /// <param name="args">[inventoryId, fromSlot, toSlot, amount]</param>
        [Event(EVT_MOVE)]
        public List<object> OnEventMove(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 3) return Fail("参数不足");
                var result = MoveItem(
                    args[0]?.ToString(),
                    Convert.ToInt32(args[1]),
                    Convert.ToInt32(args[2]),
                    args.Count >= 4 ? Convert.ToInt32(args[3]) : -1);
                return result.Success ? Ok(result) : Fail(result.Message);
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        /// <summary>事件: 查询背包</summary>
        /// <param name="args">[inventoryId]</param>
        [Event(EVT_QUERY)]
        public List<object> OnEventQuery(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 1) return Fail("参数不足");
                var inv = GetInventory(args[0]?.ToString());
                return inv == null ? Fail("背包不存在") : Ok(inv);
            }
            catch (Exception ex) { return Fail(ex.Message); }
        }

        private static List<object> Ok(object data)   => new List<object> { "成功", data };
        private static List<object> Fail(string msg)  => new List<object> { "错误", msg };

        #endregion
    }

    /// <summary>
    /// 背包操作结果 — 统一 Add/Remove/Move 的返回结构
    /// </summary>
    [Serializable]
    public readonly struct InventoryResult
    {
        public readonly bool   Success;
        public readonly int    Amount;      // 实际操作的数量
        public readonly int    Remaining;   // 剩余未处理
        public readonly string Message;

        public InventoryResult(bool success, int amount, int remaining, string message)
        {
            Success = success; Amount = amount; Remaining = remaining; Message = message ?? string.Empty;
        }

        public static InventoryResult Ok(int amount) =>
            new InventoryResult(true, amount, 0, "");

        public static InventoryResult Partial(int amount, int remaining, string msg) =>
            new InventoryResult(true, amount, remaining, msg);

        public static InventoryResult Fail(string msg) =>
            new InventoryResult(false, 0, 0, msg);

        public override string ToString() =>
            Success ? $"OK(+{Amount}, remaining={Remaining})" : $"FAIL({Message})";
    }
}
