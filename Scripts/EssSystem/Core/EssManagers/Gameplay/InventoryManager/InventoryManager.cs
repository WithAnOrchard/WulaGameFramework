using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
using UIMgr = EssSystem.Core.EssManagers.Presentation.UIManager.UIManager;   // I1: 避免魔法字符串 走常量

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager
{
    /// <summary>
    /// 背包门面 — 挂到场景里的单例 MonoBehaviour
    /// <para>
    /// 负责生命周期、注册默认模板/配置、UI 打开/关闭/缓存。<br/>
    /// 业务逻辑在 <see cref="InventoryService"/>；UI 构建/绑定在 <see cref="InventoryUIBuilder"/>。
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
        [Tooltip("是否启动时注册几个调试用默认模板（Sword/Apple）")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [Header("Editor Tools")]
        [Tooltip("Inspector「添加随机物品」按钮的目标 Inventory ID")]
        [SerializeField] private string _editorTargetInventoryId = "player";

        #endregion

        /// <summary>底层 Service（同等于 InventoryService.Instance，但 Inspector 里可见）</summary>
        public InventoryService Service => InventoryService.Instance;

        /// <summary>已打开 UI 的 inventoryId → slot 子组件引用集合，用于响应 EVT_CHANGED 原地更新。</summary>
        private readonly Dictionary<string, SlotUIRefs> _slotRefs = new Dictionary<string, SlotUIRefs>();

        /// <summary>已打开 UI 的描述面板组件引用，点击 slot 时填充。</summary>
        private readonly Dictionary<string, DescUIRefs> _descRefs = new Dictionary<string, DescUIRefs>();

        /// <summary>
        /// 已构建 UI 的根面板缓存 — 重新打开同一 inventory 时复用 GameObject，仅切换 <c>Visible</c>。
        /// 关闭 UI 实质是 SetVisible(false) 而非销毁，详见 <see cref="CloseInventoryUI"/>。
        /// </summary>
        private readonly Dictionary<string, UIPanelComponent> _rootPanels = new Dictionary<string, UIPanelComponent>();

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

        /// <summary>注册调试用默认物品模板。</summary>
        private void RegisterDefaultItemTemplates()
        {
            RegisterTemplateIfMissing(new InventoryItem("Sword", "铁剑")
                .WithType(InventoryItemType.Equipment).WithMaxStack(1).WithCurrentStack(1)
                .WithIcon("Sword").WithValue(50));

            RegisterTemplateIfMissing(new InventoryItem("Apple", "苹果")
                .WithType(InventoryItemType.Consumable).WithMaxStack(99).WithCurrentStack(1)
                .WithIcon("Apple").WithValue(10));

            Log("注册默认物品模板完成（仅创建缺失项）", Color.cyan);
        }

        /// <summary>仅在模板不存在时注册，避免每次启动覆盖磁盘上用户修改过的模板。</summary>
        private void RegisterTemplateIfMissing(InventoryItem template)
        {
            if (template == null || string.IsNullOrEmpty(template.Id)) return;
            if (Service.GetTemplate(template.Id) != null) return;
            Service.RegisterTemplate(template);
        }

        /// <summary>注册默认容器配置（玩家背包）。</summary>
        private void RegisterDefaultConfigs()
        {
            RegisterConfigIfMissing("PlayerBackPack", "玩家背包", BuildPlayerConfig);
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
                    .WithStartOffset(110f, 465f)
                    .WithSlotBackgroundId("Slot_1"))
                .WithPanelConfig(new PanelConfig(680f, 560f)
                    .WithPanelPosition(960f, 540f)
                    .WithPanelScale(1f, 1f)
                    .WithBackgroundId("背包背景")
                    .WithBackgroundColor(new Color(0.08f, 0.08f, 0.12f, 0.95f)))
                .WithCloseButtonConfig(new ButtonConfig(640f, 520f, 80f, 80f)
                    .WithScale(1f, 1f).WithText("×")
                    .WithSpriteId("Btn_Close")
                    .WithColor(new Color(1f, 0.3f, 0.3f, 1f))
                    .WithVisible(true).WithInteractable(true))
                .WithShowDescription(true)
                .WithDescriptionPanelConfig(new DescriptionPanelConfig(300f, 465f)
                    .WithOffset(-150f, 275f)
                    .WithBackgroundId("背包背景")
                    .WithTextPadding(40f, 40f)
                    .WithFontSize(14)
                    .WithTextColor(new Color(0.95f, 0.95f, 0.95f, 1f))
                    .WithEmptyPlaceholder("（点击物品查看描述）"))
                .WithShowTitle(true)
                .WithTitleConfig(new TitleConfig(420f, 40f)
                    .WithPosition(340f, 530f)        // 主面板顶部居中（680/2, 560-30）
                    .WithFontSize(22)
                    .WithTextColor(new Color(1f, 0.92f, 0.7f, 1f))
                    .WithAlignment(TextAnchor.MiddleCenter));

        /// <summary>创建调试用默认 Inventory（玩家）。</summary>
        private void CreateDefaultInventories()
        {
            Service.CreateInventory("player", "玩家背包", 30);
            Log("创建默认 Inventory: player", Color.cyan);
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

            // ① 缓存复用：之前构建过且 GameObject 仍然存活 → 直接 SetVisible(true)
            if (_rootPanels.TryGetValue(inventoryId, out var cached))
            {
                // I1: 走事件拿 GameObject —— 用常量不引用 UIManager.Entity 类
                var goResult = EventProcessor.Instance.TriggerEventMethod(
                    UIMgr.EVT_GET_UI_GAMEOBJECT,
                    new List<object> { cached.Id });
                var go = ResultCode.IsOk(goResult) && goResult.Count >= 2 ? goResult[1] as GameObject : null;
                if (go != null)
                {
                    cached.Visible = true;
                    Log($"复用缓存的Inventory UI: {inventoryId}", Color.green);
                    return ResultCode.Ok(inventoryId);
                }
                // 实体已销毁（外部触发 EVT_UNREGISTER 等）→ 清理缓存重建
                _rootPanels.Remove(inventoryId);
                _slotRefs.Remove(inventoryId);
                _descRefs.Remove(inventoryId);
            }

            var configId = data.Count >= 2 && data[1] is string s && !string.IsNullOrEmpty(s) ? s : inventoryId;
            var config = Service.GetConfig(configId);
            if (config == null) return ResultCode.Fail($"Inventory配置不存在: {configId}");

            var (panel, slotRefs, descRefs) = InventoryUIBuilder.BuildPanelTree(inventoryId, config);

            // I1: 通过事件调用 UIManager 注册实体（解耦）——用常量不引用 UIManager.Entity 类
            var result = EventProcessor.Instance.TriggerEventMethod(
                UIMgr.EVT_REGISTER_ENTITY,
                new List<object> { inventoryId, panel });

            if (!ResultCode.IsOk(result))
            {
                Log($"打开Inventory UI失败: {inventoryId}", Color.red);
                return ResultCode.Fail($"打开Inventory UI失败: {inventoryId}");
            }

            _slotRefs[inventoryId] = slotRefs;
            if (descRefs != null) _descRefs[inventoryId] = descRefs;
            _rootPanels[inventoryId] = panel;

            // UI 注册后挂上拖拽处理器（依赖 GameObject 已创建）
            InventoryUIBuilder.AttachSlotDragHandlers(inventoryId, config.SlotsPerPage);

            Log($"成功构建并打开Inventory UI: {inventoryId}", Color.green);
            return ResultCode.Ok(inventoryId);
        }

        /// <summary>
        /// 关闭指定 Inventory 的 UI — 仅隐藏不销毁，下次 Open 直接复用，避免重建开销。
        /// 真正销毁请用 <see cref="DestroyInventoryUI"/> 或外部直接触发 EVT_UNREGISTER_ENTITY。
        /// data: [inventoryId]
        /// </summary>
        [Event(EVT_CLOSE_UI)]
        public List<object> CloseInventoryUI(List<object> data)
        {
            if (!TryGetId(data, 0, out var inventoryId, out var fail)) return fail;

            if (_rootPanels.TryGetValue(inventoryId, out var panel))
            {
                panel.Visible = false;
                Log($"已隐藏Inventory UI: {inventoryId}", Color.green);
                return ResultCode.Ok(inventoryId);
            }

            // I1: 缓存里没有 → 容错路径 发 UNREGISTER 清理 UIManager 侧可能残留
            EventProcessor.Instance.TriggerEventMethod(
                UIMgr.EVT_UNREGISTER_ENTITY,
                new List<object> { inventoryId });
            Log($"关闭未缓存的Inventory UI: {inventoryId}", Color.yellow);
            return ResultCode.Ok(inventoryId);
        }

        /// <summary>真正销毁 UI（GameObject 也释放）。data: [inventoryId]</summary>
        public List<object> DestroyInventoryUI(List<object> data)
        {
            if (!TryGetId(data, 0, out var inventoryId, out var fail)) return fail;

            // I1: 用常量
            EventProcessor.Instance.TriggerEventMethod(
                UIMgr.EVT_UNREGISTER_ENTITY,
                new List<object> { inventoryId });

            _slotRefs.Remove(inventoryId);
            _descRefs.Remove(inventoryId);
            _rootPanels.Remove(inventoryId);

            Log($"销毁Inventory UI: {inventoryId}", Color.green);
            return ResultCode.Ok(inventoryId);
        }

        /// <summary>
        /// 监听 <see cref="InventoryService.EVT_CHANGED"/>，对已打开 UI 的容器原地刷新 slot 显示。
        /// args: [inventoryId, op, itemId, amount]
        /// </summary>
        [EventListener(InventoryService.EVT_CHANGED)]
        public List<object> OnInventoryChanged(string evt, List<object> args)
        {
            if (args == null || args.Count < 1) return null;
            var invId = args[0] as string;
            if (string.IsNullOrEmpty(invId)) return null;
            if (!_slotRefs.TryGetValue(invId, out var refs)) return null;

            var inv = Service?.GetInventory(invId);
            if (inv == null) return null;

            var slotCount = Mathf.Min(refs.Names.Length, refs.Stacks.Length);
            for (var i = 0; i < slotCount; i++)
            {
                var item = inv.GetSlot(i)?.Item;
                InventoryUIBuilder.ApplyItemToSlot(refs.Icons[i], refs.Names[i], refs.Stacks[i], item);
            }
            return null;
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

        #endregion

        #region Editor

        [ContextMenu("重新加载数据")]
        private void EditorReloadData()
        {
            Log("开始重新加载InventoryService数据", Color.yellow);
            Service.ReloadData();
            Log("InventoryService数据重新加载完成", Color.green);
        }

        /// <summary>
        /// Inspector 工具：从已注册的物品模板中随机挑一个，加入 <see cref="_editorTargetInventoryId"/> 指向的容器。
        /// 仅在 Play 模式下生效（依赖 Service 已初始化）。
        /// </summary>
        [ContextMenu("添加随机物品到目标容器")]
        private void EditorAddRandomItem()
        {
            if (string.IsNullOrEmpty(_editorTargetInventoryId))
            {
                Log("[Editor] 目标 Inventory ID 为空", Color.red);
                return;
            }
            if (Service == null)
            {
                Log("[Editor] Service 未初始化，请进入 Play 模式后再点", Color.red);
                return;
            }
            if (Service.GetInventory(_editorTargetInventoryId) == null)
            {
                Log($"[Editor] 容器不存在: {_editorTargetInventoryId}", Color.red);
                return;
            }

            var templates = new List<InventoryItem>(Service.GetAllTemplates());
            if (templates.Count == 0)
            {
                Log("[Editor] 没有已注册的物品模板", Color.yellow);
                return;
            }

            var picked = templates[UnityEngine.Random.Range(0, templates.Count)];
            var instance = picked.Instantiate(1);
            var result = Service.AddItem(_editorTargetInventoryId, instance, 1);

            Log($"[Editor] 向 {_editorTargetInventoryId} 添加随机物品 {picked.Id}({picked.Name}): {result}",
                result.Success ? Color.green : Color.red);
        }

        #endregion
    }
}
