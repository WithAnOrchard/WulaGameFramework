using System;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao
{
    /// <summary>
    /// 描述面板配置 — 显示当前选中物品 Description 的子面板（背包独有复合 Config）。
    /// <para>本身是一个 <see cref="UIPanelSpec"/> 组合（面板尺寸 + 背景 + Offset），
    /// 内部含 3 个 <see cref="UITextSpec"/> 文本子组件（名称/数量/详细描述）+ 1 个 <see cref="UIIconSpec"/> 图标。</para>
    /// </summary>
    [Serializable]
    public class DescriptionPanelConfig
    {
        #region Fields

        /// <summary>面板宽度</summary>
        public float Width = 220f;

        /// <summary>面板高度</summary>
        public float Height = 180f;

        /// <summary>相对主面板左下角的偏移</summary>
        public Vector2 Offset = new Vector2(700f, 100f);

        /// <summary>背景 Sprite ID（由 ResourceManager 解析）</summary>
        public string BackgroundSpriteId;

        /// <summary>背景颜色（无 Sprite 时使用）</summary>
        public Color BackgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.92f);

        /// <summary>文本相对面板左下角的内边距偏移</summary>
        public Vector2 TextPadding = new Vector2(12f, 12f);

        /// <summary>默认字体大小（仅作为 With* 缺省 fallback；具体子组件以各自 Spec 字段为准）</summary>
        public int FontSize = 14;

        /// <summary>默认文本颜色（同上 fallback）</summary>
        public Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        /// <summary>没有选中物品时的占位文字</summary>
        public string EmptyPlaceholder = "（点击物品查看描述）";

        /// <summary>图标子组件（位置 / 尺寸 / 可见性）</summary>
        public UIIconSpec IconConfig = new UIIconSpec();

        /// <summary>名称文本子组件</summary>
        public UITextSpec NameConfig = new UITextSpec
        {
            Position  = new Vector2(150f, 428f),
            Size      = new Vector2(276f, 47f),
            FontSize  = 20,
            TextColor = new Color(0.95f, 0.95f, 0.95f, 1f),
            Alignment = TextAnchor.MiddleCenter,
        };

        /// <summary>数量文本子组件</summary>
        public UITextSpec StackConfig = new UITextSpec
        {
            Position  = new Vector2(150f, 293f),
            Size      = new Vector2(276f, 33f),
            FontSize  = 16,
            TextColor = new Color(1f, 0.85f, 0.4f, 1f),
            Alignment = TextAnchor.MiddleCenter,
        };

        /// <summary>详细描述文本子组件</summary>
        public UITextSpec DescTextConfig = new UITextSpec
        {
            Position  = new Vector2(150f, 149f),
            Size      = new Vector2(276f, 233f),
            FontSize  = 14,
            TextColor = new Color(0.95f, 0.95f, 0.95f, 1f),
            Alignment = TextAnchor.UpperLeft,
        };

        #endregion

        #region Constructors

        /// <summary>反序列化用</summary>
        public DescriptionPanelConfig() { }

        public DescriptionPanelConfig(float width, float height)
        {
            Width = Mathf.Max(1f, width);
            Height = Mathf.Max(1f, height);
        }

        #endregion

        #region Chain API

        public DescriptionPanelConfig WithSize(float width, float height)
        {
            Width = Mathf.Max(1f, width);
            Height = Mathf.Max(1f, height);
            return this;
        }

        public DescriptionPanelConfig WithOffset(float x, float y) { Offset = new Vector2(x, y); return this; }
        public DescriptionPanelConfig WithOffset(Vector2 offset)   { Offset = offset; return this; }
        public DescriptionPanelConfig WithBackgroundId(string id)  { BackgroundSpriteId = id; return this; }
        public DescriptionPanelConfig WithBackgroundColor(Color c) { BackgroundColor = c; return this; }
        public DescriptionPanelConfig WithTextPadding(float x, float y) { TextPadding = new Vector2(x, y); return this; }
        public DescriptionPanelConfig WithFontSize(int size)       { FontSize = Mathf.Max(1, size); return this; }
        public DescriptionPanelConfig WithTextColor(Color color)   { TextColor = color; return this; }
        public DescriptionPanelConfig WithEmptyPlaceholder(string text) { EmptyPlaceholder = text ?? string.Empty; return this; }

        public DescriptionPanelConfig WithIconConfig(UIIconSpec cfg)
        {
            IconConfig = cfg ?? new UIIconSpec();
            return this;
        }

        public DescriptionPanelConfig WithNameConfig(UITextSpec cfg)
        {
            NameConfig = cfg ?? new UITextSpec();
            return this;
        }

        public DescriptionPanelConfig WithStackConfig(UITextSpec cfg)
        {
            StackConfig = cfg ?? new UITextSpec();
            return this;
        }

        public DescriptionPanelConfig WithDescTextConfig(UITextSpec cfg)
        {
            DescTextConfig = cfg ?? new UITextSpec();
            return this;
        }

        #endregion
    }
}
