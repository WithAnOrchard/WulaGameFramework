using System;
using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;

namespace EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.Specs
{
    /// <summary>
    /// 对话 UI 配置 — 定义对话框尺寸/背景/说话者文本/正文/选项按钮的布局。
    /// <para>
    /// 通用子组件（Panel / Text / Button）改用 UIManager 的共享 Spec 类型；
    /// 选项按钮列表仍保留 <see cref="OptionsLayout"/>（多按钮堆叠是 Dialogue 独有需求）。
    /// </para>
    /// <para>
    /// 坐标系约定：
    /// • 子组件 <c>Position</c> 全部采用「父面板左下原点」并表示组件**中心**；
    /// <see cref="DialogueUIBuilder"/> 在挂载 UITextComponent 时会自动减去
    /// <c>PanelWidth/2, PanelHeight/2</c> 完成父中心锚点的转换。<br/>
    /// • <see cref="Panel"/>.<c>Position</c> 表示在 Canvas 上的世界坐标（典型 1920×1080）。
    /// </para>
    /// </summary>
    [Serializable]
    public class DialogueConfig
    {
        /// <summary>配置 Id（在 <c>DialogueService.CAT_CONFIGS</c> 中唯一）。</summary>
        public string ConfigId;

        /// <summary>展示名 / 调试名。</summary>
        public string DisplayName;

        /// <summary>主面板（背景 + 整体尺寸/位置）。默认 960×240，置于 1920×1080 底部居中偏上。</summary>
        public UIPanelSpec Panel = new UIPanelSpec()
            .WithSize(960f, 240f)
            .WithPosition(960f, 180f)
            .WithBackgroundColor(new Color(0.06f, 0.07f, 0.12f, 0.94f));

        /// <summary>说话者名（左下原点；中心位置 (440, 30)）。</summary>
        public UITextSpec SpeakerText = new UITextSpec(240f, 210f, 440f, 30f, 22, TextAnchor.MiddleLeft);

        /// <summary>正文（左下原点；中心位置 (820, 145)）。</summary>
        public UITextSpec BodyText = new UITextSpec(480f, 95f, 820f, 145f, 18, TextAnchor.UpperLeft);

        /// <summary>立绘面板（始终创建；行级 <c>PortraitSpriteId</c> 为空时隐藏）。左下原点。</summary>
        public UIPanelSpec Portrait = new UIPanelSpec()
            .WithSize(160f, 160f)
            .WithPosition(90f, 120f)
            .WithBackgroundColor(new Color(0f, 0f, 0f, 0f));

        /// <summary>"下一句" 按钮（无选项时显示）。左下原点。</summary>
        public UIButtonSpec NextButton = new UIButtonSpec(886f, 32f, 100f, 40f)
            .WithText("▶")
            .WithColor(new Color(0.25f, 0.5f, 0.85f, 0.9f));

        /// <summary>选项按钮列表（Dialogue 独有的多按钮堆叠布局）。</summary>
        public OptionsLayout Options = new OptionsLayout();

        /// <summary>关闭按钮（右上角）。左下原点。</summary>
        public UIButtonSpec CloseButton = new UIButtonSpec(942f, 222f, 36f, 36f)
            .WithText("×")
            .WithColor(new Color(0.8f, 0.3f, 0.3f, 1f));

        public DialogueConfig() { }

        public DialogueConfig(string configId, string displayName = null)
        {
            ConfigId = configId;
            DisplayName = displayName ?? configId;
        }

        public DialogueConfig WithPanel(UIPanelSpec p)        { Panel       = p ?? Panel;       return this; }
        public DialogueConfig WithSpeaker(UITextSpec t)       { SpeakerText = t ?? SpeakerText; return this; }
        public DialogueConfig WithBody(UITextSpec t)          { BodyText    = t ?? BodyText;    return this; }
        public DialogueConfig WithPortrait(UIPanelSpec p)     { Portrait    = p ?? Portrait;    return this; }
        public DialogueConfig WithNextButton(UIButtonSpec n)  { NextButton  = n ?? NextButton;  return this; }
        public DialogueConfig WithOptions(OptionsLayout o)    { Options     = o ?? Options;     return this; }
        public DialogueConfig WithCloseButton(UIButtonSpec c) { CloseButton = c ?? CloseButton; return this; }

        // ────────────────────────────────────────────────────────────
        // 选项按钮列表（Dialogue 独有 — 共享按钮 Spec + 列表布局参数）
        // ────────────────────────────────────────────────────────────

        /// <summary>选项按钮垂直堆叠布局。所有按钮共用 <see cref="ButtonTemplate"/> 的外观；
        /// 自上而下堆叠：第一个按钮中心 = <see cref="FirstButtonOffset"/>，
        /// 后续按钮 y 递减 <c>ButtonTemplate.Size.y + Spacing</c>。</summary>
        [Serializable]
        public class OptionsLayout
        {
            /// <summary>最大选项数（决定预创建多少按钮槽）。</summary>
            public int MaxOptions = 4;

            /// <summary>按钮间垂直间距。</summary>
            public float Spacing = 3f;

            /// <summary>第一个选项按钮中心位置（左下原点）。默认贴合 panel 960×240 底部。</summary>
            public Vector2 FirstButtonOffset = new Vector2(480f, 105f);

            /// <summary>所有选项按钮共用的外观模板（Size / Sprite / Color / FontSize 等）。</summary>
            public UIButtonSpec ButtonTemplate = new UIButtonSpec()
                .WithSize(380f, 26f)
                .WithColor(new Color(0.18f, 0.18f, 0.28f, 0.95f))
                .WithVisible(false)
                .WithInteractable(false);

            public OptionsLayout WithMaxOptions(int n)              { MaxOptions = Mathf.Max(1, n); return this; }
            public OptionsLayout WithSpacing(float s)               { Spacing = s; return this; }
            public OptionsLayout WithFirstButtonOffset(float x, float y) { FirstButtonOffset = new Vector2(x, y); return this; }
            public OptionsLayout WithButtonTemplate(UIButtonSpec t) { ButtonTemplate = t ?? ButtonTemplate; return this; }
        }
    }
}
