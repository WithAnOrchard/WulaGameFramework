using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;

// 本文件不 <c>using</c> UIManager 模块。查 GameObject 走 EVT_GET_UI_GAMEOBJECT 事件。

namespace EssSystem.Core.Application.SingleManagers.InventoryManager
{
    /// <summary>
    /// Inventory UI 的纯静态构建/绑定工具集 —— 把 <see cref="InventoryManager"/> 从大量 UI 细节里解放出来。
    /// <para>
    /// 职责：<br/>
    /// 1. 根据 <see cref="InventoryConfig"/> 构建完整 UIPanel 树（panel + title + slots + close + 描述子面板）<br/>
    /// 2. 把单条 <see cref="InventoryItem"/> 状态写入 slot / 描述面板（数据→视图绑定）<br/>
    /// 3. 给已注册的 slot GameObject 挂上拖拽处理器
    /// </para>
    /// </summary>
    internal static class InventoryUIBuilder
    {
        /// <summary>
        /// 文本超采样倍率（参 <c>Assets/Agent.md §5「文本清晰度」</c>）。
        /// FontSize × N + Size × N + Scale × (1/N)：让字体以 N× 分辨率栅格化再缩小渲染，
        /// 显著降低 uGUI Text 在 1080p+ 屏幕上的模糊感。建议整数倍（2×/3×）。
        /// </summary>
        private const float TextSupersample = 6f;
        private static DescUIRefs _sharedDescRefs;
        private const string HotbarInventoryId = "hotbar";
        private const string HotbarSlotFrame = "Tribe/Common/Items/UI/slot_frame_gold";

        #region Public API

        /// <summary>
        /// 构建完整 UIPanel 树。返回的 refs 用于后续 EVT_CHANGED / 点击事件原地刷新。
        /// </summary>
        public static (UIPanelComponent panel, SlotUIRefs slotRefs, DescUIRefs descRefs)
            BuildPanelTree(string inventoryId, InventoryConfig config)
        {
            var pc = config.PanelConfig;
            var panel = new UIPanelComponent(inventoryId, $"{config.DisplayName} - {inventoryId}")
                .SetPosition(pc.Position.x, pc.Position.y)
                .SetSize(pc.Size.x, pc.Size.y)
                .SetScale(pc.Scale.x, pc.Scale.y)
                .SetBackgroundSpriteId(pc.BackgroundSpriteId)
                .SetBackgroundColor(SpriteAwareTint(pc.BackgroundSpriteId, pc.BackgroundColor))
                .SetVisible(true);

            if (inventoryId == HotbarInventoryId)
                ApplyHotbarChrome(panel, pc);
            else
                ApplyInventoryChrome(panel, pc, inventoryId);

            // 0) 标题（容器名称）
            if (config.ShowTitle && config.TitleConfig != null && config.TitleConfig.Visible)
                BuildTitle(inventoryId, panel, config, pc);

            // 1) 描述子面板（先建，slot 点击回调引用其 refs）
            DescUIRefs descRefs = null;
            if (config.ShowDescription)
                descRefs = BuildDescriptionPanel(inventoryId, panel, config.DescriptionPanelConfig);

            // 2) 槽位
            var slotRefs = BuildSlots(inventoryId, panel, config, descRefs);

            // 3) 关闭按钮
            BuildCloseButton(inventoryId, panel, config.CloseButtonConfig);

            return (panel, slotRefs, descRefs);
        }

        private static void ApplyHotbarChrome(UIPanelComponent panel, UIPanelSpec pc)
        {
            var w = pc.Size.x;
            var h = pc.Size.y;
            var cx = w * 0.5f;
            var cy = h * 0.5f;
            var trayW = Mathf.Min(w - 72f, 706f);
            var trayH = Mathf.Min(h - 10f, 84f);
            var gold = new Color(0.88f, 0.69f, 0.35f, 0.82f);
            var trayBg = new Color(0.024f, 0.019f, 0.016f, 0.94f);
            var innerBg = new Color(0.010f, 0.012f, 0.014f, 0.62f);
            var slotRailBg = new Color(0.040f, 0.026f, 0.022f, 0.30f);
            var shadow = new Color(0f, 0f, 0f, 0.58f);

            panel.SetBackgroundSpriteId(string.Empty)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f));

