using System;
using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>DobeCat UI 主题全部语义颜色。</summary>
    public struct DobeCatThemeData
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
        public Color TextOnHeader; // 标题栏文字（在 Header 上）—— 允许 Header 与 Body 亮度差异较大
        public Color Close;        // 关闭按钮
    }

    /// <summary>
    /// DobeCat 主题管理器（静态）。
    /// 预设 4 套主题；切换时广播 <see cref="OnThemeChanged"/>，所有面板监听后重建。
    /// </summary>
    public static class DobeCatTheme
    {
        private const string PrefKey = "DobeCat.ThemeIndex";
        private static int _index;

        /// <summary>主题变更广播（主线程）。</summary>
        public static event Action OnThemeChanged;

        public static DobeCatThemeData Current => Presets[_index];
        public static int CurrentIndex => _index;

        public static readonly DobeCatThemeData[] Presets =
        {
            // ── 0  深夜（冷蓝暗色）──────────────────────────────────────────
            new DobeCatThemeData
            {
                Name        = "深夜",
                Background  = new Color(0.09f, 0.10f, 0.13f, 0.97f),
                Header      = new Color(0.05f, 0.06f, 0.09f, 1.00f),
                Accent      = new Color(0.27f, 0.56f, 0.98f, 1.00f),
                AccentAlt   = new Color(0.20f, 0.42f, 0.74f, 1.00f),
                ButtonBg    = new Color(0.16f, 0.18f, 0.24f, 1.00f),
                ButtonGreen = new Color(0.14f, 0.50f, 0.28f, 1.00f),
                ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                Divider     = new Color(0.20f, 0.22f, 0.30f, 1.00f),
                ScrollBg    = new Color(0.05f, 0.06f, 0.09f, 1.00f),
                TextMain    = new Color(0.92f, 0.93f, 0.96f, 1.00f),
                TextSub     = new Color(0.50f, 0.55f, 0.65f, 1.00f),
                TextOnHeader= new Color(0.92f, 0.93f, 0.96f, 1.00f),
                Close       = new Color(0.68f, 0.16f, 0.16f, 1.00f),
            },
            // ── 1  海洋（#2C80C5 蓝 + #FFFAE6 暖白）————————————————————————
            // 面板背景直接用 #2C80C5；主文字/Header 用 #FFFAE6 奶白；强烈"蓝+白"视觉
            new DobeCatThemeData
            {
                Name        = "海洋",
                Background  = new Color(0.173f,0.502f,0.773f,0.97f),  // #2C80C5 蓝面板
                Header      = new Color(1.00f, 0.980f,0.902f,1.00f),  // #FFFAE6 奶白标题栏
                Accent      = new Color(0.10f, 0.32f, 0.55f, 1.00f),  // 深蓝（icon在奶白头上可见）
                AccentAlt   = new Color(1.00f, 0.980f,0.902f,1.00f),  // #FFFAE6
                ButtonBg    = new Color(0.10f, 0.32f, 0.55f, 1.00f),  // 深蓝按钮（白字可读）
                ButtonGreen = new Color(0.12f, 0.50f, 0.30f, 1.00f),
                ButtonRed   = new Color(0.62f, 0.18f, 0.20f, 1.00f),
                Divider     = new Color(1.00f, 0.980f,0.902f,0.65f),  // 奶白分隔线
                ScrollBg    = new Color(0.09f, 0.32f, 0.56f, 0.96f),  // 深蓝背板（灰文字可读）
                TextMain    = new Color(1.00f, 0.980f,0.902f,1.00f),  // #FFFAE6（在蓝面板上）
                TextSub     = new Color(0.92f, 0.95f, 1.00f, 0.85f),
                TextOnHeader= new Color(0.08f, 0.22f, 0.40f, 1.00f),  // 深身蓝（在奶白头上清晰可见）
                Close       = new Color(0.78f, 0.20f, 0.22f, 1.00f),
            },
            // ── 2  暮光（#363433 暖棕 + #E2C17C 金黄）──────────────────────
            // Background 直接用 #363433；强调色用 #E2C17C 金
            new DobeCatThemeData
            {
                Name        = "暮光",
                Background  = new Color(0.212f,0.204f,0.200f,0.97f),  // #363433
                Header      = new Color(0.10f, 0.10f, 0.09f, 1.00f),
                Accent      = new Color(0.886f,0.757f,0.486f,1.00f),  // #E2C17C
                AccentAlt   = new Color(0.62f, 0.50f, 0.27f, 1.00f),
                ButtonBg    = new Color(0.28f, 0.27f, 0.25f, 1.00f),
                ButtonGreen = new Color(0.24f, 0.46f, 0.20f, 1.00f),
                ButtonRed   = new Color(0.54f, 0.18f, 0.16f, 1.00f),
                Divider     = new Color(0.40f, 0.34f, 0.20f, 1.00f),
                ScrollBg    = new Color(0.11f, 0.10f, 0.09f, 1.00f),
                TextMain    = new Color(0.96f, 0.91f, 0.80f, 1.00f),
                TextSub     = new Color(0.70f, 0.60f, 0.38f, 1.00f),
                TextOnHeader= new Color(0.886f,0.757f,0.486f,1.00f),  // 金 #E2C17C
                Close       = new Color(0.66f, 0.20f, 0.17f, 1.00f),
            },
            // ── 3  樱花（#66A9C9 天蓝 + #F0C9CF 粉）────────────────────────
            // Header 直接用 #66A9C9；主文字用 #F0C9CF 粉白
            new DobeCatThemeData
            {
                Name        = "樱花",
                Background  = new Color(0.07f, 0.10f, 0.13f, 0.97f),
                Header      = new Color(0.400f,0.663f,0.788f,1.00f),  // #66A9C9
                Accent      = new Color(0.400f,0.663f,0.788f,1.00f),  // #66A9C9
                AccentAlt   = new Color(0.941f,0.788f,0.812f,1.00f),  // #F0C9CF
                ButtonBg    = new Color(0.12f, 0.18f, 0.25f, 1.00f),
                ButtonGreen = new Color(0.14f, 0.44f, 0.38f, 1.00f),
                ButtonRed   = new Color(0.62f, 0.20f, 0.28f, 1.00f),
                Divider     = new Color(0.24f, 0.36f, 0.46f, 1.00f),
                ScrollBg    = new Color(0.04f, 0.06f, 0.09f, 1.00f),
                TextMain    = new Color(0.941f,0.788f,0.812f,1.00f),  // #F0C9CF
                TextSub     = new Color(0.62f, 0.68f, 0.74f, 1.00f),
                TextOnHeader= new Color(0.07f, 0.10f, 0.13f, 1.00f),  // 深色（在 #66A9C9 浅蓝头上可见）
                Close       = new Color(0.78f, 0.28f, 0.38f, 1.00f),
            },
            // ── 4  极光 (深夜×海洋)：深夜黑体 + 海洋蓝头 + 奶白文字 + 电蓝accent ──
            new DobeCatThemeData
            {
                Name        = "极光",
                Background  = new Color(0.09f, 0.10f, 0.13f, 0.97f),  // 深夜 黑体
                Header      = new Color(0.173f,0.502f,0.773f,1.00f),  // 海洋 蓝头
                Accent      = new Color(0.27f, 0.56f, 0.98f, 1.00f),  // 深夜 电蓝accent
                AccentAlt   = new Color(1.00f, 0.980f,0.902f,1.00f),  // 海洋 奶白
                ButtonBg    = new Color(0.10f, 0.32f, 0.55f, 1.00f),  // 海洋 深蓝按钮
                ButtonGreen = new Color(0.14f, 0.50f, 0.28f, 1.00f),
                ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                Divider     = new Color(0.173f,0.502f,0.773f,0.70f),  // 海洋蓝分隔
                ScrollBg    = new Color(0.05f, 0.06f, 0.09f, 0.97f),  // 深夜 深滚动背板
                TextMain    = new Color(1.00f, 0.980f,0.902f,1.00f),  // 海洋 奶白文字
                TextSub     = new Color(0.92f, 0.95f, 1.00f, 0.80f),
                TextOnHeader= new Color(1.00f, 0.980f,0.902f,1.00f),  // 奶白（在蓝头上）
                Close       = new Color(0.68f, 0.16f, 0.16f, 1.00f),
            },
            // ── 5  星烛 (深夜×暮光)：深夜黑体 + 暮光暗头 + 金accent + 暖奶油文字 ──
            new DobeCatThemeData
            {
                Name        = "星烛",
                Background  = new Color(0.09f, 0.10f, 0.13f, 0.97f),  // 深夜 黑体
                Header      = new Color(0.10f, 0.10f, 0.09f, 1.00f),  // 暮光 暗暖头
                Accent      = new Color(0.886f,0.757f,0.486f,1.00f),  // 暮光 金accent
                AccentAlt   = new Color(0.27f, 0.56f, 0.98f, 1.00f),  // 深夜 电蓝
                ButtonBg    = new Color(0.16f, 0.18f, 0.24f, 1.00f),  // 深夜 按钮
                ButtonGreen = new Color(0.14f, 0.50f, 0.28f, 1.00f),
                ButtonRed   = new Color(0.52f, 0.16f, 0.16f, 1.00f),
                Divider     = new Color(0.40f, 0.34f, 0.20f, 1.00f),  // 暮光 金棕分隔
                ScrollBg    = new Color(0.05f, 0.06f, 0.09f, 0.97f),
                TextMain    = new Color(0.96f, 0.91f, 0.80f, 1.00f),  // 暮光 暖奶油文字
                TextSub     = new Color(0.70f, 0.60f, 0.38f, 1.00f),  // 暮光 金棕次文字
                TextOnHeader= new Color(0.886f,0.757f,0.486f,1.00f),  // 金（在暗头上）
                Close       = new Color(0.66f, 0.20f, 0.17f, 1.00f),
            },
            // ── 6  幽蓝 (深夜×樱花)：深夜黑体 + 樱花天蓝头 + 粉文字 + 电蓝accent ──
            new DobeCatThemeData
            {
                Name        = "幽蓝",
                Background  = new Color(0.09f, 0.10f, 0.13f, 0.97f),  // 深夜 黑体
                Header      = new Color(0.400f,0.663f,0.788f,1.00f),  // 樱花 天蓝头
                Accent      = new Color(0.27f, 0.56f, 0.98f, 1.00f),  // 深夜 电蓝accent
                AccentAlt   = new Color(0.941f,0.788f,0.812f,1.00f),  // 樱花 粉
                ButtonBg    = new Color(0.16f, 0.18f, 0.24f, 1.00f),  // 深夜 按钮
                ButtonGreen = new Color(0.14f, 0.44f, 0.38f, 1.00f),
                ButtonRed   = new Color(0.62f, 0.20f, 0.28f, 1.00f),
                Divider     = new Color(0.24f, 0.36f, 0.46f, 1.00f),  // 樱花 分隔
                ScrollBg    = new Color(0.04f, 0.06f, 0.09f, 0.97f),
                TextMain    = new Color(0.941f,0.788f,0.812f,1.00f),  // 樱花 粉文字
                TextSub     = new Color(0.50f, 0.55f, 0.65f, 1.00f),  // 深夜 冷灰次文字
                TextOnHeader= new Color(0.07f, 0.10f, 0.13f, 1.00f),  // 深黑（在天蓝头上）
                Close       = new Color(0.78f, 0.28f, 0.38f, 1.00f),
            },
            // ── 7  金海 (海洋×暮光)：海洋蓝体 + 暮光暖棕头 + 金accent + 奶白文字 ──
            new DobeCatThemeData
            {
                Name        = "金海",
                Background  = new Color(0.173f,0.502f,0.773f,0.97f),  // 海洋 蓝体
                Header      = new Color(0.212f,0.204f,0.200f,1.00f),  // 暮光 暖棕头
                Accent      = new Color(0.886f,0.757f,0.486f,1.00f),  // 暮光 金accent
                AccentAlt   = new Color(1.00f, 0.980f,0.902f,1.00f),  // 海洋 奶白
                ButtonBg    = new Color(0.28f, 0.27f, 0.25f, 1.00f),  // 暮光 按钮暖棕
                ButtonGreen = new Color(0.12f, 0.50f, 0.30f, 1.00f),
                ButtonRed   = new Color(0.62f, 0.18f, 0.20f, 1.00f),
                Divider     = new Color(0.886f,0.757f,0.486f,0.60f),  // 金分隔线
                ScrollBg    = new Color(0.09f, 0.32f, 0.56f, 0.97f),  // 深蓝滚动背板
                TextMain    = new Color(1.00f, 0.980f,0.902f,1.00f),  // 海洋 奶白文字
                TextSub     = new Color(0.886f,0.757f,0.486f,0.90f),  // 金次文字（在蓝体上）
                TextOnHeader= new Color(0.886f,0.757f,0.486f,1.00f),  // 金（在暖棕头上）
                Close       = new Color(0.66f, 0.20f, 0.17f, 1.00f),
            },
            // ── 8  珊瑚 (海洋×樱花)：海洋蓝体 + 樱花粉头 + 天蓝accent + 奶白文字 ──
            new DobeCatThemeData
            {
                Name        = "珊瑚",
                Background  = new Color(0.173f,0.502f,0.773f,0.97f),  // 海洋 蓝体
                Header      = new Color(0.941f,0.788f,0.812f,1.00f),  // 樱花 粉头
                Accent      = new Color(0.400f,0.663f,0.788f,1.00f),  // 樱花 天蓝accent
                AccentAlt   = new Color(0.10f, 0.32f, 0.55f, 1.00f),  // 海洋 深蓝
                ButtonBg    = new Color(0.10f, 0.32f, 0.55f, 1.00f),  // 海洋 深蓝按钮
                ButtonGreen = new Color(0.12f, 0.50f, 0.30f, 1.00f),
                ButtonRed   = new Color(0.62f, 0.18f, 0.20f, 1.00f),
                Divider     = new Color(0.941f,0.788f,0.812f,0.60f),  // 粉分隔线
                ScrollBg    = new Color(0.09f, 0.32f, 0.56f, 0.97f),  // 深蓝滚动背板
                TextMain    = new Color(1.00f, 0.980f,0.902f,1.00f),  // 海洋 奶白文字
                TextSub     = new Color(0.92f, 0.95f, 1.00f, 0.85f),
                TextOnHeader= new Color(0.10f, 0.06f, 0.09f, 1.00f),  // 深色（在粉头上）
                Close       = new Color(0.78f, 0.28f, 0.38f, 1.00f),
            },
            // ── 9  晚霞 (暮光×樱花)：暮光暖棕体 + 樱花天蓝头 + 金accent + 暖奶油文字 ──
            new DobeCatThemeData
            {
                Name        = "晚霞",
                Background  = new Color(0.212f,0.204f,0.200f,0.97f),  // 暮光 暖棕体
                Header      = new Color(0.400f,0.663f,0.788f,1.00f),  // 樱花 天蓝头
                Accent      = new Color(0.886f,0.757f,0.486f,1.00f),  // 暮光 金accent
                AccentAlt   = new Color(0.941f,0.788f,0.812f,1.00f),  // 樱花 粉
                ButtonBg    = new Color(0.28f, 0.27f, 0.25f, 1.00f),  // 暮光 暖棕按钮
                ButtonGreen = new Color(0.24f, 0.46f, 0.20f, 1.00f),
                ButtonRed   = new Color(0.54f, 0.18f, 0.16f, 1.00f),
                Divider     = new Color(0.62f, 0.50f, 0.27f, 1.00f),  // 暮光 金棕分隔
                ScrollBg    = new Color(0.11f, 0.10f, 0.09f, 0.97f),  // 暮光 深滚动背板
                TextMain    = new Color(0.96f, 0.91f, 0.80f, 1.00f),  // 暮光 暖奶油文字
                TextSub     = new Color(0.70f, 0.60f, 0.38f, 1.00f),  // 暮光 金棕次文字
                TextOnHeader= new Color(0.10f, 0.08f, 0.05f, 1.00f),  // 深暖色（在天蓝头上）
                Close       = new Color(0.66f, 0.20f, 0.17f, 1.00f),
            },
        };

        /// <summary>应从 Awake 或早期启动调用一次，以从 PlayerPrefs 恢复上次选择。</summary>
        public static void LoadSaved()
        {
            _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 1), 0, Presets.Length - 1);
        }

        /// <summary>切换主题并通知所有监听者重建 UI。</summary>
        public static void Apply(int index)
        {
            index = Mathf.Clamp(index, 0, Presets.Length - 1);
            if (_index == index) return;
            _index = index;
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();
            try { OnThemeChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
