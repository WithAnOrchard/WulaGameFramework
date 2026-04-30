using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.EssManager.InventoryManager.Dao;
using EssSystem.Core.EssManagers.UIManager.Dao.CommonComponents;

namespace EssSystem.EssManager.InventoryManager
{
    /// <summary>
    /// 背包门面 — 挂到场景里的单例 MonoBehaviour
    /// <para>
    /// 负责生命周期、注册默认模板、配置管理、UI 自动挂载。<br/>
    /// 绝大多数业务逻辑放在 <see cref="InventoryService"/> 里，本类仅转发或包薄。
    /// </para>
    /// </summary>
    [Manager(10)]
    public class InventoryManager : Manager<InventoryManager>
    {
        // ─── Event 名常量（供调用方使用）
        public const string EVT_OPEN_UI  = "OpenInventoryUI";
        public const string EVT_CLOSE_UI = "CloseInventoryUI";

        #region Inspector

        [Header("Default Templates (auto-registered)")]
        [Tooltip("是否启动时注册几个调试用默认模板（Potion/Sword）")]
        [SerializeField] private bool _registerDebugTemplates = true;

        #endregion

        /// <summary>底层 Service（同等于 InventoryService.Instance，但 Inspector 里可见）</summary>
        public InventoryService Service => InventoryService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates) RegisterDefaultItemTemplates();
            RegisterDefaultConfigs();
            CreateDefaultInventories();

