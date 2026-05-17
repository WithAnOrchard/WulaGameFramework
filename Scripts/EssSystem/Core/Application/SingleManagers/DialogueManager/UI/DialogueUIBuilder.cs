using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
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
        private const float TextSupersample = 2f;

        /// <summary>构建完整对话 UI 面板树。</summary>
        public static (UIPanelComponent panel, DialogueUIRefs refs)
            BuildPanelTree(string daoId, DialogueConfig config)
        {
            var pl = config.Panel;

            // 根面板（也是主背景）
            var root = pl.CreateComponent(daoId, "DialoguePanel");
            // Sprite 设置时强制使用白色色套，避免背景图被 BackgroundColor 染色
            root.SetBackgroundColor(SpriteAwareTint(pl.BackgroundSpriteId, pl.BackgroundColor))
                .SetVisible(true);

            var refs = new DialogueUIRefs { Root = root, Background = root };

            // 立绘（始终创建；行级 PortraitSpriteId 提供时显示）
            if (config.Portrait != null)
            {
                refs.Portrait = config.Portrait.CreateComponent($"{daoId}_Portrait", "Portrait");
                refs.Portrait.SetVisible(false);
                root.AddChild(refs.Portrait);
            }

            // 说话者名（左下原点 → 中心原点；2× 超采样）
            refs.SpeakerText = BuildSupersampledText(
                $"{daoId}_Speaker", "Speaker", config.SpeakerText, pl.Size);
            root.AddChild(refs.SpeakerText);

            // 正文（同上）
            refs.BodyText = BuildSupersampledText(
                $"{daoId}_Body", "Body", config.BodyText, pl.Size);
            root.AddChild(refs.BodyText);

            // Next 按钮
            refs.NextButton = config.NextButton.CreateComponent($"{daoId}_NextBtn", "NextButton");
            root.AddChild(refs.NextButton);

            // Close 按钮
            refs.CloseButton = config.CloseButton.CreateComponent($"{daoId}_CloseBtn", "CloseButton");
            root.AddChild(refs.CloseButton);

            // 选项按钮（默认全部隐藏，行有 options 时再显示）
            var op = config.Options;
            var maxOpts = Mathf.Max(1, op.MaxOptions);
            var tmpl = op.ButtonTemplate;
            var btnH = tmpl.Size.y;
            refs.OptionButtons = new UIButtonComponent[maxOpts];
            for (var i = 0; i < maxOpts; i++)
            {
                var x = op.FirstButtonOffset.x;
                var y = op.FirstButtonOffset.y - i * (btnH + op.Spacing);
                var btn = tmpl.CreateComponent($"{daoId}_Opt_{i}", $"Option_{i}");
                btn.SetText(string.Empty)
                   .SetPosition(x, y)
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
        /// 用 <see cref="UITextSpec"/> 构造一个应用了文本超采样的 <see cref="UITextComponent"/>。
        /// FontSize × N、Size × N、Scale × (1/N)，最终视觉大小不变但字体清晰度显著提升。
        /// 自动把 spec 中的「左下原点 + 组件中心」位置转换成 UITextEntity 的「父中心相对」坐标。
        /// 参 <c>Assets/Agent.md §5「文本清晰度」</c>。
        /// </summary>
        private static UITextComponent BuildSupersampledText(
            string id, string name, UITextSpec spec, Vector2 panelSize)
        {
            var n = TextSupersample;
            var inv = 1f / n;
            // 左下原点 → 父中心原点（UITextEntity 的锚点在父中心）
            var cx = spec.Position.x - panelSize.x * 0.5f;
            var cy = spec.Position.y - panelSize.y * 0.5f;
            var t = new UITextComponent(id, name)
                .SetPosition(cx, cy)                                            // Position 不放大（视觉中心不变）
                .SetSize(spec.Size.x * n, spec.Size.y * n)                       // Rect 放大 N 倍
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(spec.FontSize * n)))
                .SetColor(spec.TextColor)
                .SetAlignment(spec.Alignment)
                .SetText(string.Empty)
                .SetScale(inv, inv);                                             // 整体缩小 1/N → 视觉与原始一致
            t.SetVisible(spec.Visible);
            return t;
        }
    }
}
