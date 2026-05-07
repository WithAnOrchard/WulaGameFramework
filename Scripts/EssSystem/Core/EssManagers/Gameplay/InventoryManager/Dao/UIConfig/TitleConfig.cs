using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao
{
    /// <summary>
    /// 容器标题配置 — 在主 Panel 顶部显示一行容器名称（默认使用 InventoryConfig.DisplayName）
    /// <para>
    /// 作为主 Panel 的子节点挂载，<see cref="Position"/> 为相对主面板左下角的位置（rect 中心点）。
    /// </para>
    /// </summary>
    [Serializable]
    public class TitleConfig
    {
        #region Fields

        /// <summary>是否可见</summary>
        public bool IsVisible = true;

        /// <summary>相对主面板左下角的位置（指向 rect 中心点）</summary>
        public Vector2 Position = new Vector2(340f, 530f);

        /// <summary>标题文本框尺寸</summary>
        public Vector2 Size = new Vector2(400f, 40f);

        /// <summary>字体大小</summary>
        public int FontSize = 22;

        /// <summary>文本颜色</summary>
        public Color TextColor = new Color(1f, 0.92f, 0.7f, 1f);

        /// <summary>对齐方式</summary>
        public TextAnchor Alignment = TextAnchor.MiddleCenter;

        /// <summary>自定义文本（留空则使用 InventoryConfig.DisplayName）</summary>
        public string CustomText;

        #endregion

        #region Constructors

        /// <summary>反序列化用</summary>
        public TitleConfig() { }

        public TitleConfig(float width, float height)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        #endregion

        #region Chain API

        public TitleConfig WithVisible(bool visible) { IsVisible = visible; return this; }

        public TitleConfig WithPosition(float x, float y) { Position = new Vector2(x, y); return this; }

        public TitleConfig WithPosition(Vector2 pos) { Position = pos; return this; }

        public TitleConfig WithSize(float width, float height)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            return this;
        }

        public TitleConfig WithFontSize(int size) { FontSize = Mathf.Max(1, size); return this; }

        public TitleConfig WithTextColor(Color color) { TextColor = color; return this; }

        public TitleConfig WithAlignment(TextAnchor anchor) { Alignment = anchor; return this; }

        public TitleConfig WithCustomText(string text) { CustomText = text; return this; }

        #endregion
    }
}