            Log("InventoryManager 初始化完成", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        #endregion

        #region Defaults Registration

        /// <summary>注册调试用默认物品模板（药水/铁剑）。</summary>
        private void RegisterDefaultItemTemplates()
        {
            Service.RegisterTemplate(new InventoryItem("Potion", "生命药水")
                .WithType(InventoryItemType.Consumable).WithMaxStack(99).WithCurrentStack(1)
                .WithWeight(0.5f).WithValue(10));

            Service.RegisterTemplate(new InventoryItem("Sword", "铁剑")
                .WithType(InventoryItemType.Equipment).WithMaxStack(1).WithCurrentStack(1)
                .WithWeight(2.0f).WithValue(50));

            Log("注册默认物品模板: Potion, Sword", Color.cyan);
        }

        /// <summary>注册默认容器配置（玩家背包 / 箱子）。</summary>
        private void RegisterDefaultConfigs()
        {
            RegisterConfigIfMissing("PlayerBackPack", "玩家背包", BuildPlayerConfig);
            RegisterConfigIfMissing("Chest", "箱子", BuildChestConfig);
        }

        private void RegisterConfigIfMissing(string id, string label, System.Func<string, string, InventoryConfig> builder)
        {
            if (Service.HasData(InventoryService.CAT_CONFIGS, id))
            {
                Log($"{label}配置已存在，跳过注册", Color.yellow);
                return;
            }
            Service.RegisterConfig(builder(id, label));
            Log($"注册默认{label}配置", Color.cyan);
        }

        private static InventoryConfig BuildPlayerConfig(string id, string label) =>
            new InventoryConfig(id, label)
                .WithPageCount(1)
                .WithSlotsPerPage(30)
                .WithSlotConfig(new SlotConfig(80f, 80f, 6)
                    .WithSlotSpacing(12f, 12f)
                    .WithStartOffset(100f, 460f))
                .WithPanelConfig(new PanelConfig(680f, 560f)
                    .WithPanelPosition(960f, 540f)
                    .WithPanelScale(1f, 1f)
                    .WithBackgroundColor(new Color(0.08f, 0.08f, 0.12f, 0.95f)))
                .WithCloseButtonConfig(new ButtonConfig(640f, 520f, 36f, 36f)
                    .WithScale(1f, 1f).WithText("×")
                    .WithColor(new Color(1f, 0.3f, 0.3f, 1f))
                    .WithVisible(true).WithInteractable(true));

        private static InventoryConfig BuildChestConfig(string id, string label) =>
            new InventoryConfig(id, label)
                .WithPageCount(2)
                .WithSlotsPerPage(20)
                .WithSlotConfig(new SlotConfig(60f, 60f, 5)
                    .WithSlotSpacing(8f, 8f)
                    .WithStartOffset(-170f, 120f))
                .WithPanelConfig(new PanelConfig(500f, 400f)
                    .WithPanelPosition(0f, 0f)
                    .WithBackgroundColor(new Color(0.12f, 0.12f, 0.12f, 0.95f)))
                .WithCloseButtonConfig(new ButtonConfig(230f, 170f, 30f, 30f));

        /// <summary>创建调试用默认 Inventory（玩家 / 箱子）。</summary>
        private void CreateDefaultInventories()
        {
            Service.CreateInventory("player", "玩家背包", 30);
            Service.CreateInventory("chest", "箱子", 20);
            Log("创建默认 Inventory: player, chest", Color.cyan);
        }

        #endregion

        #region Event Methods (UI)

        /// <summary>打开指定 Inventory 的 UI。data: [inventoryId, configId?]</summary>
        [Event(EVT_OPEN_UI)]
        public List<object> OpenInventoryUI(List<object> data)
        {
            if (!TryGetId(data, 0, out var inventoryId, out var fail)) return fail;

            if (Service.GetInventory(inventoryId) == null)
                return ResultCode.Fail($"Inventory不存在: {inventoryId}");

            var configId = data.Count >= 2 && data[1] is string s && !string.IsNullOrEmpty(s) ? s : inventoryId;
            var config = Service.GetConfig(configId);
            if (config == null) return ResultCode.Fail($"Inventory配置不存在: {configId}");

            var panel = BuildPanelTree(inventoryId, config);

            // 通过事件调用 UIManager 注册实体（解耦）
            var result = EventProcessor.Instance.TriggerEventMethod(
                EssSystem.Core.EssManagers.UIManager.UIManager.EVT_REGISTER_ENTITY,
                new List<object> { inventoryId, panel });

            if (!ResultCode.IsOk(result))
            {
                Log($"打开Inventory UI失败: {inventoryId}", Color.red);
                return ResultCode.Fail($"打开Inventory UI失败: {inventoryId}");
            }
            Log($"成功打开Inventory UI: {inventoryId}", Color.green);
            return ResultCode.Ok(inventoryId);
        }

        /// <summary>关闭指定 Inventory 的 UI。data: [inventoryId]</summary>
        [Event(EVT_CLOSE_UI)]
        public List<object> CloseInventoryUI(List<object> data)
        {
            if (!TryGetId(data, 0, out var inventoryId, out var fail)) return fail;

            // 通过事件调用 UIManager 注销实体（解耦）
            EventProcessor.Instance.TriggerEventMethod(
                EssSystem.Core.EssManagers.UIManager.UIManager.EVT_UNREGISTER_ENTITY,
                new List<object> { inventoryId });

            Log($"成功关闭Inventory UI: {inventoryId}", Color.green);
            return ResultCode.Ok(inventoryId);
        }

        /// <summary>统一参数校验：从 data[index] 取非空字符串。</summary>
        private static bool TryGetId(List<object> data, int index, out string id, out List<object> failResult)
        {
            id = data != null && data.Count > index ? data[index] as string : null;
            if (string.IsNullOrEmpty(id))
            {
                failResult = ResultCode.Fail("参数无效");
                return false;
            }
            failResult = null;
            return true;
        }

        /// <summary>根据配置构建完整 UIPanel 树（panel + slots + close button）。</summary>
        private static UIPanelComponent BuildPanelTree(string inventoryId, InventoryConfig config)
        {
            var pc = config.PanelConfig;
            var panel = new UIPanelComponent(inventoryId, $"{config.DisplayName} - {inventoryId}")
                .SetPosition(pc.PanelPosition.x, pc.PanelPosition.y)
                .SetSize(pc.PanelWidth, pc.PanelHeight)
                .SetScale(pc.PanelScale.x, pc.PanelScale.y)
                .SetBackgroundSpriteId(pc.BackgroundSpriteId)
                .SetBackgroundColor(pc.BackgroundColor)
                .SetVisible(true);

            var sc = config.SlotConfig;
            for (var i = 0; i < config.SlotsPerPage; i++)
            {
                var row = i / sc.SlotsPerRow;
                var col = i % sc.SlotsPerRow;
                var x = sc.StartOffsetX + col * (sc.SlotWidth + sc.SlotSpacingX);
                var y = sc.StartOffsetY - row * (sc.SlotHeight + sc.SlotSpacingY);

                panel.AddChild(new UIButtonComponent($"{inventoryId}_Slot_{i}", $"Slot_{i}")
                    .SetPosition(x, y)
                    .SetSize(sc.SlotWidth, sc.SlotHeight)
                    .SetVisible(true)
                    .SetButtonSpriteId(sc.SlotBackgroundSpriteId)
                    .SetInteractable(true));
            }

            var cb = config.CloseButtonConfig;
            var closeBtn = new UIButtonComponent($"{inventoryId}_CloseButton", "CloseButton", cb.ButtonText)
                .SetPosition(cb.Position.x, cb.Position.y)
                .SetSize(cb.Size.x, cb.Size.y)
                .SetScale(cb.Scale.x, cb.Scale.y)
                .SetVisible(cb.IsVisible)
                .SetInteractable(cb.IsInteractable);

            // 关闭按钮点击 → 通过事件触发 CloseInventoryUI（与外部主动调用走同一路径）
            closeBtn.OnClick += _ => EventProcessor.Instance.TriggerEventMethod(
                EVT_CLOSE_UI, new List<object> { inventoryId });

            panel.AddChild(closeBtn);

            return panel;
        }

        #endregion

        #region Editor

        [ContextMenu("重新加载数据")]
        private void EditorReloadData()
        {
            Log("开始重新加载InventoryService数据", Color.yellow);
            Service.ReloadData();
            Log("InventoryService数据重新加载完成", Color.green);
        }

        #endregion
    }
}
