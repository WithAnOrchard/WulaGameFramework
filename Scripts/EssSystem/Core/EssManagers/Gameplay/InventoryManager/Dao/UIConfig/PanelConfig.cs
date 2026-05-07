using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao
{
    /// <summary>
    /// 面板配置 - 定义背包面板的大小和位置参数
    /// </summary>
    [Serializable]
    public class PanelConfig
    {
        /// <summary>面板宽度</summary>
        public float PanelWidth = 400f;

        /// <summary>面板高度</summary>
        public float PanelHeight = 300f;

        /// <summary>面板位置（相对于父容器）</summary>
        public Vector2 PanelPosition = Vector2.zero;

        /// <summary>面板缩放</summary>
        public Vector2 PanelScale = Vector2.one;

        /// <summary>背包背景 Sprite ID</summary>
        public string BackgroundSpriteId;

        /// <summary>背景颜色</summary>
        public Color BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        /// <summary>反序列化用</summary>
        public PanelConfig() { }

        /// <summary>创建面板配置</summary>
        public PanelConfig(float width = 400f, float height = 300f)
        {
            PanelWidth = Mathf.Max(1f, width);
            PanelHeight = Mathf.Max(1f, height);
        }

        /// <summary>设置面板大小</summary>
        public PanelConfig WithPanelSize(float width, float height)
        {
            PanelWidth = Mathf.Max(1f, width);
            PanelHeight = Mathf.Max(1f, height);
            return this;
        }

        /// <summary>设置面板位置</summary>
        public PanelConfig WithPanelPosition(Vector2 position)
        {
            PanelPosition = position;
            return this;
        }

        /// <summary>设置面板位置</summary>
        public PanelConfig WithPanelPosition(float x, float y)
        {
            PanelPosition = new Vector2(x, y);
            return this;
        }

        /// <summary>设置面板缩放</summary>
        public PanelConfig WithPanelScale(Vector2 scale)
        {
            PanelScale = scale;
            return this;
        }

        /// <summary>设置面板缩放</summary>
        public PanelConfig WithPanelScale(float x, float y)
        {
            PanelScale = new Vector2(x, y);
            return this;
        }

        /// <summary>设置背景Sprite ID</summary>
        public PanelConfig WithBackgroundId(string spriteId)
        {
            BackgroundSpriteId = spriteId;
            return this;
        }

        /// <summary>设置背景颜色</summary>
        public PanelConfig WithBackgroundColor(Color color)
        {
            BackgroundColor = color;
            return this;
        }
    }
}
