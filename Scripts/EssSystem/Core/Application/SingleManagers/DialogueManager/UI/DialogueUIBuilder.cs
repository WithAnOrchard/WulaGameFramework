using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
using EssSystem.Core.Presentation.UIManager.Theme;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.Specs;
// 不 using UIManager 模块；查 UI GameObject 走 EVT_GET_UI_GAMEOBJECT 事件。

namespace EssSystem.Core.Application.SingleManagers.DialogueManager
{
    /// <summary>
    /// 对话 UI 纯静态构建器 —— 把 <see cref="DialogueManager"/> 从布局细节中解放出来。
    /// 职责：
    /// <list type="number">
    ///   <item>按 <see cref="DialogueConfig"/> 构建 panel 树（背景/立绘/说话者/正文/Next/Close/Options）</item>
    ///   <item>把 <see cref="DialogueLine"/> 状态写到子组件（数据 → 视图）</item>
    /// </list>
    /// </summary>
    internal static class DialogueUIBuilder
    {
        /// <summary>
        /// 文本超采样倍率（参 <c>Assets/Agent.md §5「文本清晰度」</c>）。
        /// FontSize × N + Size × N + Scale × (1/N)：让字体以 N× 分辨率栅格化再缩小渲染，
        /// 显著降低 uGUI Text 在 1080p+ 屏幕上的模糊感。建议整数倍（2×/3×）。
        /// </summary>
        private const float TextSupersample = 6f;
        private const string UiSpriteOptionArrow = "Tribe/Common/Items/Weapons/arrow_gold";

