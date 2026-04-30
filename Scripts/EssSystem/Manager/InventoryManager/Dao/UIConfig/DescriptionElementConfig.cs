using System;
using UnityEngine;

namespace EssSystem.EssManager.InventoryManager.Dao
{
    /// <summary>
    /// 描述面板内「图标」子组件配置（无文本属性的纯 UIPanel）
    /// <para><see cref="Position"/> 为 rect 中心相对描述面板左下角的坐标（与 <see cref="TitleConfig"/> 同约定）。</para>
    /// </summary>
    [Serializable]
    public class DescriptionIconConfig
    {
        public bool    IsVisible = true;
        public Vector2 Position  = new Vector2(150f, 363f);  // 默认基于 300×465 面板：横向居中，纵向 78%
        public Vector2 Size      = new Vector2(102f, 102f);  // ≈ 22% 面板高度

        public DescriptionIconConfig() { }

        public DescriptionIconConfig WithVisible(bool v)  { IsVisible = v;            return this; }
        public DescriptionIconConfig WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
        public DescriptionIconConfig WithSize(float w, float h)     { Size     = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h)); return this; }
    }

    /// <summary>
    /// 描述面板内「文本」子组件配置（名称 / 数量 / 详细描述）
    /// <para><see cref="Position"/> 为 rect 中心相对描述面板左下角的坐标。</para>
    /// </summary>
    [Serializable]
    public class DescriptionTextElementConfig
    {
        public bool       IsVisible = true;
        public Vector2    Position  = Vector2.zero;
        public Vector2    Size      = new Vector2(280f, 30f);
        public int        FontSize  = 14;
        public Color      TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public TextAnchor Alignment = TextAnchor.MiddleCenter;

        public DescriptionTextElementConfig() { }

        public DescriptionTextElementConfig WithVisible(bool v)        { IsVisible = v; return this; }
        public DescriptionTextElementConfig WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
        public DescriptionTextElementConfig WithSize(float w, float h)     { Size = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h)); return this; }
        public DescriptionTextElementConfig WithFontSize(int s)            { FontSize = Mathf.Max(1, s); return this; }
        public DescriptionTextElementConfig WithTextColor(Color c)         { TextColor = c; return this; }
        public DescriptionTextElementConfig WithAlignment(TextAnchor a)    { Alignment = a; return this; }
    }
}
