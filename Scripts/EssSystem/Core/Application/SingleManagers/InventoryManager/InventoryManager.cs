using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Runtime;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
// §4.1 跨模块调用走 bare-string 协议，不 using UIManager 获得运行时零跨模块依赖

namespace EssSystem.Core.Application.SingleManagers.InventoryManager
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
        public const string EVT_OPEN_UI    = "OpenInventoryUI";
        public const string EVT_CLOSE_UI   = "CloseInventoryUI";
        public const string EVT_REGISTER_ITEM = "InventoryRegisterItem";
        public const string EVT_REGISTER_PICKABLE_ITEM = "InventoryRegisterPickableItem";
        public const string EVT_SPAWN_PICKABLE_ITEM = "InventorySpawnPickableItem";
        /// <summary>快捷栏使用事件：玩家按下 1~9 时广播。args: [string inventoryId, int slotIndex, InventoryItem item|null]</summary>
        public const string EVT_HOTBAR_USE = "InventoryHotbarUse";

        // ─── 默认容器 ID（代码侧默认创建的 3 个）
        public const string ID_PLAYER       = "player";
        public const string ID_HOTBAR       = "hotbar";
        public const string ID_EQUIPMENT    = "equipment";
        // ─── 默认 ConfigId
        private const string CFG_PLAYER     = "PlayerBackPack";
        private const string CFG_HOTBAR     = "Hotbar";
        private const string CFG_EQUIPMENT  = "PlayerEquipment";

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

        private readonly Dictionary<string, PickableItemDefinition> _pickableDefinitions = new Dictionary<string, PickableItemDefinition>();

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

        /// <summary>Awake 之后所有 Manager / EventProcessor 都注册完了，在 Start 里安全自动打开 Hotbar。</summary>
        private void Start()
        {
            // 启动后自动打开 Hotbar——快捷栏一直可见
            var result = OpenInventoryUI(new List<object> { ID_HOTBAR, CFG_HOTBAR });
            if (!ResultCode.IsOk(result))
                Log($"自动打开 Hotbar 失败: {(result != null && result.Count > 1 ? result[1] : "(unknown)")}", Color.red);
        }

        /// <summary>
        /// 监听 1~9 按键 + 调用 base Update（Inspector 同步 / logging sync）。
        /// </summary>
        protected override void Update()
        {
            base.Update();
            if (Service == null) return;
            for (var i = 0; i < 9; i++)
            {
                if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                var inv  = Service.GetInventory(ID_HOTBAR);
                var item = inv?.GetSlot(i)?.Item;
                EventProcessor.Instance.TriggerEvent(EVT_HOTBAR_USE,
                    new List<object> { ID_HOTBAR, i, item });
            }
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

        /// <summary>注册默认容器配置（玩家背包 / 快捷栏 / 装备栏）。
        /// <para>仅当持久化中不存在时注册；已有持久化数据时尊重用户/存档侧配置，不覆盖。</para>
        /// </summary>
        private void RegisterDefaultConfigs()
        {
            RegisterConfigIfMissing(CFG_PLAYER,    "玩家背包", BuildPlayerConfig);
            RegisterConfigIfMissing(CFG_HOTBAR,    "快捷栏",   BuildHotbarConfig);
            RegisterConfigIfMissing(CFG_EQUIPMENT, "装备栏",   BuildEquipmentConfig);
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
                .WithPanelConfig(new UIPanelSpec(680f, 560f)
                    // 主面板向右偏移 150：补偿左侧伸出的 300 宽描述子面板，
                    // 让 (主面板 + 描述子面板) 组合 bounding-box 在 1920×1080 参考分辨率下水平居中
                    .WithPosition(1110f, 540f)
                    .WithScale(1f, 1f)
                    .WithBackgroundId("背包背景")
                    .WithBackgroundColor(new Color(0.08f, 0.08f, 0.12f, 0.95f)))
                .WithCloseButtonConfig(new UIButtonSpec(640f, 520f, 80f, 80f)
                    .WithScale(1f, 1f).WithText("×")
                    .WithSpriteId("Btn_Close")
                    .WithColor(new Color(1f, 0.3f, 0.3f, 1f))
                    .WithVisible(true).WithInteractable(true))
                .WithShowDescription(true)
                .WithDescriptionPanelConfig(new DescriptionPanelConfig(300f, 465f)
                    .WithOffset(-150f, 275f)
                    .WithBackgroundId("背包背景")
                    .WithTextPadding(40f, 40f)
                    .WithFontSize(20)
                    .WithTextColor(new Color(0.95f, 0.95f, 0.95f, 1f))
                    .WithEmptyPlaceholder("（点击物品查看描述）")
                    // 内部图标 / 文本整体在 300 宽面板内右移 ~25px（避免贴左边缘），同步收窄宽度避免溢出
                    .WithIconConfig(new UIIconSpec()
                        .WithPosition(150f, 370f)
                        .WithSize(112f, 112f))
                    .WithNameConfig(new UITextSpec
                    {
                        Position  = new Vector2(150f, 300f),
                        Size      = new Vector2(260f, 48f),
                        FontSize  = 26,
                        TextColor = new Color(0.95f, 0.95f, 0.95f, 1f),
                        Alignment = TextAnchor.MiddleCenter,
                    })
                    .WithStackConfig(new UITextSpec
                    {
                        Position  = new Vector2(150f, 260f),
                        Size      = new Vector2(260f, 36f),
                        FontSize  = 20,
                        TextColor = new Color(1f, 0.85f, 0.4f, 1f),
                        Alignment = TextAnchor.MiddleCenter,
                    })
                    .WithDescTextConfig(new UITextSpec
                    {
                        Position  = new Vector2(150f, 125f),
                        Size      = new Vector2(250f, 190f),
                        FontSize  = 20,
                        TextColor = new Color(0.95f, 0.95f, 0.95f, 1f),
                        Alignment = TextAnchor.MiddleCenter,
                    }))
                .WithShowTitle(true)
                .WithTitleConfig(new UITextSpec(420f, 40f)
                    .WithPosition(340f, 530f)        // 主面板顶部居中（680/2, 560-30）
                    .WithFontSize(22)
                    .WithTextColor(new Color(1f, 0.92f, 0.7f, 1f))
                    .WithAlignment(TextAnchor.MiddleCenter));

        /// <summary>快捷栏配置：9 格 × 1 行，常驻屏幕底部居中，无标题/描述/关闭按钮。</summary>
        private static InventoryConfig BuildHotbarConfig(string id, string label) =>
            new InventoryConfig(id, label)
                .WithPageCount(1)
                .WithSlotsPerPage(9)
                .WithSlotConfig(new SlotConfig(70f, 70f, 9)
                    .WithSlotSpacing(8f, 8f)
                    .WithStartOffset(78f, 50f)             // 780 宽：左右各 43 padding + 半槽 35 = 78；垂直居中 100/2
                    .WithSlotBackgroundId("Slot_1"))
                .WithPanelConfig(new UIPanelSpec(780f, 100f)
                    .WithPosition(960f, 80f)          // 1920×1080 底部居中（上沿距屏底 30）
                    .WithScale(1f, 1f)
                    .WithBackgroundId("背包背景")
                    .WithBackgroundColor(new Color(0.08f, 0.08f, 0.12f, 0.85f)))
                .WithCloseButtonConfig(new UIButtonSpec(0f, 0f, 1f, 1f).WithVisible(false).WithInteractable(false))
                .WithShowTitle(false)
                .WithShowDescription(false);

        /// <summary>装备栏配置：5 格 × 1 列（头盔/盔甲/护腿/鞋子/背包），插在玩家背包右侧。</summary>
        private static InventoryConfig BuildEquipmentConfig(string id, string label) =>
            new InventoryConfig(id, label)
                .WithPageCount(1)
                .WithSlotsPerPage(5)
                .WithSlotConfig(new SlotConfig(80f, 80f, 1)
                    .WithSlotSpacing(0f, 12f)
                    .WithStartOffset(60f, 465f)            // 120 宽 → 居中 60；标题下方开始，5 格完整落在面板内
                    .WithSlotBackgroundId("Slot_1"))
                .WithPanelConfig(new UIPanelSpec(120f, 550f)
                    .WithPosition(1520f, 540f)        // PlayerBackPack 右边沿 1450 + 10 间隔 + 60 半宽 = 1520，留 10
                    .WithScale(1f, 1f)
                    .WithBackgroundId("背包背景")
                    .WithBackgroundColor(new Color(0.08f, 0.08f, 0.12f, 0.95f)))
                .WithCloseButtonConfig(new UIButtonSpec(0f, 0f, 1f, 1f).WithVisible(false).WithInteractable(false))
                .WithShowTitle(true)
                .WithTitleConfig(new UITextSpec(110f, 30f)
                    .WithPosition(60f, 545f)               // 120 宽中心 60，500 高顶部 -15
                    .WithFontSize(16)
                    .WithTextColor(new Color(1f, 0.92f, 0.7f, 1f))
                    .WithAlignment(TextAnchor.MiddleCenter))
                .WithShowDescription(false);

        /// <summary>创建调试用默认 Inventory（玩家背包 / 快捷栏 / 装备栏）。</summary>
        private void CreateDefaultInventories()
        {
            Service.CreateInventory(ID_PLAYER,    "玩家背包", 30);
            Service.CreateInventory(ID_HOTBAR,    "快捷栏",    9);
            Service.CreateInventory(ID_EQUIPMENT, "装备栏",    5);
            Log("创建默认 Inventory: player / hotbar / equipment", Color.cyan);
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
                // §4.1 跨模块 bare-string 调用 UIManager.EVT_GET_UI_GAMEOBJECT
                var goResult = EventProcessor.Instance.TriggerEventMethod(
                    "GetUIGameObject",
                    new List<object> { cached.Id });
                var go = ResultCode.IsOk(goResult) && goResult.Count >= 2 ? goResult[1] as GameObject : null;
                if (go != null)
                {
                    cached.Visible = true;
                    Log($"复用缓存的Inventory UI: {inventoryId}", Color.green);
                    LinkPlayerEquipmentVisibility(inventoryId, true);
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

            // §4.1 跨模块 bare-string 调用 UIManager.EVT_REGISTER_ENTITY
            var result = EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity",
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

            LinkPlayerEquipmentVisibility(inventoryId, true);

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

            LinkPlayerEquipmentVisibility(inventoryId, false);

            if (_rootPanels.TryGetValue(inventoryId, out var panel))
            {
                panel.Visible = false;
                Log($"已隐藏Inventory UI: {inventoryId}", Color.green);
                return ResultCode.Ok(inventoryId);
            }

            // §4.1 跨模块 bare-string：UIManager.EVT_UNREGISTER_ENTITY
            EventProcessor.Instance.TriggerEventMethod(
                "UnregisterUIEntity",
                new List<object> { inventoryId });
            Log($"关闭未缓存的Inventory UI: {inventoryId}", Color.yellow);
            return ResultCode.Ok(inventoryId);
        }

        /// <summary>真正销毁 UI（GameObject 也释放）。data: [inventoryId]</summary>
        public List<object> DestroyInventoryUI(List<object> data)
        {
            if (!TryGetId(data, 0, out var inventoryId, out var fail)) return fail;

            // §4.1 跨模块 bare-string：UIManager.EVT_UNREGISTER_ENTITY
            EventProcessor.Instance.TriggerEventMethod(
                "UnregisterUIEntity",
                new List<object> { inventoryId });

            _slotRefs.Remove(inventoryId);
            _descRefs.Remove(inventoryId);
            _rootPanels.Remove(inventoryId);

            Log($"销毁Inventory UI: {inventoryId}", Color.green);
            return ResultCode.Ok(inventoryId);
        }

        [Event(EVT_REGISTER_ITEM)]
        public List<object> RegisterItem(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is InventoryItem item))
                return ResultCode.Fail("参数无效: [InventoryItem]");
            RegisterTemplateIfMissing(item);
            return ResultCode.Ok(item.Id);
        }

        [Event(EVT_REGISTER_PICKABLE_ITEM)]
        public List<object> RegisterPickableItem(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is PickableItemDefinition definition))
                return ResultCode.Fail("参数无效: [PickableItemDefinition]");
            if (string.IsNullOrEmpty(definition.Id) || string.IsNullOrEmpty(definition.ItemTemplateId))
                return ResultCode.Fail("可拾取物定义缺少 Id 或 ItemTemplateId");

            definition.DefaultAmount = Mathf.Max(1, definition.DefaultAmount);
            _pickableDefinitions[definition.Id] = definition;
            Log($"注册可拾取物定义: {definition.Id} -> {definition.ItemTemplateId}", Color.cyan);
            return ResultCode.Ok(definition.Id);
        }

        [Event(EVT_SPAWN_PICKABLE_ITEM)]
        public List<object> SpawnPickableItem(List<object> data)
        {
            if (!TryGetId(data, 0, out var pickableId, out var fail)) return fail;
            if (!_pickableDefinitions.TryGetValue(pickableId, out var definition))
                return ResultCode.Fail($"可拾取物定义不存在: {pickableId}");

            var position = data.Count >= 2 && data[1] is Vector3 v3
                ? v3
                : data.Count >= 2 && data[1] is Vector2 v2
                    ? new Vector3(v2.x, v2.y, 0f)
                    : Vector3.zero;
            var targetInventoryId = data.Count >= 3 && data[2] is string invId && !string.IsNullOrEmpty(invId)
                ? invId
                : ID_PLAYER;
            var amount = data.Count >= 4 ? System.Convert.ToInt32(data[3]) : definition.DefaultAmount;

            var go = new GameObject(string.IsNullOrEmpty(definition.DisplayName) ? definition.Id : definition.DisplayName);
            go.transform.position = position;

            var sr = go.AddComponent<SpriteRenderer>();
            if (!string.IsNullOrEmpty(definition.SpriteResourcePath))
            {
                // 走 ResourceManager 事件通道加载（§4.1 bare-string），自动走缓存 + subfolder hints
                var spriteResult = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite", new List<object> { definition.SpriteResourcePath });
                if (ResultCode.IsOk(spriteResult) && spriteResult.Count >= 2 && spriteResult[1] is Sprite spr)
                    sr.sprite = spr;
                else
                    Log($"可拾取物 Sprite 加载失败: {definition.SpriteResourcePath}", Color.yellow);
            }

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var colliderRadius = sr.sprite != null
                ? Mathf.Max(sr.bounds.size.x, sr.bounds.size.y) * 0.5f
                : Mathf.Max(definition.ColliderSize.x, definition.ColliderSize.y) * 0.25f;
            colliderRadius = Mathf.Max(0.05f, colliderRadius);

            var physicalCollider = go.AddComponent<CircleCollider2D>();
            physicalCollider.isTrigger = false;
            physicalCollider.radius = colliderRadius;
            physicalCollider.offset = definition.ColliderOffset;

            var pickupTriggerGo = new GameObject("PickupTrigger");
            pickupTriggerGo.transform.SetParent(go.transform, false);
            var pickupTrigger = pickupTriggerGo.AddComponent<CircleCollider2D>();
            pickupTrigger.isTrigger = true;
            pickupTrigger.radius = colliderRadius;
            pickupTrigger.offset = definition.ColliderOffset;

            var pickable = go.AddComponent<PickableItem>();
            pickable.Configure(targetInventoryId, definition.ItemTemplateId, amount);

            Log($"生成可拾取物: {pickableId} at {position}", Color.green);
            return ResultCode.Ok(go);
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

        /// <summary>
        /// PlayerBackPack 联动：打开/关闭玩家背包时同步装备栏。
        /// open=true 时 — 如装备栏未打开则调 OpenInventoryUI（内部会复用缓存）。
        /// open=false 时 — 只隐藏已缓存面板，不销毁。
        /// </summary>
        private void LinkPlayerEquipmentVisibility(string inventoryId, bool open)
        {
            if (inventoryId != ID_PLAYER) return;
            if (Service.GetInventory(ID_EQUIPMENT) == null) return;
            if (open)
            {
                OpenInventoryUI(new List<object> { ID_EQUIPMENT, CFG_EQUIPMENT });
            }
            else if (_rootPanels.TryGetValue(ID_EQUIPMENT, out var eqPanel))
            {
                eqPanel.Visible = false;
            }
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