        /// <summary>构建完整对话 UI 面板树。</summary>
        public static (UIPanelComponent panel, DialogueUIRefs refs)
            BuildPanelTree(string daoId, DialogueConfig config)
        {
            var pl = config.Panel;
            var t = DefaultUITheme.Instance.Current;
            var titleH = Mathf.Min(44f, pl.Size.y * 0.24f);
            var innerBorder = new Color(0.23f, 0.37f, 0.46f, 0.55f);
            var goldLine = new Color(0.88f, 0.69f, 0.35f, 0.88f);
            var shadow = new Color(0.01f, 0.012f, 0.018f, 0.88f);

            var root = pl.CreateComponent(daoId, "DialoguePanel");
            root.SetBackgroundColor(new Color(0.055f, 0.06f, 0.085f, 0.90f)).SetVisible(true);

            var titleBar = new UIPanelComponent($"{daoId}_TitleBar", "DialogueTitleBar")
                .SetSize(pl.Size.x, titleH)
                .SetPosition(pl.Size.x * 0.5f, pl.Size.y - titleH * 0.5f)
                .SetBackgroundColor(new Color(t.Header.r, t.Header.g, t.Header.b, 0.96f));
            root.AddChild(titleBar);

            titleBar.AddChild(BuildSupersampledRaw(
                $"{daoId}_Title", "DialogueTitle",
                centerX: pl.Size.x * 0.5f,
                centerY: titleH * 0.5f,
                width: pl.Size.x - 96f,
                height: titleH,
                fontSize: 16,
                color: t.TextOnHeader,
                align: TextAnchor.MiddleLeft).SetText("对话"));

            root.AddChild(new UIPanelComponent($"{daoId}_Divider", "DialogueDivider")
                .SetSize(pl.Size.x, 1f)
                .SetPosition(pl.Size.x * 0.5f, pl.Size.y - titleH - 0.5f)
                .SetBackgroundColor(new Color(t.Divider.r, t.Divider.g, t.Divider.b, 0.80f)));

            root.AddChild(new UIPanelComponent($"{daoId}_ContentBg", "DialogueContentBg")
                .SetSize(pl.Size.x - 24f, pl.Size.y - titleH - 24f)
                .SetPosition(pl.Size.x * 0.5f, (pl.Size.y - titleH) * 0.5f - 2f)
                .SetBackgroundColor(new Color(0.025f, 0.032f, 0.047f, 0.70f)));

            AddBorder(root, daoId, "ContentGoldBorder",
                centerX: pl.Size.x * 0.5f,
                centerY: (pl.Size.y - titleH) * 0.5f - 2f,
                width: pl.Size.x - 24f,
                height: pl.Size.y - titleH - 24f,
                thickness: 1f,
                color: goldLine);

            root.AddChild(new UIPanelComponent($"{daoId}_BorderTop", "DialogueBorderTop")
                .SetSize(pl.Size.x, 2f)
                .SetPosition(pl.Size.x * 0.5f, pl.Size.y - 1f)
                .SetBackgroundColor(goldLine));

            root.AddChild(new UIPanelComponent($"{daoId}_BorderBottom", "DialogueBorderBottom")
                .SetSize(pl.Size.x, 2f)
                .SetPosition(pl.Size.x * 0.5f, 1f)
                .SetBackgroundColor(goldLine));

            root.AddChild(new UIPanelComponent($"{daoId}_BorderLeft", "DialogueBorderLeft")
                .SetSize(2f, pl.Size.y)
                .SetPosition(1f, pl.Size.y * 0.5f)
                .SetBackgroundColor(goldLine));

            root.AddChild(new UIPanelComponent($"{daoId}_BorderRight", "DialogueBorderRight")
                .SetSize(2f, pl.Size.y)
                .SetPosition(pl.Size.x - 1f, pl.Size.y * 0.5f)
                .SetBackgroundColor(goldLine));

            root.AddChild(new UIPanelComponent($"{daoId}_PortraitBg", "DialoguePortraitBg")
                .SetSize(config.Portrait.Size.x + 18f, config.Portrait.Size.y + 18f)
                .SetPosition(config.Portrait.Position.x, config.Portrait.Position.y)
                .SetBackgroundColor(shadow));

            AddBorder(root, daoId, "PortraitGoldBorder",
                centerX: config.Portrait.Position.x,
                centerY: config.Portrait.Position.y,
                width: config.Portrait.Size.x + 18f,
                height: config.Portrait.Size.y + 18f,
                thickness: 1f,
                color: goldLine);

            var refs = new DialogueUIRefs { Root = root, Background = root };

            // 立绘头像框：始终显示（无 sprite 时显示框色作为视觉占位）
            if (config.Portrait != null)
            {
                refs.Portrait = config.Portrait.CreateComponent($"{daoId}_Portrait", "Portrait");
                refs.PortraitFrameColor = t.ScrollBg;
                refs.Portrait.SetVisible(true);
                root.AddChild(refs.Portrait);
            }

            // 说话者名（左下原点 → 中心原点；2× 超采样）
            refs.SpeakerText = BuildSupersampledText(
                $"{daoId}_Speaker", "Speaker", config.SpeakerText);
            root.AddChild(refs.SpeakerText);

            // 正文（同上）
            refs.BodyText = BuildSupersampledText(
                $"{daoId}_Body", "Body", config.BodyText);
            root.AddChild(refs.BodyText);

            // Next 按钮
            refs.NextButton = config.NextButton.CreateComponent($"{daoId}_NextBtn", "NextButton");
            AttachSupersampledButtonLabel(refs.NextButton, $"{daoId}_NextText", refs.NextButton.Text, 30, new Color(0.96f, 0.92f, 0.74f, 1f));
            root.AddChild(refs.NextButton);

            // Close 按钮
            refs.CloseButton = config.CloseButton.CreateComponent($"{daoId}_CloseBtn", "CloseButton");
            AttachSupersampledButtonLabel(refs.CloseButton, $"{daoId}_CloseText", refs.CloseButton.Text, 22, new Color(1f, 0.86f, 0.86f, 0.95f));
            root.AddChild(refs.CloseButton);

            // 选项按钮（默认全部隐藏，行有 options 时再显示）
            // 每个按钮 + 一个超采样 UIText 覆盖层 —— 按钮自身 Text 留空（uGUI 内置 Text 模糊），
            // 文字渲染交给同位置同尺寸的 UITextComponent，享受 ApplySupersample 的高分辨率 raster。
            var op = config.Options;
            var maxOpts = Mathf.Max(1, op.MaxOptions);
            var tmpl = op.ButtonTemplate;
            var btnH = tmpl.Size.y;
            refs.OptionButtons = new UIButtonComponent[maxOpts];
            refs.OptionTexts   = new UITextComponent[maxOpts];
            for (var i = 0; i < maxOpts; i++)
            {
                var x = op.FirstButtonOffset.x;
                var y = op.FirstButtonOffset.y - i * (btnH + op.Spacing);
                var btn = tmpl.CreateComponent($"{daoId}_Opt_{i}", $"Option_{i}");
                btn.SetText(string.Empty)            // 让内置 Text 留白，避免与覆盖层重叠
                   .SetPosition(x, y)
                   .SetVisible(false)
                   .SetInteractable(false);

                AddBorder(btn, daoId, $"Option_{i}_GoldBorder",
                    centerX: tmpl.Size.x * 0.5f,
                    centerY: btnH * 0.5f,
                    width: tmpl.Size.x,
                    height: btnH,
                    thickness: 1f,
                    color: goldLine);

                btn.AddChild(new UIPanelComponent($"{daoId}_Opt_{i}_Arrow", $"Option_{i}_Arrow")
                    .SetSize(18f, 12f)
                    .SetPosition(24f, btnH * 0.5f)
                    .SetBackgroundSpriteId(UiSpriteOptionArrow)
                    .SetBackgroundColor(Color.white));

                refs.OptionButtons[i] = btn;
                root.AddChild(btn);

                // 超采样文字覆盖层 —— 与按钮同心同尺寸（白字、居中）
                var label = BuildSupersampledRaw(
                    $"{daoId}_OptText_{i}", $"Option_{i}_Text",
                    centerX: x,
                    centerY: y,
                    width: tmpl.Size.x,
                    height: tmpl.Size.y,
                    fontSize: 20,                          // UIButtonSpec 没有 FontSize 字段，使用覆盖层控制字号
                    color: Color.white,
                    align: TextAnchor.MiddleCenter);
                label.SetVisible(false);
                refs.OptionTexts[i] = label;
                root.AddChild(label);
            }

            return (root, refs);
        }

