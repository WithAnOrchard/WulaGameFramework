using System;
using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;

namespace EssSystem.Core.Presentation.UIManager.Dao.Specs
{
    /// <summary>
    /// 通用「文本」声明式配置 — 纯数据 + With* 链式 API + 一键转 <see cref="UITextComponent"/>。
    /// </summary>
    [Serializable]
    public class UITextSpec
    {
        /// <summary>文本框中心位置（含义由调用方决定）</summary>
        public Vector2 Position = Vector2.zero;

        /// <summary>文本框尺寸</summary>
        public Vector2 Size = new Vector2(200f, 40f);

        /// <summary>字体大小</summary>
        public int FontSize = 16;

        /// <summary>文本颜色</summary>
        public Color TextColor = Color.white;

        /// <summary>对齐方式</summary>
        public TextAnchor Alignment = TextAnchor.MiddleCenter;

        /// <summary>是否可见</summary>
        public bool Visible = true;

        /// <summary>初始文本（可被 builder 覆盖）</summary>
        public string Text = string.Empty;

        public UITextSpec() { }

        public UITextSpec(float width, float height)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        public UITextSpec(float width, float height, float x, float y, int fontSize, TextAnchor alignment)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            Position = new Vector2(x, y);
            FontSize = Mathf.Max(1, fontSize);
            Alignment = alignment;
        }

        public UITextSpec WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
        public UITextSpec WithPosition(Vector2 pos)      { Position = pos; return this; }

        public UITextSpec WithSize(float w, float h)
        {
            Size = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            return this;
        }

        public UITextSpec WithFontSize(int size)         { FontSize = Mathf.Max(1, size); return this; }
        public UITextSpec WithTextColor(Color color)     { TextColor = color; return this; }
        public UITextSpec WithAlignment(TextAnchor a)    { Alignment = a; return this; }
        public UITextSpec WithVisible(bool v)            { Visible = v; return this; }
        public UITextSpec WithText(string t)             { Text = t ?? string.Empty; return this; }

        /// <summary>把 Spec 字段应用到现成 <see cref="UITextComponent"/>（不覆盖 builder 已写入的 Text，除非传 <paramref name="useSpecText"/>=true）。</summary>
        public UITextComponent ApplyTo(UITextComponent text, bool useSpecText = false)
        {
            if (text == null) return null;
            text.SetPosition(Position.x, Position.y)
                .SetSize(Size.x, Size.y)
                .SetFontSize(FontSize)
                .SetColor(TextColor)
                .SetAlignment(Alignment)
                .SetVisible(Visible);
            if (useSpecText && !string.IsNullOrEmpty(Text)) text.SetText(Text);
            return text;
        }

        /// <summary>用 Spec 直接 new 一个 <see cref="UITextComponent"/>；<paramref name="initialText"/> 为空时回退到 <see cref="Text"/>。</summary>
        public UITextComponent CreateComponent(string id, string name = null, string initialText = null)
        {
            var t = initialText ?? Text ?? string.Empty;
            return ApplyTo(new UITextComponent(id, name, t));
        }
    }
}
