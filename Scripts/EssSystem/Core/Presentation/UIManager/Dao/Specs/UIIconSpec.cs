using System;
using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;

namespace EssSystem.Core.Presentation.UIManager.Dao.Specs
{
    /// <summary>
    /// 通用「图标」声明式配置 — 无文本的纯透明子 Panel 占位，业务侧通常自行往里填 Sprite。
    /// </summary>
    [Serializable]
    public class UIIconSpec
    {
        /// <summary>是否可见</summary>
        public bool Visible = true;

        /// <summary>中心位置</summary>
        public Vector2 Position = Vector2.zero;

        /// <summary>尺寸</summary>
        public Vector2 Size = new Vector2(64f, 64f);

        public UIIconSpec() { }

        public UIIconSpec(float x, float y, float w, float h)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
        }

        public UIIconSpec WithVisible(bool v)            { Visible = v; return this; }
        public UIIconSpec WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
        public UIIconSpec WithPosition(Vector2 pos)      { Position = pos; return this; }
        public UIIconSpec WithSize(float w, float h)
        {
            Size = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            return this;
        }

        /// <summary>把 Spec 应用到现成 <see cref="UIPanelComponent"/>（背景设透明，仅作占位）。</summary>
        public UIPanelComponent ApplyTo(UIPanelComponent panel)
        {
            if (panel == null) return null;
            panel.SetPosition(Position.x, Position.y)
                 .SetSize(Size.x, Size.y)
                 .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                 .SetVisible(Visible);
            return panel;
        }

        /// <summary>用 Spec 直接 new 一个图标占位 <see cref="UIPanelComponent"/>。</summary>
        public UIPanelComponent CreateComponent(string id, string name = null)
        {
            return ApplyTo(new UIPanelComponent(id, name));
        }
    }
}
