using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.UIManager.Dao.CommonComponents;
using EssSystem.Core.EssManagers.UIManager.Entity;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager
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
        #region Public API

        /// <summary>
        /// 构建完整 UIPanel 树。返回的 refs 用于后续 EVT_CHANGED / 点击事件原地刷新。
        /// </summary>
        public static (UIPanelComponent panel, SlotUIRefs slotRefs, DescUIRefs descRefs)
            BuildPanelTree(string inventoryId, InventoryConfig config)
        {
            var pc = config.PanelConfig;
            var panel = new UIPanelComponent(inventoryId, $"{config.DisplayName} - {inventoryId}")
                .SetPosition(pc.PanelPosition.x, pc.PanelPosition.y)
                .SetSize(pc.PanelWidth, pc.PanelHeight)
                .SetScale(pc.PanelScale.x, pc.PanelScale.y)
                .SetBackgroundSpriteId(pc.BackgroundSpriteId)
                .SetBackgroundColor(SpriteAwareTint(pc.BackgroundSpriteId, pc.BackgroundColor))
                .SetVisible(true);

            // 0) 标题（容器名称）
            if (config.ShowTitle && config.TitleConfig != null && config.TitleConfig.IsVisible)
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

        /// <summary>给 slot 的 GameObject 挂上 <see cref="InventorySlotDragHandler"/>。需在 UI 注册之后调用。</summary>
        public static void AttachSlotDragHandlers(string inventoryId, int slotCount)
        {
            for (var i = 0; i < slotCount; i++)
            {
                var slotGo = UIEntity.GetGameObjectById($"{inventoryId}_Slot_{i}");
                if (slotGo == null) continue;

                var handler = slotGo.GetComponent<InventorySlotDragHandler>()
                              ?? slotGo.AddComponent<InventorySlotDragHandler>();
                handler.InventoryId = inventoryId;
                handler.SlotIndex   = i;

                var iconGo = UIEntity.GetGameObjectById($"{inventoryId}_Slot_{i}_Icon");
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
                icon.BackgroundSpriteId = item.IconSpriteId ?? string.Empty;
                icon.BackgroundColor    = Color.white;
                name.Text  = item.Name ?? item.Id ?? string.Empty;
                stack.Text = $"{item.CurrentStack}/{item.MaxStack}";
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
                refs.Icon.BackgroundSpriteId = item.IconSpriteId ?? string.Empty;
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

        private static void BuildTitle(string inventoryId, UIPanelComponent parent, InventoryConfig config, PanelConfig pc)
        {
            var tc = config.TitleConfig;
            var text = !string.IsNullOrEmpty(tc.CustomText) ? tc.CustomText : (config.DisplayName ?? inventoryId);

            // TitleConfig.Position 用左下角约定 → UITextComponent 用中心约定，转换：减去 (PanelW/2, PanelH/2)
            var posX = tc.Position.x - pc.PanelWidth  * 0.5f;
            var posY = tc.Position.y - pc.PanelHeight * 0.5f;

            var titleText = new UITextComponent($"{inventoryId}_Title", $"{inventoryId}_Title")
                .SetPosition(posX, posY)
                .SetSize(tc.Size.x, tc.Size.y)
                .SetFontSize(tc.FontSize)
                .SetColor(tc.TextColor)
                .SetAlignment(tc.Alignment)
                .SetText(text);
            titleText.SetVisible(true);
            parent.AddChild(titleText);
        }

        private static SlotUIRefs BuildSlots(string inventoryId, UIPanelComponent panel, InventoryConfig config, DescUIRefs descRefs)
        {
            var inv = InventoryService.Instance.GetInventory(inventoryId);
            var sc  = config.SlotConfig;
            var refs = new SlotUIRefs
            {
                Icons  = new UIPanelComponent[config.SlotsPerPage],
                Names  = new UITextComponent [config.SlotsPerPage],
                Stacks = new UITextComponent [config.SlotsPerPage],
            };

            var smallFont = Mathf.Max(8, Mathf.RoundToInt(sc.SlotHeight * 0.13f));

            for (var i = 0; i < config.SlotsPerPage; i++)
            {
                var slotIdx = i; // 闭包捕获用
                var row = i / sc.SlotsPerRow;
                var col = i % sc.SlotsPerRow;
                var x = sc.StartOffsetX + col * (sc.SlotWidth + sc.SlotSpacingX);
                var y = sc.StartOffsetY - row * (sc.SlotHeight + sc.SlotSpacingY);

                var slotBtn = new UIButtonComponent($"{inventoryId}_Slot_{i}", $"Slot_{i}")
                    .SetPosition(x, y)
                    .SetSize(sc.SlotWidth, sc.SlotHeight)
                    .SetVisible(true)
                    .SetButtonSpriteId(sc.SlotBackgroundSpriteId)
                    .SetInteractable(true);

                // 图标（slot 居中）
                var iconSize = Mathf.Min(sc.SlotWidth, sc.SlotHeight) * 0.55f;
                var iconPanel = new UIPanelComponent($"{inventoryId}_Slot_{i}_Icon", $"Slot_{i}_Icon")
                    .SetPosition(sc.SlotWidth * 0.5f, sc.SlotHeight * 0.5f)
                    .SetSize(iconSize, iconSize)
                    .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                    .SetVisible(true);
                slotBtn.AddChild(iconPanel);

                // 名称（slot 顶部）
                var nameText = new UITextComponent($"{inventoryId}_Slot_{i}_NameText", $"Slot_{i}_Name")
                    .SetPosition(0f, sc.SlotHeight * 0.38f)
                    .SetSize(Mathf.Max(1f, sc.SlotWidth - 4f), sc.SlotHeight * 0.18f)
                    .SetFontSize(smallFont)
                    .SetColor(Color.white)
                    .SetAlignment(TextAnchor.MiddleCenter)
                    .SetText(string.Empty);
                nameText.SetVisible(true);
                slotBtn.AddChild(nameText);

                // 数量（slot 底部右对齐）
                var stackText = new UITextComponent($"{inventoryId}_Slot_{i}_StackText", $"Slot_{i}_Stack")
                    .SetPosition(0f, -sc.SlotHeight * 0.38f)
                    .SetSize(Mathf.Max(1f, sc.SlotWidth - 4f), sc.SlotHeight * 0.18f)
                    .SetFontSize(smallFont)
                    .SetColor(new Color(1f, 0.85f, 0.4f, 1f))
                    .SetAlignment(TextAnchor.MiddleRight)
                    .SetText(string.Empty);
                stackText.SetVisible(true);
                slotBtn.AddChild(stackText);

                refs.Icons[i]  = iconPanel;
                refs.Names[i]  = nameText;
                refs.Stacks[i] = stackText;

                ApplyItemToSlot(iconPanel, nameText, stackText, inv?.GetSlot(slotIdx)?.Item);

                // 点击 slot → 更新描述面板
                if (descRefs != null)
                {
                    var capturedRefs = descRefs;
                    slotBtn.OnClick += _ =>
                    {
                        var clickedItem = InventoryService.Instance.GetInventory(inventoryId)?.GetSlot(slotIdx)?.Item;
                        ApplyItemToDesc(capturedRefs, clickedItem);
                    };
                }

                panel.AddChild(slotBtn);
            }

            return refs;
        }

        private static void BuildCloseButton(string inventoryId, UIPanelComponent panel, ButtonConfig cb)
        {
            var closeBtn = new UIButtonComponent($"{inventoryId}_CloseButton", "CloseButton", cb.ButtonText)
                .SetPosition(cb.Position.x, cb.Position.y)
                .SetSize(cb.Size.x, cb.Size.y)
                .SetScale(cb.Scale.x, cb.Scale.y)
                .SetButtonSpriteId(cb.ButtonSpriteId)
                .SetVisible(cb.IsVisible)
                .SetInteractable(cb.IsInteractable);

            closeBtn.OnClick += _ => EventProcessor.Instance.TriggerEventMethod(
                InventoryManager.EVT_CLOSE_UI, new List<object> { inventoryId });

            panel.AddChild(closeBtn);
        }

        private static DescUIRefs BuildDescriptionPanel(string inventoryId, UIPanelComponent parent, DescriptionPanelConfig dp)
        {
            var descPanel = new UIPanelComponent($"{inventoryId}_DescPanel", $"{inventoryId}_DescriptionPanel")
                .SetPosition(dp.Offset.x, dp.Offset.y)
                .SetSize(dp.Width, dp.Height)
                .SetBackgroundSpriteId(dp.BackgroundSpriteId)
                .SetBackgroundColor(SpriteAwareTint(dp.BackgroundSpriteId, dp.BackgroundColor))
                .SetVisible(true);

            // 图标
            var ic = dp.IconConfig ?? new DescriptionIconConfig();
            var iconPanel = new UIPanelComponent($"{inventoryId}_DescIcon", $"{inventoryId}_DescriptionIcon")
                .SetPosition(ic.Position.x, ic.Position.y)
                .SetSize(ic.Size.x, ic.Size.y)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(ic.IsVisible);
            descPanel.AddChild(iconPanel);

            // 三个文本子组件
            var nameText  = BuildDescText($"{inventoryId}_DescName",  $"{inventoryId}_DescriptionName",
                                           dp.NameConfig,  dp, string.Empty);
            var stackText = BuildDescText($"{inventoryId}_DescStack", $"{inventoryId}_DescriptionStack",
                                           dp.StackConfig, dp, string.Empty);
            var descText  = BuildDescText($"{inventoryId}_DescText",  $"{inventoryId}_DescriptionText",
                                           dp.DescTextConfig, dp, dp.EmptyPlaceholder ?? string.Empty);
            descPanel.AddChild(nameText);
            descPanel.AddChild(stackText);
            descPanel.AddChild(descText);

            parent.AddChild(descPanel);

            return new DescUIRefs
            {
                Icon = iconPanel,
                Name = nameText,
                Stack = stackText,
                Description = descText,
                EmptyPlaceholder = dp.EmptyPlaceholder ?? string.Empty,
            };
        }

        /// <summary>
        /// 用 <see cref="DescriptionTextElementConfig"/> 创建一个 UITextComponent。
        /// 配置 Position 是「rect 中心相对父面板左下角」，内部转成 UITextComponent 的「中心相对父中心」。
        /// </summary>
        private static UITextComponent BuildDescText(
            string daoId, string name,
            DescriptionTextElementConfig cfg,
            DescriptionPanelConfig dp,
            string initialText)
        {
            cfg = cfg ?? new DescriptionTextElementConfig();
            var posX = cfg.Position.x - dp.Width  * 0.5f;
            var posY = cfg.Position.y - dp.Height * 0.5f;

            var t = new UITextComponent(daoId, name)
                .SetPosition(posX, posY)
                .SetSize(cfg.Size.x, cfg.Size.y)
                .SetFontSize(cfg.FontSize)
                .SetColor(cfg.TextColor)
                .SetAlignment(cfg.Alignment)
                .SetText(initialText ?? string.Empty);
            t.SetVisible(cfg.IsVisible);
            return t;
        }

        #endregion
    }
}