            panel.AddChild(new UIPanelComponent($"{panel.Id}_HotbarShadow", "HotbarShadow")
                .SetPosition(cx, cy - 4f)
                .SetSize(trayW + 10f, trayH + 8f)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0.28f)));

            panel.AddChild(new UIPanelComponent($"{panel.Id}_HotbarPlate", "HotbarPlate")
                .SetPosition(cx, cy)
                .SetSize(trayW, trayH)
                .SetBackgroundColor(trayBg));

            AddHotbarBorder(panel, $"{panel.Id}_HotbarOuter", cx, cy, trayW, trayH, 2f, gold);

            panel.AddChild(new UIPanelComponent($"{panel.Id}_HotbarInner", "HotbarInner")
                .SetPosition(cx, cy - 1f)
                .SetSize(trayW - 14f, trayH - 14f)
                .SetBackgroundColor(innerBg));

            panel.AddChild(new UIPanelComponent($"{panel.Id}_HotbarSlotRail", "HotbarSlotRail")
                .SetPosition(cx, cy - 1f)
                .SetSize(trayW - 18f, trayH - 28f)
                .SetBackgroundColor(slotRailBg));

            panel.AddChild(new UIPanelComponent($"{panel.Id}_HotbarBottomShade", "HotbarBottomShade")
                .SetPosition(cx, cy - trayH * 0.5f + 13f)
                .SetSize(trayW - 22f, 4f)
                .SetBackgroundColor(shadow));
        }

        private static void ApplyInventoryChrome(UIPanelComponent panel, UIPanelSpec pc, string inventoryId)
        {
            var w = pc.Size.x;
            var h = pc.Size.y;
            var cx = w * 0.5f;
            var cy = h * 0.5f;
            var gold = new Color(0.88f, 0.69f, 0.35f, 0.78f);
            var goldSoft = new Color(0.88f, 0.69f, 0.35f, 0.32f);
            var rootBg = new Color(0.050f, 0.038f, 0.040f, 0.94f);
            var headerBg = new Color(0.012f, 0.014f, 0.018f, 0.82f);
            var innerBg = new Color(0.018f, 0.020f, 0.024f, 0.30f);

            panel.SetBackgroundSpriteId(string.Empty)
                .SetBackgroundColor(rootBg);

            panel.AddChild(new UIPanelComponent($"{panel.Id}_WindowInner", "WindowInner")
                .SetPosition(cx, cy - 18f)
                .SetSize(w - 22f, h - 92f)
                .SetBackgroundColor(innerBg));

            panel.AddChild(new UIPanelComponent($"{panel.Id}_WindowHeader", "WindowHeader")
                .SetPosition(cx, h - 24f)
                .SetSize(w - 2f, 48f)
                .SetBackgroundColor(headerBg));

            AddHotbarBorder(panel, $"{panel.Id}_WindowOuter", cx, cy, w, h, 2f, gold);

            panel.AddChild(new UIPanelComponent($"{panel.Id}_WindowHeaderLine", "WindowHeaderLine")
                .SetPosition(cx, h - 49f)
                .SetSize(w - 24f, 1f)
                .SetBackgroundColor(goldSoft));

            var isEquipment = inventoryId == "equipment";
            var slotAreaWidth = isEquipment ? w - 24f : 560f;
            var slotAreaHeight = isEquipment ? 460f : 460f;
            var slotAreaX = cx;
            var slotAreaY = isEquipment ? 232f : 256f;
            panel.AddChild(new UIPanelComponent($"{panel.Id}_SlotArea", "SlotArea")
                .SetPosition(slotAreaX, slotAreaY)
                .SetSize(slotAreaWidth, slotAreaHeight)
                .SetBackgroundColor(new Color(0.010f, 0.012f, 0.014f, 0.16f)));

            AddHotbarBorder(panel, $"{panel.Id}_SlotAreaBorder", slotAreaX, slotAreaY,
                slotAreaWidth, slotAreaHeight, 1f, new Color(gold.r, gold.g, gold.b, 0.18f));
        }

        private static void AddHotbarBorder(UIPanelComponent parent, string id, float centerX, float centerY,
            float width, float height, float thickness, Color color)
        {
            parent.AddChild(new UIPanelComponent($"{id}_Top", "Top")
                .SetPosition(centerX, centerY + height * 0.5f - thickness * 0.5f)
                .SetSize(width, thickness)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{id}_Bottom", "Bottom")
                .SetPosition(centerX, centerY - height * 0.5f + thickness * 0.5f)
                .SetSize(width, thickness)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{id}_Left", "Left")
                .SetPosition(centerX - width * 0.5f + thickness * 0.5f, centerY)
                .SetSize(thickness, height)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{id}_Right", "Right")
                .SetPosition(centerX + width * 0.5f - thickness * 0.5f, centerY)
                .SetSize(thickness, height)
                .SetBackgroundColor(color));
        }

        /// <summary>走 EVT_GET_UI_GAMEOBJECT 事件拿 UI GameObject，不引用 UIEntity 类。</summary>
        private static GameObject QueryUIGameObject(string daoId)
        {
            if (!EventProcessor.HasInstance) return null;
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject",   // = UIManager.EVT_GET_UI_GAMEOBJECT
                new List<object> { daoId });
            return ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as GameObject : null;
        }

        /// <summary>给 slot 的 GameObject 挂上 <see cref="InventorySlotDragHandler"/>。需在 UI 注册之后调用。</summary>
        public static void AttachSlotDragHandlers(string inventoryId, int slotCount)
        {
            for (var i = 0; i < slotCount; i++)
            {
                var slotGo = QueryUIGameObject($"{inventoryId}_Slot_{i}");
                if (slotGo == null) continue;

                var handler = slotGo.GetComponent<InventorySlotDragHandler>()
                              ?? slotGo.AddComponent<InventorySlotDragHandler>();
                handler.InventoryId = inventoryId;
                handler.SlotIndex   = i;

                var iconGo = QueryUIGameObject($"{inventoryId}_Slot_{i}_Icon");
                handler.SourceIconImage = iconGo != null
                    ? iconGo.GetComponent<UnityEngine.UI.Image>()
                    : null;
            }
        }

        /// <summary>把单个物品状态写入 slot 三件套；item 为空表示空槽。</summary>
        public static void ApplyItemToSlot(UIPanelComponent icon, UITextComponent name, UITextComponent stack, InventoryItem item)
        {
            var hasItem = item != null && !item.IsEmpty;
            if (hasItem)
            {
                // 物品实例 IconSpriteId 可能为空（持久化数据来自旧 session），回退到模板
                var iconSpriteId = item.IconSpriteId;
                if (string.IsNullOrEmpty(iconSpriteId) && InventoryService.HasInstance)
                    iconSpriteId = InventoryService.Instance.GetTemplate(item.Id)?.IconSpriteId;

                icon.BackgroundSpriteId = iconSpriteId ?? string.Empty;
                icon.BackgroundColor    = Color.white;
                name.Text  = string.Empty;
                stack.Text = item.CurrentStack > 0 ? item.CurrentStack.ToString() : string.Empty;
            }
            else
            {
                icon.BackgroundSpriteId = string.Empty;
                icon.BackgroundColor    = new Color(0f, 0f, 0f, 0f);
                name.Text  = string.Empty;
                stack.Text = string.Empty;
            }
        }

        /// <summary>把单个物品状态写入描述面板四件套；item 为空时显示占位文本。</summary>
        public static void ApplyItemToDesc(DescUIRefs refs, InventoryItem item)
        {
            var hasItem = item != null && !item.IsEmpty;
            if (hasItem)
            {
                // 与 ApplyItemToSlot 保持一致：实例 IconSpriteId 为空时回退到模板
                var iconSpriteId = item.IconSpriteId;
                if (string.IsNullOrEmpty(iconSpriteId) && InventoryService.HasInstance)
                    iconSpriteId = InventoryService.Instance.GetTemplate(item.Id)?.IconSpriteId;

                refs.Icon.BackgroundSpriteId = iconSpriteId ?? string.Empty;
                refs.Icon.BackgroundColor    = Color.white;
                refs.Name.Text        = item.Name ?? item.Id ?? string.Empty;
                refs.Stack.Text       = $"数量 {item.CurrentStack} / 最大堆叠 {item.MaxStack}";
                refs.Description.Text = item.Description ?? string.Empty;
            }
            else
            {
                refs.Icon.BackgroundSpriteId = string.Empty;
                refs.Icon.BackgroundColor    = new Color(0f, 0f, 0f, 0f);
                refs.Name.Text        = string.Empty;
                refs.Stack.Text       = string.Empty;
                refs.Description.Text = refs.EmptyPlaceholder ?? string.Empty;
            }
        }

        #endregion

        #region Internal Builders

        /// <summary>有 sprite 时返回白色（=不染色）；无 sprite 时回退到配置颜色。</summary>
        private static Color SpriteAwareTint(string spriteId, Color fallback) =>
            string.IsNullOrEmpty(spriteId) ? fallback : Color.white;

        private static void BuildTitle(string inventoryId, UIPanelComponent parent, InventoryConfig config, UIPanelSpec pc)
        {
            var tc = config.TitleConfig;
            var text = !string.IsNullOrEmpty(tc.Text) ? tc.Text : (config.DisplayName ?? inventoryId);

            // UITextSpec.Position 约定 = rect 中心相对父面板 bottom-left（与 UITextComponent.SetPosition
            // 在 UIEntityFactory 里 anchor=(0,0)+pivot=(0.5,0.5) 的语义一致）——直接送入即可，
            // 历史代码减去 (PanelW/2, PanelH/2) 的“转换”是 bug，会把文本推到面板左下区。
            var titleText = new UITextComponent($"{inventoryId}_Title", $"{inventoryId}_Title")
                .SetPosition(tc.Position.x, tc.Position.y)
                .SetColor(tc.TextColor)
                .SetAlignment(tc.Alignment)
                .SetText(text);
            ApplySupersample(titleText, tc.Size.x, tc.Size.y, tc.FontSize);
            titleText.SetVisible(true);
            parent.AddChild(titleText);
        }

        private static SlotUIRefs BuildSlots(string inventoryId, UIPanelComponent panel, InventoryConfig config, DescUIRefs descRefs)
        {
            var inv = InventoryService.Instance.GetInventory(inventoryId);
            var sc  = config.SlotConfig;
            var refs = new SlotUIRefs
            {
                Buttons = new UIButtonComponent[config.SlotsPerPage],
                Icons  = new UIPanelComponent[config.SlotsPerPage],
                Names  = new UITextComponent [config.SlotsPerPage],
                Stacks = new UITextComponent [config.SlotsPerPage],
                SelectionRoots = inventoryId == HotbarInventoryId
                    ? new UIPanelComponent[config.SlotsPerPage]
                    : null,
            };

            var stackFont = Mathf.Max(16, Mathf.RoundToInt(sc.SlotHeight * 0.24f));

            for (var i = 0; i < config.SlotsPerPage; i++)
            {
                var slotIdx = i; // 闭包捕获用
                var row = i / sc.SlotsPerRow;
                var col = i % sc.SlotsPerRow;
                var x = sc.StartOffsetX + col * (sc.SlotWidth + sc.SlotSpacingX);
                var y = sc.StartOffsetY - row * (sc.SlotHeight + sc.SlotSpacingY);

                if (inventoryId == HotbarInventoryId)
                {
                    panel.AddChild(new UIPanelComponent($"{inventoryId}_SlotBack_{i}", $"SlotBack_{i}")
                        .SetPosition(x, y)
                        .SetSize(sc.SlotWidth + 4f, sc.SlotHeight + 4f)
                        .SetBackgroundColor(new Color(0.010f, 0.008f, 0.010f, 0.34f)));
                }

                var slotBtn = new UIButtonComponent($"{inventoryId}_Slot_{i}", $"Slot_{i}")
                    .SetPosition(x, y)
                    .SetSize(sc.SlotWidth, sc.SlotHeight)
                    .SetVisible(true)
                    .SetButtonSpriteId(inventoryId == HotbarInventoryId ? HotbarSlotFrame : sc.SlotBackgroundSpriteId)
                    .SetButtonColor(inventoryId == HotbarInventoryId ? new Color(1f, 0.95f, 0.82f, 0.96f) : Color.white)
                    .SetInteractable(true);

                // 图标（slot 居中）
                var iconSize = Mathf.Min(sc.SlotWidth, sc.SlotHeight) * 0.72f;
                var iconPanel = new UIPanelComponent($"{inventoryId}_Slot_{i}_Icon", $"Slot_{i}_Icon")
                    .SetPosition(sc.SlotWidth * 0.5f, sc.SlotHeight * 0.5f)
                    .SetSize(iconSize, iconSize)
                    .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                    .SetVisible(true);
                slotBtn.AddChild(iconPanel);

                // 名称（slot 顶部）
                // 注：子组件 Position 是 rect 中心相对父组件 bottom-left（与图标 (SlotW*0.5, SlotH*0.5)
                //     表示 slot 中心是同一约定）。早期版本写成 (0, SlotH*0.38)，结果把文字 rect 中心
                //     锚到了 slot 左边缘 → 视觉偏移。这里改用 SlotCenter + 偏移修正。
                var nameText = new UITextComponent($"{inventoryId}_Slot_{i}_NameText", $"Slot_{i}_Name")
                    .SetPosition(sc.SlotWidth * 0.5f, sc.SlotHeight * 0.5f + sc.SlotHeight * 0.38f)
                    .SetColor(Color.white)
                    .SetAlignment(TextAnchor.MiddleCenter)
                    .SetText(string.Empty);
                ApplySupersample(nameText, Mathf.Max(1f, sc.SlotWidth - 4f), sc.SlotHeight * 0.18f, stackFont);
                nameText.SetVisible(false);
                slotBtn.AddChild(nameText);

                // 数量（slot 底部右对齐）
                var stackText = new UITextComponent($"{inventoryId}_Slot_{i}_StackText", $"Slot_{i}_Stack")
                    .SetPosition(sc.SlotWidth * 0.5f - sc.SlotWidth * 0.08f, sc.SlotHeight * 0.5f - sc.SlotHeight * 0.34f)
                    .SetColor(new Color(1f, 0.85f, 0.4f, 1f))
                    .SetAlignment(TextAnchor.MiddleRight)
                    .SetText(string.Empty);
                ApplySupersample(stackText, Mathf.Max(1f, sc.SlotWidth - 8f), sc.SlotHeight * 0.28f, stackFont);
                stackText.SetVisible(true);
                slotBtn.AddChild(stackText);

                UIPanelComponent selectionRoot = null;
                if (inventoryId == HotbarInventoryId)
                {
                    selectionRoot = BuildHotbarSelectionRoot(inventoryId, i, sc.SlotWidth, sc.SlotHeight);
                    slotBtn.AddChild(selectionRoot);
                }

                refs.Buttons[i] = slotBtn;
                refs.Icons[i]  = iconPanel;
                refs.Names[i]  = nameText;
                refs.Stacks[i] = stackText;
                if (refs.SelectionRoots != null) refs.SelectionRoots[i] = selectionRoot;

                ApplyItemToSlot(iconPanel, nameText, stackText, inv?.GetSlot(slotIdx)?.Item);

                // 点击 slot → 更新描述面板
                var targetDescRefs = descRefs ?? _sharedDescRefs;
                if (targetDescRefs != null)
                {
                    var capturedRefs = targetDescRefs;
                    slotBtn.OnClick += clickedButton =>
                    {
                        var clickedSlotIdx = ParseSlotIndex(clickedButton?.Id, slotIdx);
                        var clickedItem = InventoryService.Instance.GetInventory(inventoryId)?.GetSlot(clickedSlotIdx)?.Item;
                        ApplyItemToDesc(capturedRefs, clickedItem);
                    };
                }

                if (inventoryId == HotbarInventoryId)
                {
                    slotBtn.OnClick += _ =>
                    {
                        InventoryManager.TryGetInstance()?.ToggleHotbarSelection(slotIdx);
                    };
                }

                panel.AddChild(slotBtn);
            }

            return refs;
        }

        private static void BuildCloseButton(string inventoryId, UIPanelComponent panel, UIButtonSpec cb)
        {
            var closeBtn = cb.CreateComponent($"{inventoryId}_CloseButton", "CloseButton");
            if (inventoryId != HotbarInventoryId && closeBtn.Visible)
            {
                closeBtn.SetButtonSpriteId(string.Empty)
                    .SetButtonColor(new Color(0.015f, 0.012f, 0.016f, 0.72f))
                    .SetFontSize(22);

                var closeText = new UITextComponent($"{inventoryId}_CloseButton_Text", "CloseButtonText")
                    .SetPosition(cb.Size.x * 0.5f, cb.Size.y * 0.5f)
                    .SetColor(new Color(1f, 0.96f, 0.88f, 1f))
                    .SetAlignment(TextAnchor.MiddleCenter)
                    .SetText("×")
                    .SetVisible(true);
                ApplySupersample(closeText, cb.Size.x, cb.Size.y, 18);
                closeBtn.AddChild(closeText);
            }

            closeBtn.OnClick += _ => EventProcessor.Instance.TriggerEventMethod(
                InventoryManager.EVT_CLOSE_UI, new List<object> { inventoryId });

            panel.AddChild(closeBtn);
        }

        private static int ParseSlotIndex(string buttonId, int fallback)
        {
            if (string.IsNullOrEmpty(buttonId)) return fallback;
            var marker = "_Slot_";
            var idx = buttonId.LastIndexOf(marker, System.StringComparison.Ordinal);
            if (idx < 0) return fallback;
            var start = idx + marker.Length;
            return int.TryParse(buttonId.Substring(start), out var slotIndex) ? slotIndex : fallback;
        }

        private static DescUIRefs BuildDescriptionPanel(string inventoryId, UIPanelComponent parent, DescriptionPanelConfig dc)
        {
            var descPanel = new UIPanelComponent($"{inventoryId}_DescPanel", $"{inventoryId}_DescriptionPanel")
                .SetPosition(dc.Offset.x, dc.Offset.y)
                .SetSize(dc.Width, dc.Height)
                .SetBackgroundSpriteId(dc.BackgroundSpriteId)
                .SetBackgroundColor(SpriteAwareTint(dc.BackgroundSpriteId, dc.BackgroundColor))
                .SetVisible(true);

            ApplyDescriptionChrome(descPanel, dc.Width, dc.Height);

            // 图标
            var ic = dc.IconConfig ?? new UIIconSpec();
            AddDescriptionItemFrame(descPanel, ic);
            var iconPanel = ic.CreateComponent($"{inventoryId}_DescIcon", $"{inventoryId}_DescriptionIcon");
            descPanel.AddChild(iconPanel);

            // 三个文本子组件
            var nameText  = BuildDescText($"{inventoryId}_DescName",  $"{inventoryId}_DescriptionName",
                                           dc.NameConfig,  dc, string.Empty);
            var stackText = BuildDescText($"{inventoryId}_DescStack", $"{inventoryId}_DescriptionStack",
                                           dc.StackConfig, dc, string.Empty);
            var descText  = BuildDescText($"{inventoryId}_DescText",  $"{inventoryId}_DescriptionText",
                                           dc.DescTextConfig, dc, dc.EmptyPlaceholder ?? string.Empty);
            descPanel.AddChild(nameText);
            descPanel.AddChild(stackText);
            descPanel.AddChild(descText);

            parent.AddChild(descPanel);

            var refs = new DescUIRefs
            {
                Icon = iconPanel,
                Name = nameText,
                Stack = stackText,
                Description = descText,
                EmptyPlaceholder = dc.EmptyPlaceholder ?? string.Empty,
            };
            _sharedDescRefs = refs;
            return refs;
        }

        private static UIPanelComponent BuildHotbarSelectionRoot(string inventoryId, int slotIndex, float slotWidth, float slotHeight)
        {
            var rootW = slotWidth + 12f;
            var rootH = slotHeight + 12f;
            var cx = rootW * 0.5f;
            var cy = rootH * 0.5f;
            var cyan = new Color(0.05f, 0.92f, 1f, 1f);
            var white = new Color(0.92f, 1f, 1f, 1f);

            var root = new UIPanelComponent($"{inventoryId}_Slot_{slotIndex}_Selected", $"Slot_{slotIndex}_Selected")
                .SetPosition(slotWidth * 0.5f, slotHeight * 0.5f)
                .SetSize(rootW, rootH)
                .SetBackgroundColor(new Color(0.02f, 0.70f, 1f, 0.32f))
                .SetVisible(false);

            root.AddChild(new UIPanelComponent($"{root.Id}_InnerGlow", "InnerGlow")
                .SetPosition(cx, cy)
                .SetSize(slotWidth - 8f, slotHeight - 8f)
                .SetBackgroundColor(new Color(0.30f, 1f, 1f, 0.26f)));

            AddHotbarBorder(root, $"{root.Id}_OuterBorder", cx, cy, rootW, rootH, 7f, cyan);
            AddHotbarBorder(root, $"{root.Id}_InnerBorder", cx, cy, slotWidth + 2f, slotHeight + 2f, 3f, white);

            var markerW = 11f;
            root.AddChild(new UIPanelComponent($"{root.Id}_MarkerLeft", "MarkerLeft")
                .SetPosition(markerW * 0.5f, cy)
                .SetSize(markerW, rootH - 10f)
                .SetBackgroundColor(new Color(0.90f, 1f, 1f, 1f)));

            root.AddChild(new UIPanelComponent($"{root.Id}_MarkerTop", "MarkerTop")
                .SetPosition(cx, rootH - 5f)
                .SetSize(rootW - 14f, 10f)
                .SetBackgroundColor(new Color(0.05f, 0.92f, 1f, 1f)));

            return root;
        }

        private static void ApplyDescriptionChrome(UIPanelComponent panel, float width, float height)
        {
            var cx = width * 0.5f;
            var cy = height * 0.5f;
            var gold = new Color(0.88f, 0.69f, 0.35f, 0.62f);

            panel.SetBackgroundSpriteId(string.Empty)
                .SetBackgroundColor(new Color(0.035f, 0.030f, 0.032f, 0.92f));

            panel.AddChild(new UIPanelComponent($"{panel.Id}_DescInner", "DescInner")
                .SetPosition(cx, cy)
                .SetSize(width - 16f, height - 16f)
                .SetBackgroundColor(new Color(0.010f, 0.012f, 0.014f, 0.28f)));

            AddHotbarBorder(panel, $"{panel.Id}_DescOuter", cx, cy, width, height, 2f, gold);
        }

        private static void AddDescriptionItemFrame(UIPanelComponent panel, UIIconSpec icon)
        {
            if (panel == null || icon == null) return;

            var frameW = icon.Size.x + 28f;
            var frameH = icon.Size.y + 28f;
            var gold = new Color(0.88f, 0.69f, 0.35f, 0.62f);

            panel.AddChild(new UIPanelComponent($"{panel.Id}_ItemFrameBg", "ItemFrameBg")
                .SetPosition(icon.Position.x, icon.Position.y)
                .SetSize(frameW, frameH)
                .SetBackgroundColor(new Color(0.010f, 0.012f, 0.014f, 0.52f)));

            AddHotbarBorder(panel, $"{panel.Id}_ItemFrame", icon.Position.x, icon.Position.y,
                frameW, frameH, 2f, gold);
        }

        /// <summary>
        /// 用 <see cref="UITextSpec"/> 创建一个 UITextComponent。
        /// 配置 Position 是「rect 中心相对父面板左下角」，内部转成 UITextComponent 的「中心相对父中心」。
        /// </summary>
        private static UITextComponent BuildDescText(
            string daoId, string name,
            UITextSpec cfg,
            DescriptionPanelConfig dp,
            string initialText)
        {
            cfg = cfg ?? new UITextSpec();
            // 与 BuildTitle 同理：Position 已是「rect 中心 / 父面板 bottom-left」约定，不需要减去面板一半。
            var t = new UITextComponent(daoId, name)
                .SetPosition(cfg.Position.x, cfg.Position.y)
                .SetColor(cfg.TextColor)
                .SetAlignment(cfg.Alignment)
                .SetText(initialText ?? string.Empty);
            ApplySupersample(t, cfg.Size.x, cfg.Size.y, cfg.FontSize);
            t.SetVisible(cfg.Visible);
            return t;
        }

        /// <summary>
        /// 对已经设好 Position/Color/Alignment 的 <see cref="UITextComponent"/> 应用 N× 超采样：
        /// FontSize × N、Size × N、Scale × (1/N)。视觉尺寸不变，但字体以 N× 分辨率栅格化显著减糊。
        /// </summary>
        private static void ApplySupersample(UITextComponent text, float width, float height, int fontSize)
        {
            var n = TextSupersample;
            var inv = 1f / n;
            text.SetSize(width * n, height * n)
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(fontSize * n)))
                .SetScale(inv, inv);
        }

        #endregion
    }
}
