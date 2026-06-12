using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao;
using EssSystem.Core.Presentation.InputManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine.UI;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.UI
{
    /// <summary>
    /// 通用技能栏 UI：显示固定 4 个技能槽位，并通过 InputManager 的 SkillUse1-4 触发释放。
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillBarUI : MonoBehaviour
    {
        private const int SlotCount = 4;
        private const string RootName = "SkillBarUI";
        private const string DefaultSkillIconSpriteId = "Common/UI/Inventory/backpack_tab_skills";
        private const float RootWidth = 116f;
        private const float RootHeight = 520f;
        private const float RootLeftX = 96f;
        private const float RootCenterYOffset = -84f;
        private const float SlotSize = 100f;
        private const float SlotRootWidth = 108f;
        private const float SlotRootHeight = 124f;
        private const float SlotGap = 8f;
        private const float TextSupersample = 4f;

        private static readonly string[] KeyLabels = { "J", "K", "L", ";" };
        private static readonly Color RootClear = new(0f, 0f, 0f, 0f);
        private static readonly Color SlotBg = new(0.35f, 0.35f, 0.34f, 0.62f);
        private static readonly Color EmptyIconTint = new(0f, 0f, 0f, 0f);
        private static readonly Color ReadyOverlay = new(0f, 0f, 0f, 0f);
        private static readonly Color CooldownOverlay = new(0f, 0f, 0f, 0.62f);
        private static readonly Color EmptyOverlay = new(0f, 0f, 0f, 0.24f);
        private static readonly Color ReadyNameColor = new(1f, 0.92f, 0.70f, 1f);
        private static readonly Color EmptyNameColor = new(0.72f, 0.66f, 0.56f, 0.82f);
        private static readonly Color CooldownTextColor = new(1f, 0.92f, 0.82f, 1f);
        private static readonly Color KeyTextColor = new(0.88f, 0.88f, 0.82f, 1f);
        private static readonly Color ManaCostColor = new(0.65f, 0.90f, 1f, 1f);
        private static readonly Color ManaNotEnoughColor = new(1f, 0.42f, 0.42f, 1f);

        private string _entityId;
        private string _rootId;
        private Transform _casterTransform;
        private Func<Vector3> _directionProvider;
        private bool _registered;

        private readonly UIPanelComponent[] _icons = new UIPanelComponent[SlotCount];
        private readonly UIPanelComponent[] _overlays = new UIPanelComponent[SlotCount];
        private readonly UITextComponent[] _names = new UITextComponent[SlotCount];
        private readonly UITextComponent[] _cooldowns = new UITextComponent[SlotCount];
        private readonly UITextComponent[] _costs = new UITextComponent[SlotCount];

        public bool IsBuilt => _registered;

        public bool Build(string entityId, Transform casterTransform, Func<Vector3> directionProvider = null)
        {
            if (_registered) return true;
            if (!EventProcessor.HasInstance) return false;
            if (string.IsNullOrEmpty(entityId)) return false;

            _entityId = entityId;
            _casterTransform = casterTransform != null ? casterTransform : transform;
            _directionProvider = directionProvider;
            _rootId = $"{entityId}_SkillBar";

            EnsureSlots();

            var root = new UIPanelComponent(_rootId, RootName)
                .SetPosition(RootLeftX, 540f + RootCenterYOffset).SetSize(RootWidth, RootHeight)
                .SetBackgroundColor(RootClear).SetVisible(true);

            var totalSlotsH = SlotRootHeight * SlotCount + SlotGap * (SlotCount - 1);
            var startY = RootHeight - ((RootHeight - totalSlotsH) * 0.5f + SlotRootHeight * 0.5f);

            for (var i = 0; i < SlotCount; i++)
            {
                var y = startY - i * (SlotRootHeight + SlotGap);
                var slot = BuildSlot(i, RootWidth * 0.5f, y, SlotSize);
                root.AddChild(slot);
            }

            if (!ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(
                    "RegisterUIEntity", new List<object> { _rootId, root })))
            {
                Debug.LogWarning("[SkillBarUI] 注册技能栏 UI 失败");
                return false;
            }

            _registered = true;
            AnchorBottomCenter();
            Refresh();
            return true;
        }

        public void Tick()
        {
            if (!_registered) return;
            HandleInput();
            Refresh();
        }

        public void Dispose()
        {
            if (!_registered) return;
            _registered = false;
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", new List<object> { _rootId });
        }

        private UIPanelComponent BuildSlot(int index, float x, float y, float size)
        {
            var root = new UIPanelComponent($"{_rootId}_slot_{index}", $"SkillSlot_{index + 1}")
                .SetPosition(x, y).SetSize(SlotRootWidth, SlotRootHeight)
                .SetBackgroundColor(RootClear).SetVisible(true);

            var frame = new UIPanelComponent($"{_rootId}_slot_{index}_frame", "Frame")
                .SetPosition(SlotRootWidth * 0.5f, 70f).SetSize(size, size)
                .SetBackgroundSpriteId(string.Empty).SetBackgroundColor(SlotBg).SetVisible(true);

            _icons[index] = new UIPanelComponent($"{_rootId}_slot_{index}_icon", "Icon")
                .SetPosition(SlotRootWidth * 0.5f, 70f).SetSize(72f, 72f)
                .SetBackgroundSpriteId(DefaultSkillIconSpriteId).SetBackgroundColor(EmptyIconTint).SetVisible(true);

            _overlays[index] = new UIPanelComponent($"{_rootId}_slot_{index}_cd_overlay", "CooldownOverlay")
                .SetPosition(SlotRootWidth * 0.5f, 70f).SetSize(88f, 88f)
                .SetBackgroundColor(EmptyOverlay).SetVisible(true);

            _cooldowns[index] = new UITextComponent($"{_rootId}_slot_{index}_cd_text", "CooldownText")
                .SetPosition(SlotRootWidth * 0.5f, 70f).SetSize(90f, 90f)
                .SetFontSize(28).SetColor(CooldownTextColor)
                .SetAlignment(TextAnchor.MiddleCenter).SetText(string.Empty).SetVisible(true);

            var key = MakeCrispText($"{_rootId}_slot_{index}_key", "KeyText", KeyLabels[index],
                22f, 88f, 34f, 22f, 16, KeyTextColor, TextAnchor.MiddleCenter);

            _costs[index] = MakeCrispText($"{_rootId}_slot_{index}_cost", "ManaCost", string.Empty,
                82f, 12f, 40f, 20f, 13, ManaCostColor, TextAnchor.MiddleRight);

            _names[index] = MakeCrispText($"{_rootId}_slot_{index}_name", "SkillName", string.Empty,
                32f, 12f, 56f, 20f, 12, EmptyNameColor, TextAnchor.MiddleLeft);

            root.AddChild(frame);
            root.AddChild(_icons[index]);
            root.AddChild(_overlays[index]);
            root.AddChild(_cooldowns[index]);
            root.AddChild(_costs[index]);
            root.AddChild(_names[index]);
            root.AddChild(key);
            return root;
        }

        private static UITextComponent MakeCrispText(string id, string name, string text,
            float x, float y, float width, float height, int fontSize, Color color, TextAnchor alignment)
        {
            return new UITextComponent(id, name, text)
                .SetPosition(x, y)
                .SetSize(width * TextSupersample, height * TextSupersample)
                .SetScale(1f / TextSupersample, 1f / TextSupersample)
                .SetFontSize(Mathf.RoundToInt(fontSize * TextSupersample))
                .SetColor(color)
                .SetAlignment(alignment)
                .SetVisible(true);
        }

        private void EnsureSlots()
        {
            if (string.IsNullOrEmpty(_entityId)) return;
            var slots = SkillService.Instance.GetSlots(_entityId);
            if (slots == null || slots.Length != SlotCount)
                SkillService.Instance.InitSlots(_entityId, SlotCount);
        }

        private void HandleInput()
        {
            var input = InputManager.TryGetInstance();
            if (input == null) return;

            for (var i = 0; i < SlotCount; i++)
            {
                if (!input.IsDown(InputManager.GetSkillUseActionName(i))) continue;
                CastSlot(i);
            }
        }

        private void CastSlot(int slotIndex)
        {
            var inst = GetSlotSkill(slotIndex);
            if (inst?.Definition == null || string.IsNullOrEmpty(inst.SkillId)) return;
            if (!EventProcessor.HasInstance) return;

            var casterPosition = _casterTransform != null ? _casterTransform.position : transform.position;
            EventProcessor.Instance.TriggerEventMethod(
                SkillManager.EVT_CAST_SKILL,
                new List<object> { _entityId, inst.SkillId, null, GetCastDirection(), casterPosition });
        }

        private void Refresh()
        {
            for (var i = 0; i < SlotCount; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            var inst = GetSlotSkill(index);
            if (inst?.Definition == null)
            {
                _icons[index].SetBackgroundSpriteId(DefaultSkillIconSpriteId).SetBackgroundColor(EmptyIconTint);
                _overlays[index].SetBackgroundColor(EmptyOverlay);
                _cooldowns[index].SetText(string.Empty);
                _costs[index].SetText(string.Empty).SetVisible(true);
                _names[index].SetText(string.Empty).SetColor(EmptyNameColor).SetVisible(true);
                return;
            }

            var def = inst.Definition;
            var icon = string.IsNullOrEmpty(def.IconPath) ? DefaultSkillIconSpriteId : def.IconPath;
            _icons[index].SetBackgroundSpriteId(icon).SetBackgroundColor(Color.white).SetVisible(true);
            ApplyIconSprite(index, icon);

            var cooldown = Mathf.Max(0f, inst.CooldownRemaining);
            _overlays[index].SetBackgroundColor(cooldown > 0f ? CooldownOverlay : ReadyOverlay);
            _cooldowns[index].SetText(cooldown > 0f ? Mathf.CeilToInt(cooldown).ToString() : string.Empty);

            var name = string.IsNullOrEmpty(def.DisplayName) ? inst.SkillId : def.DisplayName;
            _names[index].SetText(TrimName(name)).SetColor(inst.IsReady ? ReadyNameColor : EmptyNameColor).SetVisible(true);

            if (def.ManaCost > 0f)
            {
                var hasMana = HasEnoughMana(def.ManaCost);
                _costs[index]
                    .SetText($"MP{Mathf.CeilToInt(def.ManaCost)}")
                    .SetColor(hasMana ? ManaCostColor : ManaNotEnoughColor)
                    .SetVisible(true);
            }
            else
            {
                _costs[index].SetText(string.Empty).SetVisible(true);
            }
        }

        private void ApplyIconSprite(int index, string spriteId)
        {
            if (index < 0 || index >= SlotCount || string.IsNullOrEmpty(spriteId) || !EventProcessor.HasInstance) return;

            var goResult = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { _icons[index].Id });
            if (!ResultCode.IsOk(goResult) || goResult.Count < 2 || goResult[1] is not GameObject iconGo) return;

            var image = iconGo.GetComponent<Image>();
            if (image == null) return;

            var spriteResult = EventProcessor.Instance.TriggerEventMethod(
                "GetSprite", new List<object> { spriteId });
            if (!ResultCode.IsOk(spriteResult) && TryGetSpriteName(spriteId, out var spriteName))
            {
                spriteResult = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite", new List<object> { spriteName });
            }
            if (!ResultCode.IsOk(spriteResult) || spriteResult.Count < 2 || spriteResult[1] is not Sprite sprite) return;

            image.sprite = sprite;
            image.color = Color.white;
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

        private bool HasEnoughMana(float cost)
        {
            if (cost <= 0f) return true;
            return SkillEntityProxy.TryGetResource(_entityId, SkillEntityProxy.ResourceMana, out var current, out _) &&
                   current + 0.0001f >= cost;
        }

        private SkillInstance GetSlotSkill(int index)
        {
            if (string.IsNullOrEmpty(_entityId) || !SkillService.HasInstance) return null;
            var slots = SkillService.Instance.GetSlots(_entityId);
            return slots != null && index >= 0 && index < slots.Length ? slots[index].Skill : null;
        }

        private Vector3 GetCastDirection()
        {
            if (_directionProvider == null) return Vector3.right;
            var direction = _directionProvider.Invoke();
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        }

        private void AnchorBottomCenter()
        {
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { _rootId });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject rootGo) return;
            if (!rootGo.TryGetComponent<RectTransform>(out var rect)) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(RootLeftX, RootCenterYOffset);
        }

        private static string TrimName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= 4 ? value : value.Substring(0, 4);
        }
    }
}
