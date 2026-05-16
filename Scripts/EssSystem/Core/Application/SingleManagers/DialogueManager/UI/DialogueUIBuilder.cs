using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.UIConfig;
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
        private const float TextSupersample = 2f;

        /// <summary>构建完整对话 UI 面板树。</summary>
        public static (UIPanelComponent panel, DialogueUIRefs refs)
            BuildPanelTree(string daoId, DialogueConfig config)
        {
            var pl = config.Panel;

            // 根面板（也是主背景）
            var root = new UIPanelComponent(daoId, "DialoguePanel")
                .SetPosition(pl.Position.x, pl.Position.y)
                .SetSize(pl.Width, pl.Height)
                .SetScale(pl.Scale.x, pl.Scale.y)
                .SetBackgroundSpriteId(pl.BackgroundSpriteId)
                .SetBackgroundColor(SpriteAwareTint(pl.BackgroundSpriteId, pl.BackgroundColor))
                .SetVisible(true);

            var refs = new DialogueUIRefs { Root = root, Background = root };

            // 立绘
            if (config.Portrait != null && config.Portrait.Enabled)
            {
                var pp = config.Portrait;
                refs.Portrait = new UIPanelComponent($"{daoId}_Portrait", "Portrait")
                    .SetPosition(pp.Position.x, pp.Position.y)
                    .SetSize(pp.Width, pp.Height)
                    .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                    .SetVisible(false); // 行级 PortraitSpriteId 提供时再显示
                root.AddChild(refs.Portrait);
            }

            // 说话者名（UITextEntity 父中心锚点 → 需减去半面板转换为中心相对；同时 2× 超采样）
            var st = config.SpeakerText;
            refs.SpeakerText = BuildSupersampledText(
                $"{daoId}_Speaker", "Speaker",
                st.Position.x - pl.Width * 0.5f, st.Position.y - pl.Height * 0.5f,
                st.Width, st.Height, st.FontSize, st.TextColor, st.Alignment, st.Visible);
            root.AddChild(refs.SpeakerText);

            // 正文（同上，左下原点 → 中心原点；2× 超采样）
            var bt = config.BodyText;
            refs.BodyText = BuildSupersampledText(
                $"{daoId}_Body", "Body",
                bt.Position.x - pl.Width * 0.5f, bt.Position.y - pl.Height * 0.5f,
                bt.Width, bt.Height, bt.FontSize, bt.TextColor, bt.Alignment, bt.Visible);
            root.AddChild(refs.BodyText);

            // Next 按钮
            var nb = config.NextButton;
            refs.NextButton = new UIButtonComponent($"{daoId}_NextBtn", "NextButton", nb.ButtonText)
                .SetPosition(nb.Position.x, nb.Position.y)
                .SetSize(nb.Width, nb.Height)
                .SetButtonSpriteId(nb.ButtonSpriteId)
                .SetVisible(nb.Visible)
                .SetInteractable(true);
            root.AddChild(refs.NextButton);

            // Close 按钮
            var cb = config.CloseButton;
            refs.CloseButton = new UIButtonComponent($"{daoId}_CloseBtn", "CloseButton", cb.ButtonText)
                .SetPosition(cb.Position.x, cb.Position.y)
                .SetSize(cb.Width, cb.Height)
                .SetButtonSpriteId(cb.ButtonSpriteId)
                .SetVisible(cb.Visible)
                .SetInteractable(cb.Interactable);
            root.AddChild(refs.CloseButton);

            // 选项按钮（默认全部隐藏，行有 options 时再显示）
            // 按钮为 UIButtonComponent，锚点在父左下角 + pivot 中心 → Position 直接使用「左下原点」。
            var op = config.Options;
            var maxOpts = Mathf.Max(1, op.MaxOptions);
            refs.OptionButtons = new UIButtonComponent[maxOpts];
            for (var i = 0; i < maxOpts; i++)
            {
                var x = op.FirstButtonOffset.x;
                var y = op.FirstButtonOffset.y - i * (op.ButtonHeight + op.Spacing);
                var btn = new UIButtonComponent($"{daoId}_Opt_{i}", $"Option_{i}", string.Empty)
                    .SetPosition(x, y)
                    .SetSize(op.ButtonWidth, op.ButtonHeight)
                    .SetButtonSpriteId(op.ButtonSpriteId)
                    .SetVisible(false)
                    .SetInteractable(false);
                refs.OptionButtons[i] = btn;
                root.AddChild(btn);
            }

            return (root, refs);
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

            // 立绘
            if (refs.Portrait != null)
            {
                var hasPortrait = !string.IsNullOrEmpty(line.PortraitSpriteId);
                refs.Portrait.SetVisible(hasPortrait);
                if (hasPortrait)
                {
                    refs.Portrait.BackgroundSpriteId = line.PortraitSpriteId;
                    refs.Portrait.BackgroundColor = Color.white;
                }
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
                    if (i < optCount)
                    {
                        btn.Text = line.Options[i].Text ?? string.Empty;
                        btn.SetVisible(true).SetInteractable(true);
                    }
                    else
                    {
                        btn.SetVisible(false).SetInteractable(false);
                    }
                }
            }
        }

        private static Color SpriteAwareTint(string spriteId, Color fallback) =>
            string.IsNullOrEmpty(spriteId) ? fallback : Color.white;

        /// <summary>
        /// 构造一个应用了文本超采样的 <see cref="UITextComponent"/>：
        /// FontSize × N、Size × N、Scale × (1/N)，最终视觉大小不变但字体清晰度显著提升。
        /// 参 <c>Assets/Agent.md §5「文本清晰度」</c>。
        /// </summary>
        private static UITextComponent BuildSupersampledText(
            string id, string name,
            float centerX, float centerY,
            float width, float height,
            int fontSize, Color color, TextAnchor alignment,
            bool visible)
        {
            var n = TextSupersample;
            var inv = 1f / n;
            var t = new UITextComponent(id, name)
                .SetPosition(centerX, centerY)              // Position 不放大（视觉中心不变）
                .SetSize(width * n, height * n)             // Rect 放大 N 倍
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(fontSize * n)))
                .SetColor(color)
                .SetAlignment(alignment)
                .SetText(string.Empty)
                .SetScale(inv, inv);                        // 整体缩小 1/N → 视觉与原始一致
            t.SetVisible(visible);
            return t;
        }
    }
}
