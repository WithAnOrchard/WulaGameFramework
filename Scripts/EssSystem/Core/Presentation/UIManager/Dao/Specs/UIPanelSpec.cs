using System;
using UnityEngine;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;

namespace EssSystem.Core.Presentation.UIManager.Dao.Specs
{
    /// <summary>
    /// 通用「面板」声明式配置 — 持久化用纯数据 + With* 链式 API。
    /// <para>业务模块（Inventory/Dialogue/…）持有此类型描述「想要什么样的面板」，
    /// 调 <see cref="CreateComponent"/> 或 <see cref="ApplyTo"/> 一键转成运行时 <see cref="UIPanelComponent"/>。</para>
    /// </summary>
    [Serializable]
    public class UIPanelSpec
    {
        /// <summary>面板尺寸（width × height）</summary>
        public Vector2 Size = new Vector2(400f, 300f);

        /// <summary>面板位置（含义由调用方决定：Canvas 世界坐标 / 父面板内偏移）</summary>
        public Vector2 Position = Vector2.zero;

        /// <summary>面板缩放</summary>
        public Vector2 Scale = Vector2.one;

        /// <summary>背景 Sprite ID（经 ResourceManager 解析）</summary>
        public string BackgroundSpriteId;

        /// <summary>背景颜色（有 Sprite 时作为色套，无 Sprite 时作为纯色块）</summary>
        public Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        public UIPanelSpec() { }

        public UIPanelSpec(float width, float height)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        public UIPanelSpec WithSize(float width, float height)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            return this;
        }

        public UIPanelSpec WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }
        public UIPanelSpec WithPosition(Vector2 pos)      { Position = pos; return this; }
        public UIPanelSpec WithScale(float x, float y)    { Scale    = new Vector2(x, y); return this; }
        public UIPanelSpec WithScale(Vector2 scale)       { Scale    = scale; return this; }
        public UIPanelSpec WithBackgroundId(string id)    { BackgroundSpriteId = id; return this; }
        public UIPanelSpec WithBackgroundColor(Color c)   { BackgroundColor = c; return this; }

        /// <summary>把当前 Spec 字段应用到一个现成的 <see cref="UIPanelComponent"/>。</summary>
        public UIPanelComponent ApplyTo(UIPanelComponent panel)
        {
            if (panel == null) return null;
            panel.SetPosition(Position.x, Position.y)
                 .SetSize(Size.x, Size.y)
                 .SetScale(Scale.x, Scale.y)
                 .SetBackgroundSpriteId(BackgroundSpriteId)
                 .SetBackgroundColor(BackgroundColor);
            return panel;
        }

        /// <summary>用 Spec 直接 new 一个 <see cref="UIPanelComponent"/>。</summary>
        public UIPanelComponent CreateComponent(string id, string name = null)
        {
            return ApplyTo(new UIPanelComponent(id, name));
        }
    }
}
