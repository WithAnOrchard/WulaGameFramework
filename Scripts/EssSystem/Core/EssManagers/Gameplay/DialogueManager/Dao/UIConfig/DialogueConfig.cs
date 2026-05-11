using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.DialogueManager.Dao.UIConfig
{
    /// <summary>
    /// 对话 UI 配置 — 定义对话框尺寸/背景/说话者文本/正文/选项按钮的布局。
    /// <para>
    /// 坐标系约定（与 <c>InventoryConfig</c> 一致）：<br/>
    /// • 子组件位置（Panel / Button / Text 的 <c>Position</c>）一律采用「父面板左下角原点」<br/>
    ///   X ∈ [0, PanelWidth]，Y ∈ [0, PanelHeight]，且 Position 表示组件 <b>中心</b>。<br/>
    /// • 父面板自身的 <see cref="PanelLayout.Position"/> 表示在 Canvas 上的世界坐标（典型 1920×1080）。<br/>
    /// • <c>UITextComponent</c> 在 Unity 端实际锚点是父中心，<see cref="DialogueUIBuilder"/>
    ///   会自动减去 <c>PanelWidth/2, PanelHeight/2</c> 完成转换。
    /// </para>
    /// </summary>
    [Serializable]
    public class DialogueConfig
    {
        /// <summary>配置 Id（在 <c>DialogueService.CAT_CONFIGS</c> 中唯一）。</summary>
        public string ConfigId;

        /// <summary>展示名 / 调试名。</summary>
        public string DisplayName;

        public PanelLayout Panel = new PanelLayout();
        // 默认面板 960×240：以下子组件 Position 全用「左下原点」
        public TextLayout SpeakerText = new TextLayout(440f, 30f, 240f, 210f, 22, TextAnchor.MiddleLeft);
        public TextLayout BodyText    = new TextLayout(820f, 145f, 480f, 95f, 18, TextAnchor.UpperLeft);
        public PortraitLayout Portrait = new PortraitLayout();
        public NextButtonLayout NextButton = new NextButtonLayout();
        public OptionsLayout Options = new OptionsLayout();
        public CloseButtonLayout CloseButton = new CloseButtonLayout();

        public DialogueConfig() { }

        public DialogueConfig(string configId, string displayName = null)
        {
            ConfigId = configId;
            DisplayName = displayName ?? configId;
        }

        public DialogueConfig WithPanel(PanelLayout p) { Panel = p ?? Panel; return this; }
        public DialogueConfig WithSpeaker(TextLayout t) { SpeakerText = t ?? SpeakerText; return this; }
        public DialogueConfig WithBody(TextLayout t) { BodyText = t ?? BodyText; return this; }
        public DialogueConfig WithPortrait(PortraitLayout p) { Portrait = p ?? Portrait; return this; }
        public DialogueConfig WithNextButton(NextButtonLayout n) { NextButton = n ?? NextButton; return this; }
        public DialogueConfig WithOptions(OptionsLayout o) { Options = o ?? Options; return this; }
        public DialogueConfig WithCloseButton(CloseButtonLayout c) { CloseButton = c ?? CloseButton; return this; }

        // ────────────────────────────────────────────────────────────
        // 子配置类
        // ────────────────────────────────────────────────────────────

        /// <summary>主对话框：尺寸 + Canvas 上的世界坐标 + 背景。</summary>
        [Serializable]
        public class PanelLayout
        {
            public float Width = 960f;
            public float Height = 240f;
            /// <summary>面板在 Canvas 上的中心位置（默认假设 1920×1080，置于底部居中偏上）。</summary>
            public Vector2 Position = new Vector2(960f, 180f);
            public Vector2 Scale = Vector2.one;
            public string BackgroundSpriteId;
            public Color BackgroundColor = new Color(0.06f, 0.07f, 0.12f, 0.94f);

            public PanelLayout WithSize(float w, float h) { Width = Mathf.Max(1f, w); Height = Mathf.Max(1f, h); return this; }
            public PanelLayout WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
            public PanelLayout WithScale(float x, float y) { Scale = new Vector2(x, y); return this; }
            public PanelLayout WithBackgroundId(string id) { BackgroundSpriteId = id; return this; }
            public PanelLayout WithBackgroundColor(Color c) { BackgroundColor = c; return this; }
        }

        /// <summary>通用文本子组件配置（说话者名 / 正文）。</summary>
        [Serializable]
        public class TextLayout
        {
            public float Width;
            public float Height;
            public Vector2 Position;     // 相对父面板中心
            public int FontSize;
            public Color TextColor = Color.white;
            public TextAnchor Alignment = TextAnchor.UpperLeft;
            public bool Visible = true;

            public TextLayout() { }
            public TextLayout(float w, float h, float x, float y, int fontSize, TextAnchor align)
            {
                Width = w; Height = h;
                Position = new Vector2(x, y);
                FontSize = fontSize;
                Alignment = align;
            }
            public TextLayout WithSize(float w, float h) { Width = w; Height = h; return this; }
            public TextLayout WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
            public TextLayout WithFontSize(int size) { FontSize = Mathf.Max(1, size); return this; }
            public TextLayout WithColor(Color c) { TextColor = c; return this; }
            public TextLayout WithAlignment(TextAnchor a) { Alignment = a; return this; }
            public TextLayout WithVisible(bool v) { Visible = v; return this; }
        }

        /// <summary>立绘配置（行级 PortraitSpriteId 提供时显示，否则隐藏）。Position 用「左下原点」。</summary>
        [Serializable]
        public class PortraitLayout
        {
            public float Width = 160f;
            public float Height = 160f;
            public Vector2 Position = new Vector2(90f, 120f); // 面板左侧中部
            public Color Tint = Color.white;
            public bool Enabled = true;

            public PortraitLayout WithSize(float w, float h) { Width = w; Height = h; return this; }
            public PortraitLayout WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
            public PortraitLayout WithTint(Color c) { Tint = c; return this; }
            public PortraitLayout WithEnabled(bool e) { Enabled = e; return this; }
        }

        /// <summary>"下一句"按钮（无选项时显示）。Position 用「左下原点」。</summary>
        [Serializable]
        public class NextButtonLayout
        {
            public float Width = 100f;
            public float Height = 40f;
            public Vector2 Position = new Vector2(886f, 32f); // 面板右下角（距右下 24×12）
            public string ButtonText = "▶";
            public string ButtonSpriteId;
            public Color ButtonColor = new Color(0.25f, 0.5f, 0.85f, 0.9f);
            public int FontSize = 22;
            public bool Visible = true;

            public NextButtonLayout WithSize(float w, float h) { Width = w; Height = h; return this; }
            public NextButtonLayout WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
            public NextButtonLayout WithText(string s) { ButtonText = s ?? string.Empty; return this; }
            public NextButtonLayout WithSpriteId(string id) { ButtonSpriteId = id; return this; }
            public NextButtonLayout WithColor(Color c) { ButtonColor = c; return this; }
            public NextButtonLayout WithFontSize(int s) { FontSize = Mathf.Max(1, s); return this; }
            public NextButtonLayout WithVisible(bool v) { Visible = v; return this; }
        }

        /// <summary>选项按钮列表布局（垂直堆叠覆盖正文区；行有选项时正文自动隐藏）。Position 用「左下原点」。</summary>
        [Serializable]
        public class OptionsLayout
        {
            public int MaxOptions = 4;
            public float ButtonWidth = 380f;
            public float ButtonHeight = 26f;
            public float Spacing = 3f;
            /// <summary>第一个选项按钮中心位置（左下原点）；后续按钮向下堆叠。
            /// <para>默认值靠近 panel 960×240 底部：4 个 26 高 + 3 间距按钮，
            /// 自上而下中心 y=105/76/47/18，整体贴底排布。</para></summary>
            public Vector2 FirstButtonOffset = new Vector2(480f, 105f);
            public string ButtonSpriteId;
            public Color ButtonColor = new Color(0.18f, 0.18f, 0.28f, 0.95f);
            public int FontSize = 18;
            public Color TextColor = Color.white;
            public TextAnchor Alignment = TextAnchor.MiddleCenter;

            public OptionsLayout WithMaxOptions(int n) { MaxOptions = Mathf.Max(1, n); return this; }
            public OptionsLayout WithButtonSize(float w, float h) { ButtonWidth = w; ButtonHeight = h; return this; }
            public OptionsLayout WithSpacing(float s) { Spacing = s; return this; }
            public OptionsLayout WithFirstButtonOffset(float x, float y) { FirstButtonOffset = new Vector2(x, y); return this; }
            public OptionsLayout WithButtonSpriteId(string id) { ButtonSpriteId = id; return this; }
            public OptionsLayout WithButtonColor(Color c) { ButtonColor = c; return this; }
            public OptionsLayout WithFontSize(int s) { FontSize = Mathf.Max(1, s); return this; }
            public OptionsLayout WithTextColor(Color c) { TextColor = c; return this; }
            public OptionsLayout WithAlignment(TextAnchor a) { Alignment = a; return this; }
        }

        /// <summary>关闭按钮（右上角）。Position 用「左下原点」。</summary>
        [Serializable]
        public class CloseButtonLayout
        {
            public float Width = 36f;
            public float Height = 36f;
            public Vector2 Position = new Vector2(942f, 222f); // 面板右上角（紧贴边框）
            public string ButtonText = "×";
            public string ButtonSpriteId;
            public Color ButtonColor = new Color(0.8f, 0.3f, 0.3f, 1f);
            public int FontSize = 22;
            public bool Visible = true;
            public bool Interactable = true;

            public CloseButtonLayout WithSize(float w, float h) { Width = w; Height = h; return this; }
            public CloseButtonLayout WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
            public CloseButtonLayout WithText(string s) { ButtonText = s ?? string.Empty; return this; }
            public CloseButtonLayout WithSpriteId(string id) { ButtonSpriteId = id; return this; }
            public CloseButtonLayout WithColor(Color c) { ButtonColor = c; return this; }
            public CloseButtonLayout WithFontSize(int s) { FontSize = Mathf.Max(1, s); return this; }
            public CloseButtonLayout WithVisible(bool v) { Visible = v; return this; }
            public CloseButtonLayout WithInteractable(bool i) { Interactable = i; return this; }
        }
    }
}
