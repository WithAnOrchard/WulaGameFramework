using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao;
using EssSystem.Core.Presentation.InputManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.UI
{
    public static class SkillSelectionPanel
    {
        private const string PanelDaoId = "__skill_selection_panel";
        private const int SlotCount = 4;
        private const float TextSupersample = 4f;
        private const string DefaultSkillIconSpriteId = "Common/UI/Inventory/backpack_tab_skills";
        private const string GroupAll = "__all";
        private const string GroupChangedIcons = "__changed_icons";
        private const string GroupOther = "__other";
        private const string ChangedIconPathMarker = "Common/Skills/Icons/!";

        private static readonly Vector2 CanvasCenter = new(960f, 540f);
        private static readonly Color PanelBg = new(0.035f, 0.038f, 0.046f, 0.98f);
        private static readonly Color FrameBg = new(0.11f, 0.12f, 0.16f, 0.98f);
        private static readonly Color HeaderBg = new(0.13f, 0.14f, 0.19f, 0.96f);
        private static readonly Color InnerBg = new(0.018f, 0.022f, 0.030f, 0.90f);
        private static readonly Color RowBg = new(0.075f, 0.088f, 0.108f, 0.96f);
        private static readonly Color RowSelectedBg = new(0.18f, 0.27f, 0.35f, 0.98f);
        private static readonly Color IconBg = new(0.08f, 0.085f, 0.095f, 0.92f);
        private static readonly Color SlotBg = new(0.07f, 0.095f, 0.110f, 0.96f);
        private static readonly Color SlotBoundBg = new(0.095f, 0.17f, 0.145f, 0.98f);
        private static readonly Color CloseBg = new(0.42f, 0.08f, 0.07f, 0.96f);
        private static readonly Color CategoryBg = new(0.08f, 0.10f, 0.13f, 0.98f);
        private static readonly Color CategoryActiveBg = new(0.20f, 0.30f, 0.38f, 0.98f);
        private static readonly Color Accent = new(0.48f, 0.68f, 0.82f, 1f);
        private static readonly Color TextBright = new(0.96f, 0.92f, 0.80f, 1f);
        private static readonly Color TextNormal = new(0.86f, 0.90f, 0.92f, 1f);
        private static readonly Color TextMuted = new(0.58f, 0.65f, 0.70f, 1f);
        private static readonly Color TextGood = new(0.66f, 1f, 0.72f, 1f);
        private static readonly Color TextWarn = new(1f, 0.55f, 0.45f, 1f);

        private static readonly SkillCategoryTab[] CategoryTabs =
        {
            new(GroupAll, "全部技能"),
            new(GroupChangedIcons, "已换图标"),
            new("Unfinished", "未完成", "Unfinished"),
            new("Elemental", "元素", "Elemental"),
            new("Martial", "武技", "Martial"),
            new("Defense", "防御", "Defense"),
            new("Assassin", "刺客", "Assassin"),
            new("Gunner", "枪械/弹幕", "Gunner", "Barrage"),
            new("TrapBomb", "陷阱/爆破", "Trap", "Bomb"),
            new("SummonDeploy", "召唤/部署", "Summon", "Deployable"),
            new("SupportControl", "支援/控制", "Support", "Control", "Zone"),
            new("Mobility", "位移/其他", "Mobility", GroupOther),
        };

        private static readonly Dictionary<string, UIButtonComponent> SkillButtons = new();
        private static readonly Dictionary<string, UIButtonComponent> CategoryButtons = new();
        private static readonly Dictionary<string, string> SkillIconPanelIds = new();
        private static readonly Dictionary<string, string> SkillIconSpriteIds = new();
        private static readonly UIButtonComponent[] SlotButtons = new UIButtonComponent[SlotCount];
        private static readonly UIPanelComponent[] SlotIcons = new UIPanelComponent[SlotCount];
        private static readonly UITextComponent[] SlotTexts = new UITextComponent[SlotCount];
        private static readonly UITextComponent[] SlotMetaTexts = new UITextComponent[SlotCount];

        private static UIPanelComponent _panel;
        private static UITextComponent _selectedText;
        private static UITextComponent _statusText;
        private static string _entityId;
        private static string _selectedSkillId;
        private static string _activeGroup = GroupAll;

        public static bool IsOpen()
        {
            if (!EventProcessor.HasInstance) return false;
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { PanelDaoId });
            return ResultCode.IsOk(result) && result.Count >= 2 && result[1] is GameObject go && go != null;
        }

        public static void Toggle(string entityId)
        {
            if (IsOpen()) Close();
            else Open(entityId);
        }

        public static void Open(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !EventProcessor.HasInstance) return;
            if (IsOpen()) Close();

            _entityId = entityId;
            _selectedSkillId = null;
            ClearCaches();

            _panel = BuildPanel();
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity", new List<object> { PanelDaoId, _panel });

            ApplySkillRowIcons();
            UpdateSlotButtons();
            InstallDragHandlers();
            ShowStatus("拖动技能到右侧栏位即可覆盖绑定，也可以选中后按 1-4。");
        }

        public static void Close()
        {
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "UnregisterUIEntity", new List<object> { PanelDaoId });

            _panel = null;
            _selectedText = null;
            _statusText = null;
            _entityId = null;
            _selectedSkillId = null;
            ClearCaches();
        }

        public static void Tick()
        {
            if (!IsOpen()) return;

            var input = InputManager.TryGetInstance();
            if (input == null) return;

            if (input.IsDown("Cancel"))
            {
                Close();
                return;
            }

            for (var i = 0; i < SlotCount; i++)
            {
                if (input.IsDown(InputManager.GetHotbarUseActionName(i)))
                    BindSelectedToSlot(i);
            }
        }

        private static void ClearCaches()
        {
            SkillButtons.Clear();
            CategoryButtons.Clear();
            SkillIconPanelIds.Clear();
            SkillIconSpriteIds.Clear();
            Array.Clear(SlotButtons, 0, SlotButtons.Length);
            Array.Clear(SlotIcons, 0, SlotIcons.Length);
            Array.Clear(SlotTexts, 0, SlotTexts.Length);
            Array.Clear(SlotMetaTexts, 0, SlotMetaTexts.Length);
        }

        private static UIPanelComponent BuildPanel()
        {
            const float w = 1080f;
            const float h = 720f;
            const float margin = 26f;
            const float listW = 720f;
            const float listH = 360f;
            const float listX = margin + listW * 0.5f + 12f;
            const float contentY = 294f;
            const float rightX = 910f;

            var panel = new UIPanelComponent(PanelDaoId, "SkillSelectionPanel")
                .SetPosition(CanvasCenter.x, CanvasCenter.y)
                .SetSize(w, h)
                .SetBackgroundColor(PanelBg);

            AddFrame(panel, w, h);

            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_Header", "Header")
                .SetPosition(w * 0.5f, h - 42f)
                .SetSize(w - 44f, 58f)
                .SetBackgroundColor(HeaderBg));
            panel.AddChild(BuildText($"{PanelDaoId}_Title", "Title", "技能测试面板",
                w * 0.5f, h - 34f, 340f, 42f, 26, TextBright, TextAnchor.MiddleCenter));
            panel.AddChild(BuildText($"{PanelDaoId}_Hint", "Hint",
                "点击上方大类筛选技能，滚轮浏览，拖到右侧栏位覆盖绑定，Esc 关闭",
                w * 0.5f, h - 74f, 760f, 24f, 13, TextMuted, TextAnchor.MiddleCenter));

            var closeButton = new UIButtonComponent($"{PanelDaoId}_Close", "Close", "")
                .SetPosition(w - 46f, h - 42f)
                .SetSize(36f, 36f)
                .SetButtonColor(CloseBg);
            closeButton.AddChild(BuildText($"{PanelDaoId}_CloseText", "CloseText", "X",
                18f, 18f, 34f, 34f, 18, Color.white, TextAnchor.MiddleCenter));
            closeButton.OnClick += _ => Close();
            panel.AddChild(closeButton);

            BuildCategoryButtons(panel, w, h);

            var visibleCount = GetDefinitions().Count;
            var totalCount = GetAllDefinitions().Count;
            const float listTitleW = 260f;
            var listTitleX = listX - listW * 0.5f + listTitleW * 0.5f;
            panel.AddChild(BuildText($"{PanelDaoId}_ListTitle", "ListTitle",
                $"{GetActiveGroupLabel()} ({visibleCount}/{totalCount})",
                listTitleX, h - 218f, listTitleW, 28f, 16, TextBright, TextAnchor.MiddleLeft));
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_ListFrame", "ListFrame")
                .SetPosition(listX, contentY)
                .SetSize(listW + 18f, listH + 18f)
                .SetBackgroundColor(FrameBg));

            var scroll = new UIScrollViewComponent($"{PanelDaoId}_SkillScroll", "SkillScroll")
                .SetPosition(listX, contentY)
                .SetSize(listW, listH)
                .SetBackgroundColor(InnerBg)
                .SetContentPadding(10)
                .SetItemSpacing(7f)
                .SetScrollSensitivity(62f);
            BuildSkillRows(scroll, listW - 20f);
            panel.AddChild(scroll);

            panel.AddChild(BuildText($"{PanelDaoId}_SlotTitle", "SlotTitle", "技能栏位",
                rightX, h - 218f, 190f, 28f, 17, TextBright, TextAnchor.MiddleCenter));
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_SlotBack", "SlotBack")
                .SetPosition(rightX, contentY)
                .SetSize(294f, listH + 18f)
                .SetBackgroundColor(new Color(0.018f, 0.021f, 0.026f, 0.56f)));

            for (var i = 0; i < SlotCount; i++)
                BuildSlotButton(panel, i, rightX, h - 276f - i * 82f);

            _selectedText = BuildText($"{PanelDaoId}_Selected", "Selected", "当前选择：无",
                rightX, 92f, 260f, 30f, 14, TextNormal, TextAnchor.MiddleCenter);
            panel.AddChild(_selectedText);

            _statusText = BuildText($"{PanelDaoId}_Status", "Status", "",
                w * 0.5f, 36f, w - 70f, 32f, 14, TextNormal, TextAnchor.MiddleCenter);
            panel.AddChild(_statusText);
            return panel;
        }

        private static void AddFrame(UIPanelComponent panel, float w, float h)
        {
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_OuterTop", "FrameTop")
                .SetPosition(w * 0.5f, h - 3f).SetSize(w, 6f).SetBackgroundColor(FrameBg));
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_OuterBottom", "FrameBottom")
                .SetPosition(w * 0.5f, 3f).SetSize(w, 6f).SetBackgroundColor(FrameBg));
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_OuterLeft", "FrameLeft")
                .SetPosition(3f, h * 0.5f).SetSize(6f, h).SetBackgroundColor(FrameBg));
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_OuterRight", "FrameRight")
                .SetPosition(w - 3f, h * 0.5f).SetSize(6f, h).SetBackgroundColor(FrameBg));
            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_AccentLine", "AccentLine")
                .SetPosition(w * 0.5f, h - 94f).SetSize(w - 62f, 2f).SetBackgroundColor(Accent));
        }

        private static void BuildCategoryButtons(UIPanelComponent panel, float w, float h)
        {
            const int perRow = 6;
            const float tabW = 150f;
            const float tabH = 32f;
            const float gap = 12f;
            var totalW = perRow * tabW + (perRow - 1) * gap;
            var startX = (w - totalW) * 0.5f + tabW * 0.5f;

            for (var i = 0; i < CategoryTabs.Length; i++)
            {
                var tab = CategoryTabs[i];
                var col = i % perRow;
                var row = i / perRow;
                var x = startX + col * (tabW + gap);
                var y = h - 124f - row * 42f;
                var active = string.Equals(_activeGroup, tab.Key, StringComparison.Ordinal);
                var button = new UIButtonComponent($"{PanelDaoId}_Category_{SafeId(tab.Key)}", $"Category_{tab.Key}", "")
                    .SetPosition(x, y)
                    .SetSize(tabW, tabH)
                    .SetButtonColor(active ? CategoryActiveBg : CategoryBg);
                button.AddChild(BuildText($"{button.Id}_Text", "Text", $"{tab.Label} {CountDefinitionsForGroup(tab.Key)}",
                    tabW * 0.5f, tabH * 0.5f, tabW - 12f, tabH, 13,
                    active ? TextBright : TextNormal, TextAnchor.MiddleCenter));

                var captured = tab.Key;
                button.OnClick += _ => SetActiveGroup(captured);
                CategoryButtons[tab.Key] = button;
                panel.AddChild(button);
            }
        }

        private static void BuildSkillRows(UIScrollViewComponent scroll, float rowW)
        {
            var definitions = GetDefinitions();
            if (definitions.Count == 0)
            {
                scroll.AddContentChild(BuildText($"{PanelDaoId}_NoSkills", "NoSkills",
                    "当前分类没有技能。",
                    rowW * 0.5f, 28f, rowW, 56f, 16, TextWarn, TextAnchor.MiddleCenter));
                return;
            }

            const float rowH = 76f;
            for (var i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                var id = $"{PanelDaoId}_Skill_{SafeId(def.Id)}";
                var row = new UIButtonComponent(id, $"Skill_{def.Id}", "")
                    .SetPosition(rowW * 0.5f, rowH * 0.5f)
                    .SetSize(rowW, rowH)
                    .SetButtonColor(RowBg);

                var iconId = $"{id}_Icon";
                var iconSpriteId = GetSkillIcon(def);
                row.AddChild(new UIPanelComponent(iconId, "Icon")
                    .SetPosition(40f, 38f)
                    .SetSize(50f, 50f)
                    .SetBackgroundSpriteId(iconSpriteId)
                    .SetBackgroundColor(Color.white));

                row.AddChild(BuildText($"{id}_Name", "Name", GetSkillTitle(def),
                    188f, 52f, 220f, 24f, 15, TextBright, TextAnchor.MiddleLeft));
                row.AddChild(BuildText($"{id}_Meta", "Meta", GetSkillMeta(def),
                    188f, 26f, 220f, 20f, 12, TextMuted, TextAnchor.MiddleLeft));
                row.AddChild(BuildText($"{id}_Desc", "Desc", GetSkillDescription(def),
                    506f, 38f, 338f, 54f, 12, TextNormal, TextAnchor.MiddleLeft));

                var captured = def;
                row.OnClick += _ => SelectSkill(captured);
                SkillButtons[def.Id] = row;
                SkillIconPanelIds[def.Id] = iconId;
                SkillIconSpriteIds[def.Id] = iconSpriteId;
                scroll.AddContentChild(row);
            }
        }

        private static void BuildSlotButton(UIPanelComponent panel, int slotIndex, float x, float y)
        {
            var id = $"{PanelDaoId}_Slot{slotIndex + 1}";
            var button = new UIButtonComponent(id, $"Slot{slotIndex + 1}", "")
                .SetPosition(x, y)
                .SetSize(260f, 74f)
                .SetButtonColor(SlotBg);

            SlotIcons[slotIndex] = new UIPanelComponent($"{id}_Icon", "Icon")
                .SetPosition(38f, 37f)
                .SetSize(42f, 42f)
                .SetBackgroundSpriteId(DefaultSkillIconSpriteId)
                .SetBackgroundColor(new Color(1f, 1f, 1f, 0f));
            button.AddChild(SlotIcons[slotIndex]);

            button.AddChild(BuildText($"{id}_Label", "Label", $"槽位 {slotIndex + 1}",
                166f, 58f, 156f, 18f, 12, TextMuted, TextAnchor.MiddleLeft));
            SlotTexts[slotIndex] = BuildText($"{id}_Skill", "Skill", "空",
                166f, 38f, 156f, 24f, 15, TextNormal, TextAnchor.MiddleLeft);
            SlotMetaTexts[slotIndex] = BuildText($"{id}_Meta", "Meta", "",
                166f, 17f, 156f, 18f, 11, TextMuted, TextAnchor.MiddleLeft);
            button.AddChild(SlotTexts[slotIndex]);
            button.AddChild(SlotMetaTexts[slotIndex]);

            var capturedIndex = slotIndex;
            button.OnClick += _ => BindSelectedToSlot(capturedIndex);
            SlotButtons[slotIndex] = button;
            panel.AddChild(button);
        }

        private static void SetActiveGroup(string groupKey)
        {
            if (string.IsNullOrEmpty(groupKey) || string.Equals(_activeGroup, groupKey, StringComparison.Ordinal)) return;
            _activeGroup = groupKey;
            var entityId = _entityId;
            if (!string.IsNullOrEmpty(entityId))
                Open(entityId);
        }

        private static List<SkillDefinition> GetDefinitions()
        {
            return GetDefinitionsForGroup(_activeGroup);
        }

        private static List<SkillDefinition> GetAllDefinitions()
        {
            var list = new List<SkillDefinition>();
            if (!SkillService.HasInstance) return list;

            foreach (var def in SkillService.Instance.GetAllDefinitions())
            {
                if (def != null && !string.IsNullOrEmpty(def.Id))
                    list.Add(def);
            }

            list.Sort((a, b) => string.Compare(GetSkillTitle(a), GetSkillTitle(b), StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static List<SkillDefinition> GetDefinitionsForGroup(string groupKey)
        {
            var all = GetAllDefinitions();
            if (string.Equals(groupKey, GroupAll, StringComparison.Ordinal)) return all;

            var filtered = new List<SkillDefinition>();
            for (var i = 0; i < all.Count; i++)
            {
                if (MatchesGroup(all[i], groupKey))
                    filtered.Add(all[i]);
            }
            return filtered;
        }

        private static int CountDefinitionsForGroup(string groupKey)
        {
            return GetDefinitionsForGroup(groupKey).Count;
        }

        private static bool MatchesGroup(SkillDefinition def, string groupKey)
        {
            if (def == null) return false;
            if (string.Equals(groupKey, GroupAll, StringComparison.Ordinal)) return true;
            if (string.Equals(groupKey, GroupChangedIcons, StringComparison.Ordinal))
                return !string.IsNullOrEmpty(def.IconPath) &&
                       !string.Equals(GetSkillCategory(def), "Unfinished", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(GetSkillStatus(def), "Complete", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(GetSkillStatus(def), "Unfinished", StringComparison.OrdinalIgnoreCase) &&
                       def.IconPath.IndexOf(ChangedIconPathMarker, StringComparison.OrdinalIgnoreCase) >= 0;
            if (string.Equals(groupKey, GroupOther, StringComparison.Ordinal))
                return !IsInNamedMajorGroup(def);

            var tab = FindCategoryTab(groupKey);
            if (tab == null || tab.CategoryKeys == null || tab.CategoryKeys.Length == 0) return false;

            for (var i = 0; i < tab.CategoryKeys.Length; i++)
            {
                var key = tab.CategoryKeys[i];
                if (string.Equals(key, GroupOther, StringComparison.Ordinal))
                {
                    if (!IsInNamedMajorGroup(def)) return true;
                    continue;
                }
                if (string.Equals(GetSkillCategory(def), key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsInNamedMajorGroup(SkillDefinition def)
        {
            for (var i = 0; i < CategoryTabs.Length; i++)
            {
                var tab = CategoryTabs[i];
                if (tab == null ||
                    string.Equals(tab.Key, GroupAll, StringComparison.Ordinal) ||
                    string.Equals(tab.Key, GroupChangedIcons, StringComparison.Ordinal) ||
                    string.Equals(tab.Key, "Unfinished", StringComparison.Ordinal) ||
                    string.Equals(tab.Key, GroupOther, StringComparison.Ordinal))
                    continue;

                if (tab.CategoryKeys == null) continue;
                for (var j = 0; j < tab.CategoryKeys.Length; j++)
                {
                    var key = tab.CategoryKeys[j];
                    if (string.Equals(key, GroupOther, StringComparison.Ordinal)) continue;
                    if (string.Equals(GetSkillCategory(def), key, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            return false;
        }

        private static SkillCategoryTab FindCategoryTab(string key)
        {
            for (var i = 0; i < CategoryTabs.Length; i++)
            {
                if (string.Equals(CategoryTabs[i].Key, key, StringComparison.Ordinal))
                    return CategoryTabs[i];
            }
            return null;
        }

        private static string GetActiveGroupLabel()
        {
            var tab = FindCategoryTab(_activeGroup);
            return tab != null ? tab.Label : "全部技能";
        }

        private static void SelectSkill(SkillDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _selectedSkillId = def.Id;
            if (_selectedText != null)
                _selectedText.SetText($"当前选择：{Shorten(GetSkillTitle(def), 18)}");
            ShowStatus("点击右侧栏位绑定，或直接拖动技能到栏位覆盖。");
            UpdateSkillButtonColors();
        }

        private static void BindSelectedToSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            if (string.IsNullOrEmpty(_entityId) || !SkillService.HasInstance) return;
            if (string.IsNullOrEmpty(_selectedSkillId))
            {
                ShowStatus("请先选择一个技能。", true);
                return;
            }
            BindSkillToSlot(_selectedSkillId, slotIndex);
        }

        private static void BindSkillToSlot(string skillId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            if (string.IsNullOrEmpty(_entityId) || !SkillService.HasInstance) return;
            if (string.IsNullOrEmpty(skillId)) return;
            var service = SkillService.Instance;
            var slots = service.GetSlots(_entityId);
            if (slots == null || slots.Length != SlotCount)
                service.InitSlots(_entityId, SlotCount);

            var learned = service.LearnSkill(_entityId, skillId);
            if (learned == null || !service.BindSlot(_entityId, slotIndex, skillId))
            {
                ShowStatus($"绑定失败：{skillId}", true);
                return;
            }

            _selectedSkillId = skillId;
            var name = GetSkillTitle(learned.Definition ?? service.GetDefinition(skillId));
            if (_selectedText != null)
                _selectedText.SetText($"当前选择：{Shorten(name, 18)}");
            ShowStatus($"已将「{name}」绑定到槽位 {slotIndex + 1}。");
            UpdateSkillButtonColors();
            UpdateSlotButtons();
        }

        private static void UpdateSkillButtonColors()
        {
            foreach (var pair in SkillButtons)
                pair.Value.SetButtonColor(pair.Key == _selectedSkillId ? RowSelectedBg : RowBg);
        }

        private static void UpdateSlotButtons()
        {
            if (!SkillService.HasInstance || string.IsNullOrEmpty(_entityId)) return;
            var slots = SkillService.Instance.GetSlots(_entityId);

            for (var i = 0; i < SlotCount; i++)
            {
                var inst = slots != null && i < slots.Length ? slots[i].Skill : null;
                var hasSkill = inst?.Definition != null;
                if (SlotButtons[i] != null)
                    SlotButtons[i].SetButtonColor(hasSkill ? SlotBoundBg : SlotBg);

                if (!hasSkill)
                {
                    SlotTexts[i]?.SetText("空");
                    SlotMetaTexts[i]?.SetText("拖到这里覆盖");
                    if (SlotIcons[i] != null)
                    {
                        SlotIcons[i]
                            .SetBackgroundColor(new Color(1f, 1f, 1f, 0f))
                            .SetBackgroundSpriteId(DefaultSkillIconSpriteId);
                        ApplyPanelSprite(SlotIcons[i].Id, DefaultSkillIconSpriteId, new Color(1f, 1f, 1f, 0f));
                    }
                    continue;
                }

                var def = inst.Definition;
                var icon = GetSkillIcon(def);
                SlotTexts[i]?.SetText(Shorten(GetSkillTitle(def), 12));
                SlotMetaTexts[i]?.SetText(GetSkillMeta(def));
                if (SlotIcons[i] != null)
                {
                    SlotIcons[i].SetBackgroundColor(Color.white).SetBackgroundSpriteId(icon);
                    ApplyPanelSprite(SlotIcons[i].Id, icon, Color.white);
                }
            }
        }

        private static void ApplySkillRowIcons()
        {
            foreach (var pair in SkillIconPanelIds)
            {
                if (!SkillIconSpriteIds.TryGetValue(pair.Key, out var spriteId)) continue;
                ApplyPanelSprite(pair.Value, spriteId, Color.white);
            }
        }

        private static void ApplyPanelSprite(string panelId, string spriteId, Color tint)
        {
            if (string.IsNullOrEmpty(panelId) || string.IsNullOrEmpty(spriteId) || !EventProcessor.HasInstance) return;

            var goResult = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { panelId });
            if (!ResultCode.IsOk(goResult) || goResult.Count < 2 || goResult[1] is not GameObject go) return;

            var image = go.GetComponent<Image>();
            if (image == null) return;
            image.color = tint;
            image.preserveAspect = true;

            var spriteResult = EventProcessor.Instance.TriggerEventMethod(
                "GetSprite", new List<object> { spriteId });
            if (!ResultCode.IsOk(spriteResult) && TryGetSpriteName(spriteId, out var spriteName))
            {
                spriteResult = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite", new List<object> { spriteName });
            }

            if (!ResultCode.IsOk(spriteResult) || spriteResult.Count < 2 || spriteResult[1] is not Sprite sprite)
            {
                return;
            }

            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = true;
        }

        private static bool TryGetSpriteName(string spriteId, out string spriteName)
        {
            spriteName = null;
            if (string.IsNullOrEmpty(spriteId)) return false;

            var slash = spriteId.LastIndexOf('/');
            spriteName = slash >= 0 && slash + 1 < spriteId.Length ? spriteId[(slash + 1)..] : spriteId;

            var dot = spriteName.LastIndexOf('.');
            if (dot > 0) spriteName = spriteName[..dot];
            return !string.IsNullOrEmpty(spriteName) && spriteName != spriteId;
        }

        private static void ShowStatus(string message, bool isError = false)
        {
            if (_statusText == null) return;
            _statusText.SetText(message ?? string.Empty)
                .SetColor(isError ? TextWarn : TextGood);
        }

        private static UITextComponent BuildText(string id, string name, string text,
            float centerX, float centerY, float width, float height,
            int fontSize, Color color, TextAnchor align)
        {
            return new UITextComponent(id, name, text)
                .SetPosition(centerX, centerY)
                .SetSize(width * TextSupersample, height * TextSupersample)
                .SetScale(1f / TextSupersample, 1f / TextSupersample)
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(fontSize * TextSupersample)))
                .SetColor(color)
                .SetAlignment(align)
                .SetVisible(true);
        }

        private static string GetSkillTitle(SkillDefinition def)
        {
            if (def == null) return "未知技能";
            return !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : def.Id;
        }

        private static string GetSkillMeta(SkillDefinition def)
        {
            if (def == null) return string.Empty;
            return $"魔力 {Mathf.CeilToInt(def.ManaCost)}   冷却 {def.Cooldown:0.#}秒";
        }

        private static string GetSkillDescription(SkillDefinition def)
        {
            if (def == null) return string.Empty;
            if (!string.IsNullOrEmpty(def.Description)) return def.Description;
            return "暂无描述。";
        }

        private static string GetSkillCategory(SkillDefinition def)
        {
            return def != null && !string.IsNullOrEmpty(def.Category) ? def.Category : string.Empty;
        }

        private static string GetSkillStatus(SkillDefinition def)
        {
            return def != null && !string.IsNullOrEmpty(def.SkillStatus) ? def.SkillStatus : string.Empty;
        }

        private static string GetSkillIcon(SkillDefinition def)
        {
            return def != null && !string.IsNullOrEmpty(def.IconPath)
                ? def.IconPath
                : DefaultSkillIconSpriteId;
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
            return value.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }

        private static string SafeId(string value)
        {
            if (string.IsNullOrEmpty(value)) return "empty";
            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static void InstallDragHandlers()
        {
            if (!EventProcessor.HasInstance) return;
            foreach (var pair in SkillButtons)
            {
                var go = GetUiGameObject(pair.Value.Id);
                if (go == null) continue;
                var drag = go.GetComponent<SkillDragHandler>() ?? go.AddComponent<SkillDragHandler>();
                drag.Initialize(pair.Key);
            }

            for (var i = 0; i < SlotButtons.Length; i++)
            {
                var button = SlotButtons[i];
                if (button == null) continue;
                var go = GetUiGameObject(button.Id);
                if (go == null) continue;
                var drop = go.GetComponent<SkillSlotDropTarget>() ?? go.AddComponent<SkillSlotDropTarget>();
                drop.Initialize(i);
            }
        }

        private static GameObject GetUiGameObject(string id)
        {
            if (string.IsNullOrEmpty(id) || !EventProcessor.HasInstance) return null;
            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject", new List<object> { id });
            return ResultCode.IsOk(result) && result.Count >= 2 ? result[1] as GameObject : null;
        }

        private static bool TryFindDropSlot(PointerEventData eventData, out int slotIndex)
        {
            slotIndex = -1;
            for (var i = 0; i < SlotButtons.Length; i++)
            {
                var go = SlotButtons[i] != null ? GetUiGameObject(SlotButtons[i].Id) : null;
                var rect = go != null ? go.transform as RectTransform : null;
                if (rect == null) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, eventData.position, eventData.pressEventCamera)) continue;
                slotIndex = i;
                return true;
            }
            return false;
        }

        private sealed class SkillCategoryTab
        {
            public readonly string Key;
            public readonly string Label;
            public readonly string[] CategoryKeys;

            public SkillCategoryTab(string key, string label, params string[] categoryKeys)
            {
                Key = key;
                Label = label;
                CategoryKeys = categoryKeys ?? Array.Empty<string>();
            }
        }

        private sealed class SkillDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            private string _skillId;
            private GameObject _ghost;
            private RectTransform _ghostRect;

            public void Initialize(string skillId)
            {
                _skillId = skillId;
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (string.IsNullOrEmpty(_skillId)) return;
                var def = SkillService.HasInstance ? SkillService.Instance.GetDefinition(_skillId) : null;
                SelectSkill(def);
                CreateGhost(GetSkillTitle(def), eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_ghostRect != null)
                    _ghostRect.position = eventData.position;
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_ghost != null) Destroy(_ghost);
                _ghost = null;
                _ghostRect = null;
                if (TryFindDropSlot(eventData, out var slotIndex))
                    BindSkillToSlot(_skillId, slotIndex);
            }

            private void CreateGhost(string title, PointerEventData eventData)
            {
                var root = GetUiGameObject(PanelDaoId);
                var canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
                if (canvas == null) return;

                _ghost = new GameObject("SkillDragGhost");
                _ghost.transform.SetParent(canvas.transform, false);
                _ghostRect = _ghost.AddComponent<RectTransform>();
                _ghostRect.sizeDelta = new Vector2(190f, 42f);
                _ghostRect.position = eventData.position;

                var image = _ghost.AddComponent<Image>();
                image.color = new Color(0.13f, 0.20f, 0.26f, 0.88f);
                image.raycastTarget = false;

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(_ghost.transform, false);
                var tr = textGo.AddComponent<RectTransform>();
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(10f, 0f);
                tr.offsetMax = new Vector2(-10f, 0f);
                var text = textGo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.text = Shorten(title, 14);
                text.fontSize = 15;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
            }
        }

        private sealed class SkillSlotDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
        {
            private Image _image;
            private Color _normalColor;

            public void Initialize(int slotIndex)
            {
                _image = GetComponent<Image>();
                if (_image != null) _normalColor = _image.color;
            }

            public void OnDrop(PointerEventData eventData) { }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (_image != null)
                    _image.color = new Color(0.18f, 0.34f, 0.28f, 1f);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (_image != null)
                    _image.color = _normalColor;
            }
        }
    }
}
