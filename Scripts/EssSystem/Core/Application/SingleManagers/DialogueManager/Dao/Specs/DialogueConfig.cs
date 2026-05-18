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

        /// <summary>主面板（背景 + 整体尺寸/位置）。默认 960×240，置于 1920×1080 底部居中偏上。
        /// y=250 让对话框抬离屏幕底部 130px（中心 250 - 半高 120 = 屏底距离 130），不挡 HUD。
        /// 透明度 0.55 让游戏画面隐约可见，避免对话窗口压迫感。</summary>
        public UIPanelSpec Panel = new UIPanelSpec()
            .WithSize(960f, 240f)
            .WithPosition(960f, 250f)
            .WithBackgroundColor(new Color(0.05f, 0.06f, 0.10f, 0.55f));

        /// <summary>说话者名 —— 立绘右侧顶部（左下原点；中心位置 (320, 195)）。白字。</summary>
        public UITextSpec SpeakerText = new UITextSpec(400f, 36f, 320f, 195f, 24, TextAnchor.MiddleLeft)
            .WithTextColor(Color.white);

        /// <summary>正文 —— 占据立绘下方的主体区域（左下原点；中心位置 (490, 75)）。白字。</summary>
        public UITextSpec BodyText = new UITextSpec(880f, 110f, 490f, 75f, 18, TextAnchor.UpperLeft)
            .WithTextColor(Color.white);

        /// <summary>立绘头像框 —— 对话框左上角（左下原点；中心位置 (60, 180)）。
        /// 始终可见作为视觉占位；行级 <c>PortraitSpriteId</c> 提供时把头像贴进框里。</summary>
        public UIPanelSpec Portrait = new UIPanelSpec()
            .WithSize(96f, 96f)
            .WithPosition(60f, 180f)
            .WithBackgroundColor(new Color(0.20f, 0.18f, 0.26f, 0.95f));

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
            public float Spacing = 4f;

            /// <summary>第一个选项按钮中心位置（左下原点）。
            /// <para>居中堆叠 + 紧贴底部：x = 480（panel 960 居中），y = 120；
            /// 4 × (高 30 + 间距 4) → 中心序列 120/86/52/18，最后一个底边距离 panel 底 3px。</para></summary>
            public Vector2 FirstButtonOffset = new Vector2(480f, 120f);

            /// <summary>所有选项按钮共用的外观模板（Size / Sprite / Color / FontSize 等）。</summary>
            public UIButtonSpec ButtonTemplate = new UIButtonSpec()
                .WithSize(440f, 30f)
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
