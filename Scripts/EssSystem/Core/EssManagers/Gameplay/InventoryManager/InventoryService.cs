using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager
{
    /// <summary>
    /// 背包业务服务
    /// <list type="bullet">
    /// <item>所有持久化数据走 <c>_dataStorage</c>（由 DataService 自动扫描存档）</item>
    /// <item>Unity 端 Entity 注册表走内存字典，不参与序列化</item>
    /// <item>对 Inventory 及其内部物品进行操作（添加、移除、移动）</item>
    /// <item>事件以 <c>[Event(EVT_*)]</c> 常量引用注册（见 #region 事件名称），供外部触发</item>
    /// </list>
    /// </summary>
    public class InventoryService : Service<InventoryService>
    {
        #region 数据分类（存储在 _dataStorage 中，自动持久化）

        public const string CAT_INVENTORIES = "Inventories";
        public const string CAT_TEMPLATES   = "Items";
        public const string CAT_CONFIGS     = "Configs";

        #endregion

        #region 事件名称

        public const string EVT_CREATE  = "InventoryCreate";
        public const string EVT_DELETE  = "InventoryDelete";
        public const string EVT_ADD     = "InventoryAdd";
        public const string EVT_REMOVE  = "InventoryRemove";
        public const string EVT_MOVE    = "InventoryMove";
        public const string EVT_CHANGED = "InventoryChanged";
        public const string EVT_QUERY   = "InventoryQuery";
        public const string EVT_OPEN_UI = "OnOpenInventoryUI";
        public const string EVT_CLOSE_UI = "OnCloseInventoryUI";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("InventoryService 初始化完成", Color.green);
        }

        /// <summary>
        /// 热重载Service数据
        /// </summary>
        public void ReloadData()
        {
            // 只清空配置数据，保留 Inventory 数据
            if (_dataStorage.TryGetValue(CAT_CONFIGS, out var cfg)) cfg.Clear();

            // 重新加载数据
            LoadData();
            // I4: 直接 mutate _dataStorage 了，手动标 Inspector dirty。
            MarkInspectorDirty();

            Log("InventoryService 配置热重载完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region Config Management

        /// <summary>注册容器配置</summary>
        public void RegisterConfig(InventoryConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空配置或缺 ConfigId 的配置");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册容器配置: {config.ConfigId} ({config.DisplayName})", Color.blue);
        }

        /// <summary>获取容器配置</summary>
        public InventoryConfig GetConfig(string configId) =>
            GetData<InventoryConfig>(CAT_CONFIGS, configId);

        /// <summary>枚举所有配置</summary>
        public IEnumerable<InventoryConfig> GetAllConfigs()
        {
            foreach (var key in GetKeys(CAT_CONFIGS))
            {
                var config = GetConfig(key);
                if (config != null) yield return config;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Inventory CRUD

        /// <summary>创建新容器，返回 Inventory（若 Id 已存在则直接返回已有）</summary>
        public Inventory CreateInventory(string id, string name = null, int maxSlots = 20)
        {
            var existing = GetInventory(id);
            if (existing != null) return existing;

            var inv = new Inventory(id, name, maxSlots);
            SetData(CAT_INVENTORIES, id, inv);
            Log($"新建容器 {id}（{maxSlots} 槽）", Color.cyan);
            return inv;
        }

        /// <summary>按 ID 取容器，不存在返回 null</summary>
        public Inventory GetInventory(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return GetData<Inventory>(CAT_INVENTORIES, id);
        }

        /// <summary>枚举所有容器</summary>
        public IEnumerable<Inventory> GetAllInventories()
        {
            foreach (var key in GetKeys(CAT_INVENTORIES))
            {
                var inv = GetInventory(key);
                if (inv != null) yield return inv;
            }
        }

        /// <summary>删除容器</summary>
        public bool DeleteInventory(string id)
        {
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

        /// <summary>向容器添加物品</summary>
        public InventoryResult AddItem(string inventoryId, InventoryItem item, int amount = 1)
        {
            var inv = GetInventory(inventoryId);
            if (inv == null) return InventoryResult.Fail("容器不存在");
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
                ? InventoryResult.Partial(added, remaining, "容器已满，部分未放入")
                : InventoryResult.Ok(added);
        }

        /// <summary>从容器移除物品</summary>
        public InventoryResult RemoveItem(string inventoryId, string itemId, int amount = 1)
        {
            var inv = GetInventory(inventoryId);
            if (inv == null) return InventoryResult.Fail("容器不存在");
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

        /// <summary>同容器槽位间移动物品（amount=-1 表示全部）—— 转发到跨容器版本。</summary>
        public InventoryResult MoveItem(string inventoryId, int fromIdx, int toIdx, int amount = -1) =>
            MoveItem(inventoryId, fromIdx, inventoryId, toIdx, amount);

        /// <summary>
        /// 跨容器（或同容器）槽位间移动物品（amount=-1 表示全部）。
        /// <para>支持 3 种行为：空槽搬运 / 同 itemId 堆叠 / 异 item 交换。</para>
        /// </summary>
        public InventoryResult MoveItem(string fromInventoryId, int fromIdx, string toInventoryId, int toIdx, int amount = -1)
        {
            var fromInv = GetInventory(fromInventoryId);
            var toInv   = GetInventory(toInventoryId);
            if (fromInv == null || toInv == null) return InventoryResult.Fail("容器不存在");

            var from = fromInv.GetSlot(fromIdx);
            var to   = toInv.GetSlot(toIdx);
            if (from == null || to == null) return InventoryResult.Fail("槽位索引越界");
            if (from.IsEmpty) return InventoryResult.Fail("源槽为空");
            if (from.Locked || to.Locked) return InventoryResult.Fail("槽位被锁定");
            if (ReferenceEquals(from, to)) return InventoryResult.Ok(0);

            int moveAmount = (amount < 0 || amount >= from.Item.CurrentStack)
                ? from.Item.CurrentStack : amount;

            if (to.IsEmpty)
            {
                // 空槽：整堆搬运 or 拆堆
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
                // 交换（跨容器也合法）
                (from.Item, to.Item) = (to.Item, from.Item);
            }

            fromInv.Touch();
            SetData(CAT_INVENTORIES, fromInventoryId, fromInv);
            BroadcastChanged(fromInventoryId, "move", null, moveAmount);

            if (fromInventoryId != toInventoryId)
            {
                toInv.Touch();
                SetData(CAT_INVENTORIES, toInventoryId, toInv);
                BroadcastChanged(toInventoryId, "move", null, moveAmount);
            }

            return InventoryResult.Ok(moveAmount);
        }

        private void BroadcastChanged(string invId, string op, string itemId, int amount)
        {
            EventProcessor.Instance.TriggerEvent(EVT_CHANGED,
                new List<object> { invId, op, itemId, amount });
        }

        /// <summary>事件: 创建容器</summary>
        /// <param name="args">[id, name, maxSlots]</param>
        // I2: 遵项目规范 “[Event] 动词开头”，去除 OnEvent 前缀。字符串不变。
        [Event(EVT_CREATE)]
        public List<object> Create(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 1) return ResultCode.Fail("参数不足");
                var id = args[0]?.ToString();
                var name = args.Count >= 2 ? args[1]?.ToString() : null;
                var maxSlots = args.Count >= 3 ? Convert.ToInt32(args[2]) : 20;

                var inv = CreateInventory(id, name, maxSlots);
                return inv == null ? ResultCode.Fail("创建容器失败") : ResultCode.Ok(inv);
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }

        /// <summary>事件: 删除容器</summary>
        /// <param name="args">[id]</param>
        [Event(EVT_DELETE)]
        public List<object> Delete(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 1) return ResultCode.Fail("参数不足");
                var id = args[0]?.ToString();
                var result = DeleteInventory(id);
                return result ? ResultCode.Ok("删除成功") : ResultCode.Fail("删除失败");
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }

        /// <summary>事件: 向容器添加物品</summary>
        /// <param name="args">[inventoryId, itemIdOrItem, amount]</param>
        [Event(EVT_ADD)]
        public List<object> Add(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 2) return ResultCode.Fail("参数不足");
                var invId = args[0]?.ToString();
                var amount = args.Count >= 3 ? Convert.ToInt32(args[2]) : 1;

                InventoryItem item = args[1] as InventoryItem;
                if (item == null && args[1] is string templateId)
                    item = InstantiateTemplate(templateId, amount);

                if (item == null) return ResultCode.Fail("未知物品或模板 ID");

                var result = AddItem(invId, item, amount);
                return result.Success ? ResultCode.Ok(result) : ResultCode.Fail(result.Message);
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }

        /// <summary>事件: 从容器移除物品</summary>
        /// <param name="args">[inventoryId, itemId, amount]</param>
        [Event(EVT_REMOVE)]
        public List<object> Remove(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 2) return ResultCode.Fail("参数不足");
                var result = RemoveItem(
                    args[0]?.ToString(),
                    args[1]?.ToString(),
                    args.Count >= 3 ? Convert.ToInt32(args[2]) : 1);
                return result.Success ? ResultCode.Ok(result) : ResultCode.Fail(result.Message);
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }

        /// <summary>事件: 移动物品。支持 2 种签名（按 args[2] 类型区分）：</summary>
        /// <param name="args">
        /// <list type="bullet">
        /// <item>同容器：<c>[inventoryId(string), fromSlot(int), toSlot(int), amount(int?)]</c></item>
        /// <item>跨容器：<c>[fromInventoryId(string), fromSlot(int), toInventoryId(string), toSlot(int), amount(int?)]</c></item>
        /// </list>
        /// </param>
        [Event(EVT_MOVE)]
        public List<object> Move(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 3) return ResultCode.Fail("参数不足");

                InventoryResult result;
                // 跨容器形：args[2] 是 string（toInventoryId）
                if (args.Count >= 4 && args[2] is string toInv && !string.IsNullOrEmpty(toInv))
                {
                    result = MoveItem(
                        args[0]?.ToString(),
                        Convert.ToInt32(args[1]),
                        toInv,
                        Convert.ToInt32(args[3]),
                        args.Count >= 5 ? Convert.ToInt32(args[4]) : -1);
                }
                else
                {
                    // 同容器形：args[2] 是 int（toSlot）
                    result = MoveItem(
                        args[0]?.ToString(),
                        Convert.ToInt32(args[1]),
                        Convert.ToInt32(args[2]),
                        args.Count >= 4 ? Convert.ToInt32(args[3]) : -1);
                }
                return result.Success ? ResultCode.Ok(result) : ResultCode.Fail(result.Message);
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }

        /// <summary>事件: 查询容器</summary>
        /// <param name="args">[inventoryId]</param>
        [Event(EVT_QUERY)]
        public List<object> Query(List<object> args)
        {
            try
            {
                if (args == null || args.Count < 1) return ResultCode.Fail("参数不足");
                var inv = GetInventory(args[0]?.ToString());
                return inv == null ? ResultCode.Fail("容器不存在") : ResultCode.Ok(inv);
            }
            catch (Exception ex) { return ResultCode.Fail(ex.Message); }
        }


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
        public readonly int    Remaining;   // 剩余未处理数量
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
            Success ? $"成功(+{Amount}, 剩余={Remaining})" : $"失败({Message})";
    }
}
