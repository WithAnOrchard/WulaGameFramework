using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
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
    [Manager(5)]
    public class InventoryManager : Manager<InventoryManager>
    {
        #region Inspector

        [Header("Default Templates (auto-registered)")]
        [Tooltip("是否启动时注册几个调试用默认模板（Potion/Sword）")]
        [SerializeField] private bool _registerDebugTemplates = true;

        #endregion

        /// <summary>底层 Service（同等于 InventoryService.Instance，但 Inspector 里可见）</summary>
        public InventoryService Service => InventoryService.Instance;

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();

            // 从Service加载日志设置
            if (Service != null)
            {
                _serviceEnableLogging = Service.EnableLogging;
            }

            if (_registerDebugTemplates)
            {
                RegisterDefaultItemTemplates();
            }

            // 注册默认配置
            RegisterDefaultConfigs();

            // 创建默认Inventory用于测试
            CreateDefaultInventories();

            // 监听背包变化事件，自动刷新 UI
            RegisterInventoryChangedListener();

            // 注册UI事件监听器

            Log("InventoryManager 初始化完成", Color.green);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // 注销UI事件监听器
            // 应用退出时不需要注销背包变化监听器，所有对象都会被销毁
            // UnregisterInventoryChangedListener();
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service != null)
            {
                Service.UpdateInspectorInfo();
                _serviceInspectorInfo = Service.InspectorInfo;
            }
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null)
            {
                Service.EnableLogging = _serviceEnableLogging;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Config Management

        /// <summary>
        /// 注册默认物品模板
        /// </summary>
        private void RegisterDefaultItemTemplates()
        {
            // 生命药水
            var potion = new InventoryItem("Potion", "生命药水")
                .WithType(InventoryItemType.Consumable)
                .WithMaxStack(99)
                .WithCurrentStack(1)
                .WithWeight(0.5f)
                .WithValue(10);
            Service.RegisterTemplate(potion);

            // 铁剑
            var sword = new InventoryItem("Sword", "铁剑")
                .WithType(InventoryItemType.Equipment)
                .WithMaxStack(1)
                .WithCurrentStack(1)
                .WithWeight(2.0f)
                .WithValue(50);
            Service.RegisterTemplate(sword);

            Log($"注册默认物品模板: {potion.Name}, {sword.Name}", Color.cyan);
        }

        /// <summary>
        /// 注册默认容器配置
        /// </summary>
        private void RegisterDefaultConfigs()
        {
            // 玩家背包配置
            if (!Service.HasData(InventoryService.CAT_CONFIGS, "PlayerBackPack"))
            {
                var playerSlotConfig = new SlotConfig(80f, 80f, 6)
                    .WithSlotSpacing(12f, 12f)
                    .WithStartOffset(100f, 460f);
                var playerPanelConfig = new PanelConfig(680f, 560f)
                    .WithPanelPosition(960f, 540f)
                    .WithPanelScale(1f, 1f)
                    .WithBackgroundColor(new Color(0.08f, 0.08f, 0.12f, 0.95f));
                var playerButtonConfig = new ButtonConfig(640f, 520f, 36f, 36f)
                    .WithScale(1f, 1f)
                    .WithText("×")
                    .WithColor(new Color(1f, 0.3f, 0.3f, 1f))
                    .WithVisible(true)
                    .WithInteractable(true);

                var playerConfig = new InventoryConfig("PlayerBackPack", "玩家背包")
                    .WithPageCount(1)
                    .WithSlotsPerPage(30)
                    .WithSlotConfig(playerSlotConfig)
                    .WithPanelConfig(playerPanelConfig)
                    .WithCloseButtonConfig(playerButtonConfig);
                Service.RegisterConfig(playerConfig);
                Log("注册默认玩家背包配置", Color.cyan);
            }
            else
            {
                Log("玩家背包配置已存在，跳过注册", Color.yellow);
            }

            // 箱子配置
            if (!Service.HasData(InventoryService.CAT_CONFIGS, "Chest"))
            {
                var chestSlotConfig = new SlotConfig(60f, 60f, 5)
                    .WithSlotSpacing(8f, 8f)
                    .WithStartOffset(-170f, 120f);
                var chestPanelConfig = new PanelConfig(500f, 400f)
                    .WithPanelPosition(0f, 0f) // 屏幕中央
                    .WithBackgroundColor(new Color(0.12f, 0.12f, 0.12f, 0.95f));
                var chestButtonConfig = new ButtonConfig(230f, 170f, 30f, 30f);

                var chestConfig = new InventoryConfig("Chest", "箱子")
                    .WithPageCount(2)
                    .WithSlotsPerPage(20)
                    .WithSlotConfig(chestSlotConfig)
                    .WithPanelConfig(chestPanelConfig)
                    .WithCloseButtonConfig(chestButtonConfig);
                Service.RegisterConfig(chestConfig);
                Log("注册默认箱子配置", Color.cyan);
            }
            else
            {
                Log("箱子配置已存在，跳过注册", Color.yellow);
            }
        }


        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Default Inventories

        /// <summary>
        /// 创建默认Inventory用于测试
        /// </summary>
        private void CreateDefaultInventories()
        {
            // 创建玩家背包
            var playerInventory = Service.CreateInventory("player", "玩家背包", 30);
            Log($"创建默认Inventory: {playerInventory.Id}", Color.cyan);

            // 创建箱子Inventory
            var chestInventory = Service.CreateInventory("chest", "箱子", 20);
            Log($"创建默认Inventory: {chestInventory.Id}", Color.cyan);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Event Methods (UI Operations)

        /// <summary>
        /// 开启指定Inventory的UI
        /// </summary>
        [Event("OpenInventoryUI")]
        public List<object> OpenInventoryUI(List<object> data)
        {
            if (data == null || data.Count < 1)
            {
                return Fail("参数无效");
            }

            string inventoryId = data[0] as string;
            if (string.IsNullOrEmpty(inventoryId))
            {
                return Fail("Inventory ID 不能为空");
            }

            // 获取Inventory
            var inventory = Service.GetInventory(inventoryId);
            if (inventory == null)
            {
                return Fail($"Inventory不存在: {inventoryId}");
            }

            // 获取ConfigId，如果没有提供则使用inventoryId
            string configId = data.Count >= 2 ? data[1] as string : inventoryId;
            if (string.IsNullOrEmpty(configId))
            {
                configId = inventoryId;
            }

            // 获取Inventory配置
            var config = Service.GetConfig(configId);
            if (config == null)
            {
                return Fail($"Inventory配置不存在: {configId}");
            }

            // 创建UI面板组件
            var panel = new UIPanelComponent(inventoryId, $"{config.DisplayName} - {inventoryId}")
                .SetPosition(config.PanelConfig.PanelPosition.x, config.PanelConfig.PanelPosition.y)
                .SetSize(config.PanelConfig.PanelWidth, config.PanelConfig.PanelHeight)
                .SetScale(config.PanelConfig.PanelScale.x, config.PanelConfig.PanelScale.y)
                .SetBackgroundSpriteId(config.PanelConfig.BackgroundSpriteId)
                .SetBackgroundColor(config.PanelConfig.BackgroundColor)
                .SetVisible(true);

            // 创建Slot组件
            var slotConfig = config.SlotConfig;
            for (int i = 0; i < config.SlotsPerPage; i++)
            {
                int row = i / slotConfig.SlotsPerRow;
                int col = i % slotConfig.SlotsPerRow;
                float x = slotConfig.StartOffsetX + col * (slotConfig.SlotWidth + slotConfig.SlotSpacingX);
                float y = slotConfig.StartOffsetY - row * (slotConfig.SlotHeight + slotConfig.SlotSpacingY);

                var slotId = $"{inventoryId}_Slot_{i}";
                var slot = new UIButtonComponent(slotId, $"Slot_{i}")
                    .SetPosition(x, y)
                    .SetSize(slotConfig.SlotWidth, slotConfig.SlotHeight)
                    .SetVisible(true)
                    .SetButtonSpriteId(slotConfig.SlotBackgroundSpriteId)
                    .SetInteractable(true);

                panel.AddChild(slot);
            }

            // 创建关闭按钮组件
            var closeButtonConfig = config.CloseButtonConfig;
            var closeButtonId = $"{inventoryId}_CloseButton";
            var closeButton = new UIButtonComponent(closeButtonId, "CloseButton", closeButtonConfig.ButtonText)
                .SetPosition(closeButtonConfig.Position.x, closeButtonConfig.Position.y)
                .SetSize(closeButtonConfig.Size.x, closeButtonConfig.Size.y)
                .SetScale(closeButtonConfig.Scale.x, closeButtonConfig.Scale.y)
                .SetVisible(closeButtonConfig.IsVisible)
                .SetInteractable(closeButtonConfig.IsInteractable);

            panel.AddChild(closeButton);

            // 通过EventProcessor注册UI实体到UIManager
            var result = EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity",
                new List<object> { inventoryId, panel });

            if (result != null && result.Count >= 1 && result[0].ToString() == "成功")
            {
                Log($"成功打开Inventory UI: {inventoryId}", Color.green);
                return Ok(inventoryId);
            }
            else
            {
                Log($"打开Inventory UI失败: {inventoryId}", Color.red);
                return Fail($"打开Inventory UI失败: {inventoryId}");
            }
        }

        /// <summary>
        /// 关闭指定Inventory的UI
        /// </summary>
        [Event("CloseInventoryUI")]
        public List<object> CloseInventoryUI(List<object> data)
        {
            if (data == null || data.Count < 1)
            {
                return Fail("参数无效");
            }

            string inventoryId = data[0] as string;
            if (string.IsNullOrEmpty(inventoryId))
            {
                return Fail("Inventory ID 不能为空");
            }

            // 通过EventProcessor注销UI实体
            var result = EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity",
                new List<object> { inventoryId });

            if (result != null && result.Count >= 1 && result[0].ToString() == "成功")
            {
                Log($"成功关闭Inventory UI: {inventoryId}", Color.green);
                return Ok(inventoryId);
            }
            else
            {
                return Fail($"关闭Inventory UI失败: {inventoryId}");
            }
        }

        #endregion

        private static List<object> Ok(object data) => new List<object> { "成功", data };
        private static List<object> Fail(string msg) => new List<object> { "错误", msg };

        #region Editor Methods

        /// <summary>
        /// 在Inspector中重新加载InventoryService数据
        /// </summary>
        [ContextMenu("重新加载数据")]
        private void EditorReloadData()
        {
            Log("开始重新加载InventoryService数据", Color.yellow);
            Service.ReloadData();
            Log("InventoryService数据重新加载完成", Color.green);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Auto UI Refresh

        /// <summary>
        /// 注册背包变化监听器
        /// </summary>
        private void RegisterInventoryChangedListener()
        {
            if (EventManager.HasInstance)
            {
                EventManager.Instance.AddListener(InventoryService.EVT_CHANGED, OnInventoryChanged);
            }
        }

        /// <summary>
        /// 注销背包变化监听器
        /// </summary>
        private void UnregisterInventoryChangedListener()
        {
            if (EventManager.HasInstance && EventManager.Instance != null)
            {
                EventManager.Instance.RemoveListener(InventoryService.EVT_CHANGED, OnInventoryChanged);
            }
        }

        /// <summary>
        /// 背包变化事件处理器 - UI刷新由UIManager统一管理
        /// </summary>
        private List<object> OnInventoryChanged(string eventName, List<object> data)
        {
            // UI刷新逻辑由UIManager统一处理
            return new List<object>();
        }

        #endregion
    }
}
