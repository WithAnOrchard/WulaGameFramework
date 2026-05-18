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
        private const float TextSupersample = 6f;

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

            // 立绘头像框：始终显示（无 sprite 时显示框色作为视觉占位）
            if (config.Portrait != null)
            {
                refs.Portrait = config.Portrait.CreateComponent($"{daoId}_Portrait", "Portrait");
                refs.PortraitFrameColor = config.Portrait.BackgroundColor;
                refs.Portrait.SetVisible(true);
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
                refs.OptionButtons[i] = btn;
                root.AddChild(btn);

                // 超采样文字覆盖层 —— 与按钮同心同尺寸（白字、居中）
                var label = BuildSupersampledRaw(
                    $"{daoId}_OptText_{i}", $"Option_{i}_Text",
                    centerX: x - pl.Size.x * 0.5f,        // BL 原点 → 中心原点
                    centerY: y - pl.Size.y * 0.5f,
                    width: tmpl.Size.x,
                    height: tmpl.Size.y,
                    fontSize: 18,                          // UIButtonSpec 没有 FontSize 字段，固定 18 即可
                    color: Color.white,
                    align: TextAnchor.MiddleCenter);
                label.SetVisible(false);
                refs.OptionTexts[i] = label;
                root.AddChild(label);
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

        /// <summary>构造超采样 <see cref="UITextComponent"/>（不依赖 spec）—— 给选项按钮文本覆盖层用。
        /// 入参 centerX/centerY 已是「父中心原点」，无需再转换。</summary>
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