        private static void AddBorder(
            UIComponent parent, string daoId, string name,
            float centerX, float centerY, float width, float height, float thickness, Color color)
        {
            if (parent == null) return;

            parent.AddChild(new UIPanelComponent($"{daoId}_{name}_Top", $"{name}_Top")
                .SetSize(width, thickness)
                .SetPosition(centerX, centerY + height * 0.5f - thickness * 0.5f)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{daoId}_{name}_Bottom", $"{name}_Bottom")
                .SetSize(width, thickness)
                .SetPosition(centerX, centerY - height * 0.5f + thickness * 0.5f)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{daoId}_{name}_Left", $"{name}_Left")
                .SetSize(thickness, height)
                .SetPosition(centerX - width * 0.5f + thickness * 0.5f, centerY)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{daoId}_{name}_Right", $"{name}_Right")
                .SetSize(thickness, height)
                .SetPosition(centerX + width * 0.5f - thickness * 0.5f, centerY)
                .SetBackgroundColor(color));
        }

        /// <summary>把单条对话行写入 UI 子组件。空选项时隐藏选项按钮、显示 Next；反之反之。</summary>
        public static void ApplyLine(DialogueUIRefs refs, Dialogue dialogue, DialogueLine line)
        {
            if (refs == null || line == null) return;

            // 背景：行级覆盖 → 对话级默认 → 不变（保留 config 默认）
            var bgId = !string.IsNullOrEmpty(line.BackgroundSpriteId)
                ? line.BackgroundSpriteId
                : dialogue?.DefaultBackgroundSpriteId;
            if (!string.IsNullOrEmpty(bgId))
                refs.Background.BackgroundSpriteId = bgId;

            // 文本
            if (refs.SpeakerText != null) refs.SpeakerText.Text = line.Speaker ?? string.Empty;
            if (refs.BodyText    != null) refs.BodyText.Text    = line.Text ?? string.Empty;

            // 立绘头像框：始终显示。有 sprite → 设头像并 tint=white（不染色）；无 sprite → 显原框色
            if (refs.Portrait != null)
            {
                refs.Portrait.SetVisible(true);
                var hasPortrait = !string.IsNullOrEmpty(line.PortraitSpriteId);
                refs.Portrait.BackgroundSpriteId = hasPortrait ? line.PortraitSpriteId : string.Empty;
                refs.Portrait.BackgroundColor = hasPortrait ? Color.white : refs.PortraitFrameColor;
            }

            // Next vs Options 互斥（行有选项时同时隐藏正文避免重叠）
            var hasOptions = line.HasOptions;
            if (refs.NextButton != null)
                refs.NextButton.SetVisible(!hasOptions).SetInteractable(!hasOptions);
            if (refs.BodyText != null)
                refs.BodyText.SetVisible(!hasOptions);

            if (refs.OptionButtons != null)
            {
                var optCount = hasOptions ? Mathf.Min(line.Options.Count, refs.OptionButtons.Length) : 0;
                for (var i = 0; i < refs.OptionButtons.Length; i++)
                {
                    var btn = refs.OptionButtons[i];
                    if (btn == null) continue;
                    var hasOpt = i < optCount;
                    // 按钮自身 Text 留空（避免与超采样覆盖层重叠模糊）；文字写到 OptionTexts 的覆盖层上
                    btn.Text = string.Empty;
                    btn.SetVisible(hasOpt).SetInteractable(hasOpt);

                    var label = refs.OptionTexts != null && i < refs.OptionTexts.Length ? refs.OptionTexts[i] : null;
                    if (label != null)
                    {
                        if (hasOpt) label.Text = line.Options[i].Text ?? string.Empty;
                        label.SetVisible(hasOpt);
                    }
                }
            }
        }

