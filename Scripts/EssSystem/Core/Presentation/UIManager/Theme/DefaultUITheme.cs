using System;
using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager.Theme
{
    /// <summary>默认 UI 主题数据（包含 4 套预设）。</summary>
    public struct DefaultUIThemeData : IThemeData
    {
        public string Name;
        public Color Background;   // 面板背景
        public Color Header;       // 标题栏
        public Color Accent;       // 主强调色
        public Color AccentAlt;    // 辅助强调色
        public Color ButtonBg;     // 普通按钮背景
        public Color ButtonGreen;  // 正向/启用按钮
        public Color ButtonRed;    // 负向/停用按钮
        public Color Divider;      // 分隔线
        public Color ScrollBg;     // 滚动区背景
        public Color TextMain;     // 主文字（在 Body 上）
        public Color TextSub;      // 次要文字（在 Body 上）
        public Color TextOnHeader; // 标题栏文字（在 Header 上）
        public Color Close;        // 关闭按钮
    }

    /// <summary>
    /// 默认 UI 主题管理器（静态）。
    /// 预设 4 套主题；切换时广播 <see cref="OnThemeChanged"/>，所有面板监听后重建。
    /// </summary>
    public class DefaultUITheme : ThemeManager<DefaultUIThemeData>
    {
        private const string PrefKey_Value = "EssSystem.UIThemeIndex";

        protected override string PrefKey => PrefKey_Value;

        protected override DefaultUIThemeData[] BuildPresets()
        {
            return new[]
            {
                // ── 0  深夜（冷蓝暗色）──────────────────────────────────────────
                new DefaultUIThemeData
                {
                    Name        = "深夜",
                    Background  = new Color(0.09f, 0.10f, 0.13f, 0.97f),
                    Header      = new Color(0.05f, 0.06f, 0.09f, 1.00f),
                    Accent      = new Color(0.27f, 0.56f, 0.98f, 1.00f),
                    AccentAlt   = new Color(0.20f, 0.42f, 0.74f, 1.00f),
                    ButtonBg    = new Color(0.16f, 0.18f, 0.24f, 1.00f),
                    ButtonGreen = new Color(0.14f, 0.50f, 0.28f, 1.00f),
                    ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                    Divider     = new Color(0.30f, 0.30f, 0.35f, 1.00f),
                    ScrollBg    = new Color(0.05f, 0.06f, 0.09f, 0.97f),
                    TextMain    = new Color(0.95f, 0.95f, 0.95f, 1.00f),
                    TextSub     = new Color(0.65f, 0.65f, 0.70f, 1.00f),
                    TextOnHeader= new Color(1.00f, 1.00f, 1.00f, 1.00f),
                    Close       = new Color(0.85f, 0.35f, 0.35f, 1.00f),
                },

                // ── 1  暮光（暖棕暗色）──────────────────────────────────────────
                new DefaultUIThemeData
                {
                    Name        = "暮光",
                    Background  = new Color(0.13f, 0.11f, 0.08f, 0.97f),
                    Header      = new Color(0.08f, 0.06f, 0.04f, 1.00f),
                    Accent      = new Color(0.98f, 0.68f, 0.27f, 1.00f),
                    AccentAlt   = new Color(0.74f, 0.51f, 0.20f, 1.00f),
                    ButtonBg    = new Color(0.24f, 0.18f, 0.12f, 1.00f),
                    ButtonGreen = new Color(0.28f, 0.50f, 0.14f, 1.00f),
                    ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                    Divider     = new Color(0.40f, 0.34f, 0.20f, 1.00f),
                    ScrollBg    = new Color(0.08f, 0.06f, 0.04f, 0.97f),
                    TextMain    = new Color(0.96f, 0.91f, 0.80f, 1.00f),
                    TextSub     = new Color(0.70f, 0.60f, 0.38f, 1.00f),
                    TextOnHeader= new Color(0.886f, 0.757f, 0.486f, 1.00f),
                    Close       = new Color(0.66f, 0.20f, 0.17f, 1.00f),
                },

                // ── 2  森林（绿色暗色）──────────────────────────────────────────
                new DefaultUIThemeData
                {
                    Name        = "森林",
                    Background  = new Color(0.08f, 0.12f, 0.10f, 0.97f),
                    Header      = new Color(0.04f, 0.08f, 0.06f, 1.00f),
                    Accent      = new Color(0.27f, 0.98f, 0.56f, 1.00f),
                    AccentAlt   = new Color(0.20f, 0.74f, 0.42f, 1.00f),
                    ButtonBg    = new Color(0.16f, 0.24f, 0.18f, 1.00f),
                    ButtonGreen = new Color(0.14f, 0.50f, 0.28f, 1.00f),
                    ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                    Divider     = new Color(0.30f, 0.35f, 0.30f, 1.00f),
                    ScrollBg    = new Color(0.04f, 0.08f, 0.06f, 0.97f),
                    TextMain    = new Color(0.95f, 0.95f, 0.95f, 1.00f),
                    TextSub     = new Color(0.65f, 0.70f, 0.65f, 1.00f),
                    TextOnHeader= new Color(1.00f, 1.00f, 1.00f, 1.00f),
                    Close       = new Color(0.85f, 0.35f, 0.35f, 1.00f),
                },

                // ── 3  樱花（粉红暗色）──────────────────────────────────────────
                new DefaultUIThemeData
                {
                    Name        = "樱花",
                    Background  = new Color(0.13f, 0.08f, 0.11f, 0.97f),
                    Header      = new Color(0.08f, 0.04f, 0.06f, 1.00f),
                    Accent      = new Color(0.98f, 0.27f, 0.68f, 1.00f),
                    AccentAlt   = new Color(0.74f, 0.20f, 0.51f, 1.00f),
                    ButtonBg    = new Color(0.24f, 0.12f, 0.18f, 1.00f),
                    ButtonGreen = new Color(0.28f, 0.50f, 0.14f, 1.00f),
                    ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                    Divider     = new Color(0.40f, 0.20f, 0.34f, 1.00f),
                    ScrollBg    = new Color(0.08f, 0.04f, 0.06f, 0.97f),
                    TextMain    = new Color(0.96f, 0.80f, 0.91f, 1.00f),
                    TextSub     = new Color(0.70f, 0.38f, 0.60f, 1.00f),
                    TextOnHeader= new Color(0.886f, 0.486f, 0.757f, 1.00f),
                    Close       = new Color(0.66f, 0.17f, 0.20f, 1.00f),
                },
            };
        }

        /// <summary>单例实例。</summary>
        public static readonly DefaultUITheme Instance = new DefaultUITheme();
    }
}
