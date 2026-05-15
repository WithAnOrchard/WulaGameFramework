using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao.UIConfig;

// 本文件不 <c>using</c> UIManager 模块。查 GameObject 走 EVT_GET_UI_GAMEOBJECT 事件。

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager
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
                icon.BackgroundSpriteId = item.IconSpriteId ?? string.Empty;
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
                Icons  = new UIPanelComponent[config.SlotsPerPage],
                Names  = new UITextComponent [config.SlotsPerPage],
                Stacks = new UITextComponent [config.SlotsPerPage],
            };

            var stackFont = Mathf.Max(16, Mathf.RoundToInt(sc.SlotHeight * 0.24f));

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
                var iconSize = Mathf.Min(sc.SlotWidth, sc.SlotHeight) * 0.72f;
                var iconPanel = new UIPanelComponent($"{inventoryId}_Slot_{i}_Icon", $"Slot_{i}_Icon")
                    .SetPosition(sc.SlotWidth * 0.5f, sc.SlotHeight * 0.5f)
                    .SetSize(iconSize, iconSize)
                    .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                    .SetVisible(true);
                slotBtn.AddChild(iconPanel);

                // 名称（slot 顶部）
                var nameText = new UITextComponent($"{inventoryId}_Slot_{i}_NameText", $"Slot_{i}_Name")
                    .SetPosition(0f, sc.SlotHeight * 0.38f)
                    .SetColor(Color.white)
                    .SetAlignment(TextAnchor.MiddleCenter)
                    .SetText(string.Empty);
                ApplySupersample(nameText, Mathf.Max(1f, sc.SlotWidth - 4f), sc.SlotHeight * 0.18f, stackFont);
                nameText.SetVisible(false);
                slotBtn.AddChild(nameText);

                // 数量（slot 底部右对齐）
                var stackText = new UITextComponent($"{inventoryId}_Slot_{i}_StackText", $"Slot_{i}_Stack")
                    .SetPosition(-sc.SlotWidth * 0.08f, -sc.SlotHeight * 0.34f)
                    .SetColor(new Color(1f, 0.85f, 0.4f, 1f))
                    .SetAlignment(TextAnchor.MiddleRight)
                    .SetText(string.Empty);
                ApplySupersample(stackText, Mathf.Max(1f, sc.SlotWidth - 8f), sc.SlotHeight * 0.28f, stackFont);
                stackText.SetVisible(true);
                slotBtn.AddChild(stackText);

                refs.Icons[i]  = iconPanel;
                refs.Names[i]  = nameText;
                refs.Stacks[i] = stackText;

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

            // 图标
            var ic = dc.IconConfig ?? new DescriptionIconConfig();
            var iconPanel = new UIPanelComponent($"{inventoryId}_DescIcon", $"{inventoryId}_DescriptionIcon")
                .SetPosition(ic.Position.x, ic.Position.y)
                .SetSize(ic.Size.x, ic.Size.y)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(ic.IsVisible);
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
                .SetColor(cfg.TextColor)
                .SetAlignment(cfg.Alignment)
                .SetText(initialText ?? string.Empty);
            ApplySupersample(t, cfg.Size.x, cfg.Size.y, cfg.FontSize);
            t.SetVisible(cfg.IsVisible);
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
