using UnityEngine;
using System.Collections.Generic;

namespace EssSystem.Core.Presentation.UIManager.Dao.CommonComponents
{
    /// <summary>
    /// 可滚动容器 DAO — 对应 <see cref="UIType.ScrollView"/>。
    /// <para>运行时由 <c>UIScrollViewEntity</c> 创建 ScrollRect + Viewport + Content 子层级。</para>
    /// <para>通过 <c>UIScrollViewEntity.ContentTransform</c> 向内容区动态添加子节点。</para>
    /// </summary>
    public class UIScrollViewComponent : UIComponent
    {
        /// <summary>内容区背景色（默认深黑）。</summary>
        public Color BackgroundColor { get; set; } = new Color(0.08f, 0.09f, 0.11f, 1f);

        /// <summary>内容区条目间距（px）。</summary>
        public float ItemSpacing { get; set; } = 2f;

        /// <summary>内容区内边距（px）。</summary>
        public int ContentPadding { get; set; } = 4;

        public float ScrollSensitivity { get; set; } = 30f;

        private readonly List<UIComponent> _contentChildren = new();

        public UIScrollViewComponent(string id, string name = null)
            : base(id, UIType.ScrollView, name) { }

        public UIScrollViewComponent SetPosition(float x, float y)
            => SetPosition<UIScrollViewComponent>(x, y);

        public UIScrollViewComponent SetSize(float w, float h)
            => SetSize<UIScrollViewComponent>(w, h);

        public UIScrollViewComponent SetBackgroundColor(Color c)
        {
            BackgroundColor = c;
            return this;
        }

        public UIScrollViewComponent SetItemSpacing(float spacing)
        {
            ItemSpacing = Mathf.Max(0f, spacing);
            return this;
        }

        public UIScrollViewComponent SetContentPadding(int padding)
        {
            ContentPadding = Mathf.Max(0, padding);
            return this;
        }

        public UIScrollViewComponent SetScrollSensitivity(float sensitivity)
        {
            ScrollSensitivity = Mathf.Max(1f, sensitivity);
            return this;
        }

        public UIScrollViewComponent AddContentChild(UIComponent child)
        {
            if (child != null) _contentChildren.Add(child);
            return this;
        }

        public IEnumerable<UIComponent> GetContentChildren()
        {
            return _contentChildren;
        }
    }
}
