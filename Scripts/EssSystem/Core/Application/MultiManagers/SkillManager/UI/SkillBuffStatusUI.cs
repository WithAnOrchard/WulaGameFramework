using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.UI
{
    internal static class SkillBuffStatusUI
    {
        private const string DefaultIcon = "Common/UI/Inventory/backpack_tab_skills";
        private const string FrameSprite = "Head";
        private const float BuffStartX = 24f;
        private const float BuffStartY = -138f;
        private const float FrameSize = 58f;
        private const float IconSize = 34f;
        private const float IconGap = 8f;
        private const float TooltipGapY = 10f;
        private const float TooltipWidth = 372f;
        private const float TooltipHeight = 172f;
        private const float TextSupersample = 6f;

        private static readonly Dictionary<string, BuffIconEntry> Active = new();

        public static void ShowForPlayer(string entityId, string buffId, string iconPath,
            string title, string description, float duration)
        {
            if (string.IsNullOrEmpty(entityId) || string.IsNullOrEmpty(buffId) || !EventProcessor.HasInstance) return;
            if (!HasPlayerHud(entityId)) return;

            var key = MakeKey(entityId, buffId);
            var safeKey = SafeId(key);
            var rootId = $"SkillBuffStatus_{safeKey}";
            var tooltipId = $"{rootId}_Tooltip";
            var iconId = $"{rootId}_Icon";
            var titleId = $"{tooltipId}_Title";
            var descId = $"{tooltipId}_Desc";
            Hide(entityId, buffId);
            CleanupExistingRoot(rootId);
            CleanupExistingRoot(tooltipId);

            var slotIndex = Active.Count;
            var resolvedTitle = string.IsNullOrEmpty(title) ? buffId : title;
            var resolvedDescription = string.IsNullOrEmpty(description)
                ? $"{duration:0.#}s buff"
                : description;

            var root = new UIButtonComponent(rootId, "SkillBuffStatus", string.Empty)
                .SetPosition(BuffStartX + FrameSize * 0.5f + slotIndex * (FrameSize + IconGap),
                    BuffStartY - FrameSize * 0.5f)
                .SetSize(FrameSize, FrameSize)
                .SetButtonSpriteId(FrameSprite)
                .SetButtonColor(Color.white)
                .SetVisible(true);

            var icon = new UIPanelComponent(iconId, "BuffIcon")
                .SetPosition(FrameSize * 0.5f, FrameSize * 0.5f)
                .SetSize(IconSize, IconSize)
                .SetBackgroundSpriteId(string.IsNullOrEmpty(iconPath) ? DefaultIcon : iconPath)
                .SetBackgroundColor(Color.white)
                .SetVisible(true);

            var tooltip = new UIPanelComponent(tooltipId, "BuffTooltip")
                .SetPosition(BuffStartX + TooltipWidth * 0.5f,
                    BuffStartY - FrameSize - TooltipGapY - TooltipHeight * 0.5f)
                .SetSize(TooltipWidth, TooltipHeight)
                .SetBackgroundColor(new Color(0.025f, 0.018f, 0.026f, 0.98f))
                .SetInteractable(false)
                .SetVisible(false);

            AddTooltipFrame(tooltip, tooltipId);
            tooltip.AddChild(new UIPanelComponent($"{tooltipId}_Line", "Accent")
                .SetPosition(15f, TooltipHeight * 0.5f)
                .SetSize(6f, TooltipHeight - 24f)
                .SetBackgroundColor(new Color(0.88f, 0.12f, 0.24f, 1f))
                .SetVisible(true));
            tooltip.AddChild(BuildText(titleId, "Title", WhiteText(resolvedTitle),
                204f, 143f, 316f, 38f, 28, Color.white, TextAnchor.MiddleLeft));
            tooltip.AddChild(BuildText(descId, "Description", WhiteText(WrapDescription(resolvedDescription)),
                204f, 70f, 316f, 104f, 22, Color.white, TextAnchor.UpperLeft));

            root.OnClick += _ => ShowTooltip(tooltip, tooltipId);
            root.OnHoverEnter += _ => ShowTooltip(tooltip, tooltipId);
            root.OnHoverExit += _ => HideTooltip(tooltip, tooltipId);

            root.AddChild(icon);

            if (!ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(
                    "RegisterUIEntity", new List<object> { rootId, root })))
            {
                Debug.LogWarning($"[SkillBuffStatusUI] register root failed rootId={rootId}");
                return;
            }
            Debug.Log($"[SkillBuffStatusUI] root registered rootId={rootId}, entityId={entityId}, buffId={buffId}, slot={slotIndex}");

            if (!ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(
                    "RegisterUIEntity", new List<object> { tooltipId, tooltip })))
            {
                Debug.LogWarning($"[SkillBuffStatusUI] register tooltip failed tooltipId={tooltipId}");
                EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", new List<object> { rootId });
                return;
            }
            Debug.Log($"[SkillBuffStatusUI] tooltip registered tooltipId={tooltipId}");

            Active[key] = new BuffIconEntry(rootId, tooltipId);
            ApplyIconSprite(iconId, iconPath);
            AnchorTopLeft(rootId, BuffStartX + slotIndex * (FrameSize + IconGap), BuffStartY);
            AnchorTopLeft(tooltipId, BuffStartX, BuffStartY - FrameSize - TooltipGapY);
            SetLastSibling(titleId);
            SetLastSibling(descId);
            ForceTextRender(titleId, Color.white, 28, 316f, 38f);
            ForceTextRender(descId, Color.white, 22, 316f, 104f);
            AttachHoverHandler(rootId, () => ShowTooltip(tooltip, tooltipId), () => HideTooltip(tooltip, tooltipId));
            SetLastSibling(rootId);
        }

        public static void Hide(string entityId, string buffId)
        {
            var key = MakeKey(entityId, buffId);
            if (!Active.TryGetValue(key, out var entry)) return;

            Active.Remove(key);
            if (EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", new List<object> { entry.RootId });
                EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", new List<object> { entry.TooltipId });
            }
        }

        private static bool HasPlayerHud(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !EventProcessor.HasInstance) return false;
            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject",
                new List<object> { $"{entityId}_SkillBar" });
            if (ResultCode.IsOk(result)) return true;
            return entityId.ToLowerInvariant().Contains("player");
        }

        private static void CleanupExistingRoot(string rootId)
        {
            if (string.IsNullOrEmpty(rootId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", new List<object> { rootId });
        }

        private static void ShowTooltip(UIPanelComponent tooltip, string tooltipId)
        {
            Debug.Log($"[SkillBuffStatusUI] show tooltip tooltipId={tooltipId}");
            tooltip?.SetVisible(true);
            ForcePanelScale(tooltipId);
            ForceTextRender($"{tooltipId}_Title", Color.white, 28, 316f, 38f);
            ForceTextRender($"{tooltipId}_Desc", Color.white, 22, 316f, 104f);
            SetLastSibling(tooltipId);
            SetLastSibling($"{tooltipId}_Title");
            SetLastSibling($"{tooltipId}_Desc");
        }

        private static void HideTooltip(UIPanelComponent tooltip, string tooltipId)
        {
            Debug.Log($"[SkillBuffStatusUI] hide tooltip tooltipId={tooltipId}");
            tooltip?.SetVisible(false);
        }

        private static void AnchorTopLeft(string rootId, float x, float y)
        {
            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject", new List<object> { rootId });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject go) return;
            if (!go.TryGetComponent<RectTransform>(out var rect)) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void AttachHoverHandler(string rootId, System.Action enter, System.Action exit)
        {
            if (string.IsNullOrEmpty(rootId) || !EventProcessor.HasInstance) return;
            var result = EventProcessor.Instance.TriggerEventMethod("AttachUIHoverHandler",
                new List<object> { rootId, enter, exit, enter });
            Debug.Log($"[SkillBuffStatusUI] attach hover rootId={rootId}, ok={ResultCode.IsOk(result)}");
        }

        private static void AddTooltipFrame(UIPanelComponent tooltip, string tooltipId)
        {
            var border = new Color(0.28f, 0.30f, 0.46f, 1f);
            var inner = new Color(0.09f, 0.08f, 0.12f, 1f);

            tooltip.AddChild(new UIPanelComponent($"{tooltipId}_TopBorder", "TopBorder")
                .SetPosition(TooltipWidth * 0.5f, TooltipHeight - 2f)
                .SetSize(TooltipWidth, 4f)
                .SetBackgroundColor(border)
                .SetVisible(true));
            tooltip.AddChild(new UIPanelComponent($"{tooltipId}_BottomBorder", "BottomBorder")
                .SetPosition(TooltipWidth * 0.5f, 2f)
                .SetSize(TooltipWidth, 4f)
                .SetBackgroundColor(border)
                .SetVisible(true));
            tooltip.AddChild(new UIPanelComponent($"{tooltipId}_LeftBorder", "LeftBorder")
                .SetPosition(2f, TooltipHeight * 0.5f)
                .SetSize(4f, TooltipHeight)
                .SetBackgroundColor(border)
                .SetVisible(true));
            tooltip.AddChild(new UIPanelComponent($"{tooltipId}_RightBorder", "RightBorder")
                .SetPosition(TooltipWidth - 2f, TooltipHeight * 0.5f)
                .SetSize(4f, TooltipHeight)
                .SetBackgroundColor(border)
                .SetVisible(true));
            tooltip.AddChild(new UIPanelComponent($"{tooltipId}_TitleBand", "TitleBand")
                .SetPosition(TooltipWidth * 0.5f, TooltipHeight - 32f)
                .SetSize(TooltipWidth - 16f, 48f)
                .SetBackgroundColor(inner)
                .SetVisible(true));
        }

        private static void ForcePanelScale(string panelId)
        {
            if (string.IsNullOrEmpty(panelId) || !EventProcessor.HasInstance) return;
            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject", new List<object> { panelId });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject go) return;
            if (go.transform is RectTransform rect)
            {
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(TooltipWidth, TooltipHeight);
                Debug.Log($"[SkillBuffStatusUI] tooltip scale panelId={panelId}, local={rect.localScale}, lossy={rect.lossyScale}, rect={rect.sizeDelta}, parents={BuildScaleChain(rect)}");
            }
        }

        private static void ForceTextRender(string textId, Color color, int visualFontSize, float width, float height)
        {
            if (string.IsNullOrEmpty(textId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod("UIDaoPropertyChanged",
                new List<object> { textId, "Color", color });

            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject", new List<object> { textId });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject go) return;
            if (go.transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width * TextSupersample, height * TextSupersample);
                rect.localScale = Vector3.one * (1f / TextSupersample);
            }

            var text = go.GetComponent<Text>();
            if (text == null) return;
            text.color = color;
            text.material.color = color;
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(visualFontSize * TextSupersample));
            text.supportRichText = true;
            text.resizeTextForBestFit = false;
            text.alignByGeometry = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            go.transform.SetAsLastSibling();
            Debug.Log($"[SkillBuffStatusUI] text render textId={textId}, visualFont={visualFontSize}, actualFont={text.fontSize}, local={go.transform.localScale}, lossy={go.transform.lossyScale}, rect={(go.transform as RectTransform)?.sizeDelta}, parents={BuildScaleChain(go.transform)}");
        }

        private static string BuildScaleChain(Transform transform)
        {
            if (transform == null) return string.Empty;
            var parts = new List<string>();
            var current = transform.parent;
            while (current != null && parts.Count < 8)
            {
                parts.Add($"{current.name}:local={current.localScale},lossy={current.lossyScale}");
                current = current.parent;
            }
            return string.Join(" <- ", parts);
        }

        private static UITextComponent BuildText(string id, string name, string text,
            float x, float y, float width, float height, int fontSize, Color color, TextAnchor align)
        {
            return new UITextComponent(id, name, text)
                .SetPosition(x, y)
                .SetSize(width * TextSupersample, height * TextSupersample)
                .SetScale(1f / TextSupersample, 1f / TextSupersample)
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(fontSize * TextSupersample)))
                .SetColor(color)
                .SetAlignment(align)
                .SetVisible(true);
        }

        private static string WhiteText(string text)
        {
            return $"<color=#FFFFFFFF>{text ?? string.Empty}</color>";
        }

        private static void ApplyIconSprite(string iconId, string spriteId)
        {
            if (string.IsNullOrEmpty(iconId) || string.IsNullOrEmpty(spriteId) || !EventProcessor.HasInstance) return;
            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject", new List<object> { iconId });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject go) return;
            var image = go.GetComponent<Image>();
            if (image == null) return;
            image.preserveAspect = true;
        }

        private static void SetLastSibling(string rootId)
        {
            var result = EventProcessor.Instance.TriggerEventMethod("GetUIGameObject", new List<object> { rootId });
            if (ResultCode.IsOk(result) && result.Count > 1 && result[1] is GameObject go)
                go.transform.SetAsLastSibling();
        }

        private static string WrapDescription(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= 24) return text ?? string.Empty;
            if (text.Length <= 48) return $"{text.Substring(0, 24)}\n{text.Substring(24)}";
            return text.Length <= 72
                ? $"{text.Substring(0, 24)}\n{text.Substring(24, 24)}\n{text.Substring(48)}"
                : $"{text.Substring(0, 24)}\n{text.Substring(24, 24)}\n{text.Substring(48, 21)}...";
        }

        private static string MakeKey(string entityId, string buffId) => $"{entityId}:{buffId}";

        private static string SafeId(string value)
        {
            var chars = (value ?? string.Empty).ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }
            return chars.Length == 0 ? "empty" : new string(chars);
        }

        private readonly struct BuffIconEntry
        {
            public readonly string RootId;
            public readonly string TooltipId;
            public BuffIconEntry(string rootId, string tooltipId)
            {
                RootId = rootId;
                TooltipId = tooltipId;
            }
        }

    }
}