        private static void AttachSupersampledButtonLabel(
            UIButtonComponent button, string id, string text, int fontSize, Color color)
        {
            if (button == null || string.IsNullOrEmpty(text)) return;

            var label = BuildSupersampledRaw(
                id,
                $"{button.Name}_Text",
                centerX: button.Size.x * 0.5f,
                centerY: button.Size.y * 0.5f,
                width: button.Size.x,
                height: button.Size.y,
                fontSize: fontSize,
                color: color,
                align: TextAnchor.MiddleCenter);

            label.SetText(text ?? string.Empty);
            button.SetText(string.Empty);
            button.AddChild(label);
        }

        private static Color SpriteAwareTint(string spriteId, Color fallback) =>
            string.IsNullOrEmpty(spriteId) ? fallback : Color.white;

        /// <summary>
        /// 用 <see cref="UITextSpec"/> 构造一个应用了文本超采样的 <see cref="UITextComponent"/>。
        /// FontSize × N、Size × N、Scale × (1/N)，最终视觉大小不变但字体清晰度显著提升。
        /// 自动把 spec 中的「左下原点 + 组件中心」位置转换成 UITextEntity 的「父中心相对」坐标。
        /// 参 <c>Assets/Agent.md §5「文本清晰度」</c>。
        /// </summary>
        private static UITextComponent BuildSupersampledText(
            string id, string name, UITextSpec spec)
        {
            var n = TextSupersample;
            var inv = 1f / n;
            // 左下原点 → 父中心原点（UITextEntity 的锚点在父中心）
            var t = new UITextComponent(id, name)
                .SetPosition(spec.Position.x, spec.Position.y)                  // Position 不放大（视觉中心不变）
                .SetSize(spec.Size.x * n, spec.Size.y * n)                       // Rect 放大 N 倍
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(spec.FontSize * n)))
                .SetColor(spec.TextColor)
                .SetAlignment(spec.Alignment)
                .SetText(string.Empty)
                .SetScale(inv, inv);                                             // 整体缩小 1/N → 视觉与原始一致
            t.SetVisible(spec.Visible);
            return t;
        }

        /// <summary>构造超采样 <see cref="UITextComponent"/>（不依赖 spec）—— 给按钮文本覆盖层用。
        /// 入参 centerX/centerY 使用父面板左下原点。</summary>
        private static UITextComponent BuildSupersampledRaw(
            string id, string name,
            float centerX, float centerY, float width, float height,
            int fontSize, Color color, TextAnchor align)
        {
            var n = TextSupersample;
            var inv = 1f / n;
            var t = new UITextComponent(id, name)
                .SetPosition(centerX, centerY)
                .SetSize(width * n, height * n)
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(fontSize * n)))
                .SetColor(color)
                .SetAlignment(align)
                .SetText(string.Empty)
                .SetScale(inv, inv);
            return t;
        }
    }
}
