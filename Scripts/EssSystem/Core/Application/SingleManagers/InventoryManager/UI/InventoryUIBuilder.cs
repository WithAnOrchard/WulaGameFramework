using System.Collections.Generic;
using System.Text;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;

// 本文件只构建 Inventory UI 的 DAO 树；不直接 using UIManager 模块。
// 需要运行时 GameObject 时，通过 UIManager 暴露的事件查询。

namespace EssSystem.Core.Application.SingleManagers.InventoryManager
{
    /// <summary>
    /// Inventory UI 的静态构建与绑定工具。
    /// <para>
    /// 根据 <see cref="InventoryConfig"/> 构建面板、标题、槽位、关闭按钮、描述面板、信息页签和装饰层；
    /// 将 <see cref="InventoryItem"/> 数据刷新到 slot / 描述面板；
    /// 并在 UI 注册后给 slot GameObject 挂接拖拽处理器。
    /// </para>
    /// <para>
    /// 这里不能写 Demo / 项目专属实体、素材路径、装备部位等语义；这些内容必须来自 Inventory JSON 配置或上层适配。
    /// </para>
    /// </summary>
    internal static class InventoryUIBuilder
    {
        /// <summary>
        /// 文本超采样倍率。将文本按 N 倍字体尺寸与 Rect 尺寸生成，再按 1/N 缩回，
        /// 降低 uGUI 文本在高分辨率下的模糊感。
        /// </summary>
        private const float TextSupersample = 6f;
        private static DescUIRefs _sharedDescRefs;
        private const string HotbarInventoryId = "hotbar";
        private static readonly Dictionary<string, EmptySlotVisual> EmptySlotVisuals =
            new Dictionary<string, EmptySlotVisual>();

        private readonly struct EmptySlotVisual
        {
            public readonly string SpriteId;
            public readonly Color Color;

            public EmptySlotVisual(string spriteId, Color color)
            {
                SpriteId = spriteId;
                Color = color;
            }
        }

        #region Public API

        /// <summary>
        /// 构建完整 UIPanel 树；返回 refs 用于后续 InventoryChanged 刷新与 slot 点击回填。
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
                ApplyInventoryChrome(panel, pc, config);

            // 0) 标题
            if (config.ShowTitle && config.TitleConfig != null && config.TitleConfig.Visible)
                BuildTitle(inventoryId, panel, config, pc);

            // 1) 描述子面板：先建，slot 点击回调会引用 refs。
            DescUIRefs descRefs = null;
            if (config.ShowDescription)
                descRefs = BuildDescriptionPanel(inventoryId, panel, config.DescriptionPanelConfig);

            if (descRefs != null && config.InfoTabs != null && config.InfoTabs.Count > 0)
                BuildInfoTabs(inventoryId, panel, descRefs, config.InfoTabs);

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
            var goldSoft = new Color(gold.r, gold.g, gold.b, 0.30f);

