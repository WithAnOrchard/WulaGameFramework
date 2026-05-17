using System;
using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;

namespace EssSystem.Core.Presentation.UIManager.Dao.Specs
{
    /// <summary>
    /// 通用「按钮」声明式配置 — 纯数据 + With* 链式 API + 一键转 <see cref="UIButtonComponent"/>。
    /// </summary>
    [Serializable]
    public class UIButtonSpec
    {
        /// <summary>按钮中心位置（含义由调用方决定）</summary>
        public Vector2 Position = new Vector2(380f, 280f);

        /// <summary>按钮尺寸</summary>
        public Vector2 Size = new Vector2(32f, 32f);

        /// <summary>按钮缩放</summary>
        public Vector2 Scale = Vector2.one;

        /// <summary>按钮文本（默认 ×）</summary>
        public string Text = "×";

        /// <summary>按钮背景 Sprite ID（经 ResourceManager 解析）</summary>
        public string ButtonSpriteId;

        /// <summary>按钮 Image.color；有 Sprite 时作为色套，无 Sprite 时作为纯色块</summary>
        public Color ButtonColor = new Color(1f, 0.3f, 0.3f, 1f);

        /// <summary>是否可见</summary>
        public bool Visible = true;

        /// <summary>是否可交互</summary>
        public bool Interactable = true;

        public UIButtonSpec() { }

        public UIButtonSpec(float x, float y, float width, float height)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        public UIButtonSpec WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
        public UIButtonSpec WithPosition(Vector2 pos)      { Position = pos; return this; }

        public UIButtonSpec WithSize(float w, float h)
        {
            Size = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            return this;
        }

        public UIButtonSpec WithScale(float x, float y) { Scale = new Vector2(x, y); return this; }
        public UIButtonSpec WithText(string text)       { Text = text ?? "×"; return this; }
        public UIButtonSpec WithSpriteId(string id)     { ButtonSpriteId = id; return this; }
        public UIButtonSpec WithColor(Color c)          { ButtonColor = c; return this; }
        public UIButtonSpec WithVisible(bool v)         { Visible = v; return this; }
        public UIButtonSpec WithInteractable(bool i)    { Interactable = i; return this; }

        /// <summary>把 Spec 字段应用到现成 <see cref="UIButtonComponent"/>。</summary>
        public UIButtonComponent ApplyTo(UIButtonComponent btn)
        {
            if (btn == null) return null;
            btn.SetText(Text)
               .SetPosition(Position.x, Position.y)
               .SetSize(Size.x, Size.y)
               .SetScale(Scale.x, Scale.y)
               .SetButtonSpriteId(ButtonSpriteId)
               .SetButtonColor(ButtonColor)
               .SetVisible(Visible)
               .SetInteractable(Interactable);
            return btn;
        }

        /// <summary>用 Spec 直接 new 一个 <see cref="UIButtonComponent"/>。</summary>
        public UIButtonComponent CreateComponent(string id, string name = null)
        {
            return ApplyTo(new UIButtonComponent(id, name, Text));
        }
    }
}
