using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Manager;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager
{
    /// <summary>
    /// 背包门面 — 挂到场景里的单例 MonoBehaviour
    /// <para>
    /// 只负责：生命周期、确保玩家背包存在、注册默认模板、暴露对玩家背包的便利 API。<br/>
    /// 绝大多数业务逻辑放在 <see cref="InventoryService"/> 里，本类仅转发或包薄。
    /// </para>
    /// </summary>
    [Manager(5)]
    public class InventoryManager : Manager<InventoryManager>
    {
        #region Inspector

        [Header("Player Inventory")]
        [SerializeField] private string _playerInventoryId = "Player";
        [SerializeField] private string _playerInventoryName = "玩家背包";
        [SerializeField] private int _playerMaxSlots = 30;
        [SerializeField] private float _playerMaxWeight = 150f;

        [Header("Default Templates (auto-registered)")]
        [Tooltip("是否启动时注册几个调试用默认模板（Potion/Sword）")]
        [SerializeField] private bool _registerDebugTemplates = true;

        #endregion

        /// <summary>玩家主背包 ID（Inspector 可改）</summary>
        public string PlayerInventoryId => _playerInventoryId;

        /// <summary>底层 Service（同等于 InventoryService.Instance，但 Inspector 里可见）</summary>
        public InventoryService Service { get; private set; }

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            Service = InventoryService.Instance;

            Service.CreateInventory(_playerInventoryId, _playerInventoryName, _playerMaxSlots, _playerMaxWeight);

            if (_registerDebugTemplates) RegisterDebugTemplates();

            Log("InventoryManager 初始化完成", Color.green);
        }

        private void RegisterDebugTemplates()
        {
            Service.RegisterTemplate(new InventoryItem("potion_heal")
                .WithName("治疗药水").WithDescription("恢复 50 HP")
                .WithType(InventoryItemType.Consumable)
                .WithWeight(0.5f).WithValue(25).WithMaxStack(99));

            Service.RegisterTemplate(new InventoryItem("sword_iron")
                .WithName("铁剑").WithDescription("一把朴素的铁剑")
                .WithType(InventoryItemType.Weapon)
                .WithWeight(3f).WithValue(100));
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Player Inventory Shortcuts

        /// <summary>玩家主背包</summary>
        public Inventory PlayerInventory => Service.GetInventory(_playerInventoryId);

        /// <summary>给玩家发放 count 个模板物品</summary>
        public InventoryResult GivePlayer(string templateId, int count = 1)
        {
            var item = Service.InstantiateTemplate(templateId, count);
            if (item == null) return InventoryResult.Fail($"未知模板 {templateId}");
            return Service.AddItem(_playerInventoryId, item, count);
        }

        /// <summary>从玩家背包拿走 count 个 itemId</summary>
        public InventoryResult TakeFromPlayer(string itemId, int count = 1) =>
            Service.RemoveItem(_playerInventoryId, itemId, count);

        /// <summary>玩家背包里有多少个 itemId</summary>
        public int PlayerHas(string itemId) =>
            PlayerInventory?.CountOf(itemId) ?? 0;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Debug Menu (ContextMenu)

        [ContextMenu("Debug/Add Test Items")]
        private void DebugAddTestItems()
        {
            var p = GivePlayer("potion_heal", 10);
            var s = GivePlayer("sword_iron", 1);
            Log($"发放完成 — 药水: {p}, 剑: {s}", Color.yellow);
        }

        [ContextMenu("Debug/Show Player Inventory")]
        private void DebugShowPlayer()
        {
            var inv = PlayerInventory;
            if (inv == null) { LogWarning("玩家背包不存在"); return; }

            Log($"=== {inv.Name} ===", Color.yellow);
            Log($"槽位 {inv.UsedSlots}/{inv.MaxSlots}  权重 {inv.CurrentWeight:F1}/{inv.MaxWeight:F1}");
            foreach (var slot in inv.GetOccupiedSlots())
                Log($"  [#{slot.Index}] {slot.Item}");
        }

        [ContextMenu("Debug/Clear Player Inventory")]
        private void DebugClearPlayer()
        {
            var inv = PlayerInventory;
            if (inv == null) return;
            foreach (var slot in inv.Slots) slot.Clear();
            inv.Touch();
            Log("玩家背包已清空", Color.yellow);
        }

        #endregion
    }
}