            panel.SetBackgroundSpriteId(string.Empty)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f));

            AddHotbarBorder(panel, $"{panel.Id}_HotbarOuter", cx, cy, trayW, trayH, 2f, gold);
            AddHotbarBorder(panel, $"{panel.Id}_HotbarInner", cx, cy, trayW - 12f, trayH - 12f, 1f, goldSoft);
        }

        private static void ApplyInventoryChrome(UIPanelComponent panel, UIPanelSpec pc, InventoryConfig config)
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

            var isEquipmentConfig = config != null
                && string.Equals(config.ConfigId, "PlayerEquipment", System.StringComparison.Ordinal);
            var body = isEquipmentConfig ? config.BodyVisualConfig : null;
            var hasBodyVisual = body != null && !string.IsNullOrEmpty(body.BackgroundSpriteId);
            // Equipment body art is a sprite-only visual; no internal SlotArea border is generated.
            if (hasBodyVisual)
            {
                panel.AddChild(new UIPanelComponent($"{panel.Id}_BodyVisual", "BodyVisual")
                    .SetPosition(body.Position.x, body.Position.y)
                    .SetSize(body.Size.x, body.Size.y)
                    .SetBackgroundSpriteId(body.BackgroundSpriteId)
                    .SetBackgroundColor(SpriteAwareTint(body.BackgroundSpriteId, body.BackgroundColor))
                    .SetVisible(true)
                    .SetInteractable(false));
            }
        }

        private static void AddHotbarBorder(UIPanelComponent parent, string id, float centerX, float centerY,
            float width, float height, float thickness, Color color, List<UIPanelComponent> collect = null)
        {
            var top = new UIPanelComponent($"{id}_Top", "Top")
                .SetPosition(centerX, centerY + height * 0.5f - thickness * 0.5f)
                .SetSize(width, thickness)
                .SetBackgroundColor(color);
            parent.AddChild(top);
            collect?.Add(top);

            var bottom = new UIPanelComponent($"{id}_Bottom", "Bottom")
                .SetPosition(centerX, centerY - height * 0.5f + thickness * 0.5f)
                .SetSize(width, thickness)
                .SetBackgroundColor(color);
            parent.AddChild(bottom);
            collect?.Add(bottom);

            var left = new UIPanelComponent($"{id}_Left", "Left")
                .SetPosition(centerX - width * 0.5f + thickness * 0.5f, centerY)
                .SetSize(thickness, height)
                .SetBackgroundColor(color);
            parent.AddChild(left);
            collect?.Add(left);

            var right = new UIPanelComponent($"{id}_Right", "Right")
                .SetPosition(centerX + width * 0.5f - thickness * 0.5f, centerY)
                .SetSize(thickness, height)
                .SetBackgroundColor(color);
            parent.AddChild(right);
            collect?.Add(right);
        }

        /// <summary>通过 UIManager 事件查询 UI GameObject，避免直接引用 UIEntity 类型。</summary>
        private static GameObject QueryUIGameObject(string daoId)
        {
            if (!EventProcessor.HasInstance) return null;
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject",   // = UIManager.EVT_GET_UI_GAMEOBJECT
                new List<object> { daoId });
            return ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as GameObject : null;
        }

        /// <summary>UI 注册完成后，为每个 slot 的 GameObject 挂接拖拽处理器。</summary>
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

        /// <summary>将物品状态写入 slot 的按钮子组件；item 为空表示空槽。</summary>
        public static void ApplyItemToSlot(UIPanelComponent icon, UITextComponent name, UITextComponent stack, InventoryItem item)
        {
            var hasItem = item != null && !item.IsEmpty;
            if (hasItem)
            {
                // 实例 IconSpriteId 可能来自旧存档而为空，回退到模板图标。
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
                if (icon != null && EmptySlotVisuals.TryGetValue(icon.Id, out var emptyVisual)
                    && !string.IsNullOrEmpty(emptyVisual.SpriteId))
                {
                    icon.BackgroundSpriteId = emptyVisual.SpriteId;
                    icon.BackgroundColor    = emptyVisual.Color;
                }
                else
                {
                    icon.BackgroundSpriteId = string.Empty;
                    icon.BackgroundColor    = new Color(0f, 0f, 0f, 0f);
                }
                name.Text  = string.Empty;
                stack.Text = string.Empty;
            }
        }

        /// <summary>将物品状态写入描述面板；item 为空时显示占位文本。</summary>
        public static void ApplyItemToDesc(DescUIRefs refs, InventoryItem item)
        {
            var hasItem = item != null && !item.IsEmpty;
            if (hasItem)
            {
                // 与 ApplyItemToSlot 保持一致：实例图标为空时回退到模板图标。
                var iconSpriteId = item.IconSpriteId;
                if (string.IsNullOrEmpty(iconSpriteId) && InventoryService.HasInstance)
                    iconSpriteId = InventoryService.Instance.GetTemplate(item.Id)?.IconSpriteId;

                SetDescriptionIconVisible(refs, true, iconSpriteId);
                refs.Name.Text        = item.Name ?? item.Id ?? string.Empty;
                refs.Stack.Text       = $"数量 {item.CurrentStack} / 最大堆叠 {item.MaxStack}";
                refs.Description.Text = item.Description ?? string.Empty;
            }
            else
            {
                SetDescriptionIconVisible(refs, false, null);
                refs.Name.Text        = string.Empty;
                refs.Stack.Text       = string.Empty;
                refs.Description.Text = refs.EmptyPlaceholder ?? string.Empty;
            }
        }

        #endregion

        #region Internal Builders

        /// <summary>有 sprite 时返回白色（不染色）；无 sprite 时使用配置色。</summary>
        private static Color SpriteAwareTint(string spriteId, Color fallback) =>
            string.IsNullOrEmpty(spriteId) ? fallback : Color.white;

        private static void BuildTitle(string inventoryId, UIPanelComponent parent, InventoryConfig config, UIPanelSpec pc)
        {
            var tc = config.TitleConfig;
            var text = !string.IsNullOrEmpty(tc.Text) ? tc.Text : (config.DisplayName ?? inventoryId);

            // UITextSpec.Position 已约定为相对父面板左下角的 rect 中心坐标，
            // 与 UIEntityFactory 创建 RectTransform 的 anchor/pivot 语义一致。
            var titleText = new UITextComponent($"{inventoryId}_Title", $"{inventoryId}_Title")
                .SetPosition(tc.Position.x, tc.Position.y)
                .SetColor(tc.TextColor)
                .SetAlignment(tc.Alignment)
                .SetText(text);
            ApplySupersample(titleText, tc.Size.x, tc.Size.y, tc.FontSize);
            titleText.SetVisible(true);
            parent.AddChild(titleText);
        }

        private static void BuildInfoTabs(string inventoryId, UIPanelComponent panel, DescUIRefs descRefs,
            List<InventoryInfoTabConfig> tabs)
        {
            var tooltipPanel = new UIPanelComponent($"{inventoryId}_InfoTabTooltip", "InfoTabTooltip")
                .SetVisible(false)
                .SetInteractable(false);
            var tooltipText = new UITextComponent($"{inventoryId}_InfoTabTooltipText", "InfoTabTooltipText")
                .SetText(string.Empty)
                .SetVisible(true);
            tooltipPanel.AddChild(tooltipText);
            panel.AddChild(tooltipPanel);

            foreach (var tab in tabs)
                AddInfoTab(panel, inventoryId, tab, tooltipPanel, tooltipText, () => ApplyInfoTabToDesc(descRefs, tab));
        }

        private static void AddInfoTab(
            UIPanelComponent parent,
            string inventoryId,
            InventoryInfoTabConfig tab,
            UIPanelComponent tooltipPanel,
            UITextComponent tooltipText,
            System.Action onClick)
        {
            if (tab == null || tab.ButtonConfig == null) return;
            var tabId = string.IsNullOrEmpty(tab.Id) ? "Tab" : tab.Id;
            var tabButton = tab.ButtonConfig.CreateComponent($"{inventoryId}_InfoTab_{tabId}", $"InfoTab_{tabId}");

            var iconConfig = tab.IconConfig ?? new UIIconSpec(17f, 17f, 22f, 22f);
            if (!string.IsNullOrEmpty(tab.IconSpriteId))
            {
                tabButton.AddChild(new UIPanelComponent($"{tabButton.Id}_Icon", $"{tabId}_Icon")
                    .SetPosition(iconConfig.Position.x, iconConfig.Position.y)
                    .SetSize(iconConfig.Size.x, iconConfig.Size.y)
                    .SetBackgroundSpriteId(tab.IconSpriteId)
                    .SetBackgroundColor(Color.white)
                    .SetVisible(iconConfig.Visible)
                    .SetInteractable(false));
            }

            tabButton.OnHoverEnter += _ =>
            {
                ApplyTooltipConfig(tooltipPanel, tooltipText, tab);
                tooltipPanel.SetVisible(true);
            };
            tabButton.OnHoverExit += _ => tooltipPanel.SetVisible(false);
            tabButton.OnClick += _ => onClick?.Invoke();

            parent.AddChild(tabButton);
        }

        private static void ApplyTooltipConfig(UIPanelComponent tooltipPanel, UITextComponent tooltipText,
            InventoryInfoTabConfig tab)
        {
            var panelConfig = tab.TooltipPanelConfig ?? new UIPanelSpec(132f, 28f);
            tooltipPanel.SetPosition(panelConfig.Position.x, panelConfig.Position.y)
                .SetSize(panelConfig.Size.x, panelConfig.Size.y)
                .SetScale(panelConfig.Scale.x, panelConfig.Scale.y)
                .SetBackgroundSpriteId(panelConfig.BackgroundSpriteId)
                .SetBackgroundColor(SpriteAwareTint(panelConfig.BackgroundSpriteId, panelConfig.BackgroundColor));

            var textConfig = tab.TooltipTextConfig ?? new UITextSpec(122f, 22f, panelConfig.Size.x * 0.5f,
                panelConfig.Size.y * 0.5f, 14, TextAnchor.MiddleCenter);
            tooltipText.SetPosition(textConfig.Position.x, textConfig.Position.y)
                .SetColor(textConfig.TextColor)
                .SetAlignment(textConfig.Alignment)
                .SetText(tab.Tooltip ?? string.Empty)
                .SetVisible(textConfig.Visible);
            ApplySupersample(tooltipText, textConfig.Size.x, textConfig.Size.y, textConfig.FontSize);
        }

        private static void ApplyInfoTabToDesc(DescUIRefs refs, InventoryInfoTabConfig tab)
        {
            if (refs == null || tab == null) return;
            var description = tab.ContentMode == "EntityHpSummary"
                ? BuildEntityHpSummaryText(tab)
                : tab.ContentMode == "ItemPrompt"
                    ? (string.IsNullOrEmpty(tab.Description) ? refs.EmptyPlaceholder : tab.Description)
                    : tab.Description;
            var showIcon = false;
            ApplyInfoToDesc(refs, tab.IconSpriteId, tab.Title, tab.SubTitle, description, showIcon);
        }

        private static void ApplyInfoToDesc(DescUIRefs refs, string iconSpriteId, string title, string subTitle,
            string description, bool showIcon = true)
        {
            if (refs == null) return;
            SetDescriptionIconVisible(refs, showIcon && !string.IsNullOrEmpty(iconSpriteId), iconSpriteId);
            refs.Name.Text = title ?? string.Empty;
            refs.Stack.Text = subTitle ?? string.Empty;
            refs.Description.Text = description ?? string.Empty;
        }

        private static void SetDescriptionIconVisible(DescUIRefs refs, bool visible, string iconSpriteId)
        {
            if (refs == null) return;

            if (refs.Icon != null)
            {
                refs.Icon.SetVisible(visible);
                refs.Icon.BackgroundSpriteId = visible ? iconSpriteId ?? string.Empty : string.Empty;
                refs.Icon.BackgroundColor = visible && !string.IsNullOrEmpty(iconSpriteId)
                    ? Color.white
                    : new Color(0f, 0f, 0f, 0f);
            }

            if (refs.IconDecorations == null) return;
            foreach (var decoration in refs.IconDecorations)
                decoration?.SetVisible(visible);
        }

        private static string BuildEntityHpSummaryText(InventoryInfoTabConfig tab)
        {
            var sb = new StringBuilder();
            var hp = EventProcessor.HasInstance && !string.IsNullOrEmpty(tab.EntityId)
                ? EventProcessor.Instance.TriggerEventMethod("GetEntityHp", new List<object> { tab.EntityId })
                : null;

            if (hp != null && hp.Count >= 4 && ResultCode.IsOk(hp))
            {
                var currentHp = System.Convert.ToSingle(hp[1]);
                var maxHp = System.Convert.ToSingle(hp[2]);
                var isDead = hp[3] is bool dead && dead;
                sb.Append("生命: ").Append(FormatStatNumber(currentHp)).Append(" / ").AppendLine(FormatStatNumber(maxHp));
                sb.Append("状态: ").AppendLine(isDead ? "已倒下" : "正常");
            }
            else
            {
                sb.AppendLine("生命: 未注册");
                sb.AppendLine("状态: 未知");
            }

            if (tab.ExtraLines != null)
            {
                foreach (var line in tab.ExtraLines)
                    if (!string.IsNullOrEmpty(line)) sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatStatNumber(float value)
        {
            var rounded = Mathf.Round(value);
            return Mathf.Abs(value - rounded) < 0.01f
                ? Mathf.RoundToInt(value).ToString()
                : value.ToString("0.#");
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
                var slotVisual = GetSlotVisual(config, slotIdx);
                if (slotVisual != null && slotVisual.OverridePosition)
                {
                    x = slotVisual.Position.x;
                    y = slotVisual.Position.y;
                }

                var slotBtn = new UIButtonComponent($"{inventoryId}_Slot_{i}", $"Slot_{i}")
                    .SetPosition(x, y)
                    .SetSize(sc.SlotWidth, sc.SlotHeight)
                    .SetVisible(true)
                    .SetButtonSpriteId(sc.SlotBackgroundSpriteId)
                    .SetButtonColor(GetSlotButtonColor(inventoryId))
                    .SetInteractable(true);

                // 图标：slot 内居中。
                var iconSize = Mathf.Min(sc.SlotWidth, sc.SlotHeight) * 0.72f;
                var iconPanel = new UIPanelComponent($"{inventoryId}_Slot_{i}_Icon", $"Slot_{i}_Icon")
                    .SetPosition(sc.SlotWidth * 0.5f, sc.SlotHeight * 0.5f)
                    .SetSize(iconSize, iconSize)
                    .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                    .SetVisible(true);
                slotBtn.AddChild(iconPanel);
                RegisterEmptySlotVisual(iconPanel.Id, slotVisual);

                // 名称文本：保留给需要在 slot 顶部显示名称的场景，默认隐藏。
                // 子组件 Position 使用父 slot 左下角为原点的中心坐标。
                var nameText = new UITextComponent($"{inventoryId}_Slot_{i}_NameText", $"Slot_{i}_Name")
                    .SetPosition(sc.SlotWidth * 0.5f, sc.SlotHeight * 0.5f + sc.SlotHeight * 0.38f)
                    .SetColor(Color.white)
                    .SetAlignment(TextAnchor.MiddleCenter)
                    .SetText(string.Empty);
                ApplySupersample(nameText, Mathf.Max(1f, sc.SlotWidth - 4f), sc.SlotHeight * 0.18f, stackFont);
                nameText.SetVisible(false);
                slotBtn.AddChild(nameText);

                // 数量：slot 底部右对齐。
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

                // 点击 slot 后更新描述面板。
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

                AddSlotVisualLabel(panel, inventoryId, slotIdx, slotVisual, x, y, sc.SlotWidth, sc.SlotHeight);
            }

            return refs;
        }

        private static Color GetSlotButtonColor(string inventoryId)
        {
            if (inventoryId == HotbarInventoryId) return new Color(1f, 0.95f, 0.82f, 0.96f);
            return Color.white;
        }

        private static InventorySlotVisualConfig GetSlotVisual(InventoryConfig config, int slotIndex)
        {
            if (config?.SlotVisuals == null) return null;
            for (var i = 0; i < config.SlotVisuals.Count; i++)
            {
                var visual = config.SlotVisuals[i];
                if (visual != null && visual.SlotIndex == slotIndex) return visual;
            }
            return null;
        }

        private static void RegisterEmptySlotVisual(string iconId, InventorySlotVisualConfig visual)
        {
            if (string.IsNullOrEmpty(iconId)) return;
            if (visual == null || string.IsNullOrEmpty(visual.EmptyHintSpriteId))
            {
                EmptySlotVisuals.Remove(iconId);
                return;
            }

            EmptySlotVisuals[iconId] = new EmptySlotVisual(visual.EmptyHintSpriteId, visual.EmptyHintColor);
        }

        private static void AddSlotVisualLabel(UIPanelComponent panel, string inventoryId, int slotIndex,
            InventorySlotVisualConfig visual, float x, float y, float slotWidth, float slotHeight)
        {
            if (panel == null || visual == null || string.IsNullOrEmpty(visual.Label)) return;

            var labelConfig = visual.LabelConfig ?? new UITextSpec();
            var labelX = labelConfig.Position == Vector2.zero ? x : labelConfig.Position.x;
            var labelY = labelConfig.Position == Vector2.zero ? y + slotHeight * 0.5f + 12f : labelConfig.Position.y;
            var label = new UITextComponent($"{inventoryId}_Slot_{slotIndex}_Label", $"Slot_{slotIndex}_Label")
                .SetPosition(labelX, labelY)
                .SetColor(labelConfig.TextColor)
                .SetAlignment(labelConfig.Alignment)
                .SetText(visual.Label)
                .SetVisible(labelConfig.Visible);
            ApplySupersample(label, Mathf.Max(1f, labelConfig.Size.x > 0f ? labelConfig.Size.x : slotWidth + 12f),
                Mathf.Max(1f, labelConfig.Size.y > 0f ? labelConfig.Size.y : 22f),
                Mathf.Max(1, labelConfig.FontSize));
            panel.AddChild(label);
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
            // 描述面板文本：名称、数量/状态、正文。
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
                Icon = null,
                IconDecorations = null,
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
            var white = new Color(0.92f, 1f, 1f, 0.92f);

            var root = new UIPanelComponent($"{inventoryId}_Slot_{slotIndex}_Selected", $"Slot_{slotIndex}_Selected")
                .SetPosition(slotWidth * 0.5f, slotHeight * 0.5f)
                .SetSize(rootW, rootH)
                .SetBackgroundColor(new Color(0.02f, 0.70f, 1f, 0.42f))
                .SetVisible(false);

            root.AddChild(new UIPanelComponent($"{root.Id}_InnerGlow", "InnerGlow")
                .SetPosition(cx, cy)
                .SetSize(slotWidth - 8f, slotHeight - 8f)
                .SetBackgroundColor(new Color(0.30f, 1f, 1f, 0.24f)));

            var markerW = 11f;
            root.AddChild(new UIPanelComponent($"{root.Id}_MarkerLeft", "MarkerLeft")
                .SetPosition(markerW * 0.5f, cy)
                .SetSize(markerW, rootH - 10f)
                .SetBackgroundColor(white));

            root.AddChild(new UIPanelComponent($"{root.Id}_MarkerTop", "MarkerTop")
                .SetPosition(cx, rootH - 5f)
                .SetSize(rootW - 14f, 10f)
                .SetBackgroundColor(cyan));

            return root;
        }

        private static void ApplyDescriptionChrome(UIPanelComponent panel, float width, float height)
        {
            var cx = width * 0.5f;
            var cy = height * 0.5f;
            var gold = new Color(0.88f, 0.69f, 0.35f, 0.62f);

            panel.SetBackgroundSpriteId(string.Empty)
                .SetBackgroundColor(new Color(0.035f, 0.030f, 0.032f, 0.92f));

            AddHotbarBorder(panel, $"{panel.Id}_DescOuter", cx, cy, width, height, 2f, gold);
        }

        /// <summary>
        /// 根据 <see cref="UITextSpec"/> 创建文本组件。
        /// Position 采用“父面板左下角 + 文本 rect 中心”的约定。
        /// </summary>
        private static UITextComponent BuildDescText(
            string daoId, string name,
            UITextSpec cfg,
            DescriptionPanelConfig dp,
            string initialText)
        {
            cfg = cfg ?? new UITextSpec();
            // 与 BuildTitle 一致，Position 已是相对父面板左下角的中心坐标。
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
        /// 对文本应用 N 倍超采样：FontSize、Size 乘 N，Scale 乘 1/N。
        /// 视觉尺寸不变，但文本以更高分辨率栅格化，减少模糊。
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
