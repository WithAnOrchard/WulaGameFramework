using System;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao.UIConfig
{
    /// <summary>
    /// 按钮配置 - 定义关闭按钮的参数（参照Adjustable）
    /// </summary>
    [Serializable]
    public class ButtonConfig
    {
        /// <summary>按钮位置（相对于面板）</summary>
        public Vector2 Position = new Vector2(380f, 280f);

        /// <summary>按钮大小</summary>
        public Vector2 Size = new Vector2(32f, 32f);

        /// <summary>按钮缩放</summary>
        public Vector2 Scale = Vector2.one;

        /// <summary>按钮文本</summary>
        public string ButtonText = "×";

        /// <summary>按钮背景 Sprite ID</summary>
        public string ButtonSpriteId;

        /// <summary>按钮颜色</summary>
        public Color ButtonColor = new Color(1f, 0.3f, 0.3f, 1f);

        /// <summary>按钮是否可见</summary>
        public bool IsVisible = true;

        /// <summary>按钮是否可交互</summary>
        public bool IsInteractable = true;

        /// <summary>反序列化用</summary>
        public ButtonConfig() { }

        /// <summary>创建按钮配置</summary>
        public ButtonConfig(float x = 380f, float y = 280f, float width = 32f, float height = 32f)
        {
            Position = new Vector2(x, y);
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        /// <summary>设置按钮位置</summary>
        public ButtonConfig WithPosition(Vector2 position)
        {
            Position = position;
            return this;
        }

        /// <summary>设置按钮位置</summary>
        public ButtonConfig WithPosition(float x, float y)
        {
            Position = new Vector2(x, y);
            return this;
        }

        /// <summary>设置按钮大小</summary>
        public ButtonConfig WithSize(Vector2 size)
        {
            Size = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
            return this;
        }

        /// <summary>设置按钮大小</summary>
        public ButtonConfig WithSize(float width, float height)
        {
            Size = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
            return this;
        }

        /// <summary>设置按钮缩放</summary>
        public ButtonConfig WithScale(Vector2 scale)
        {
            Scale = scale;
            return this;
        }

        /// <summary>设置按钮缩放</summary>
        public ButtonConfig WithScale(float x, float y)
        {
            Scale = new Vector2(x, y);
            return this;
        }

        /// <summary>设置按钮文本</summary>
        public ButtonConfig WithText(string text)
        {
            ButtonText = text ?? "×";
            return this;
        }

        /// <summary>设置按钮Sprite ID</summary>
        public ButtonConfig WithSpriteId(string spriteId)
        {
            ButtonSpriteId = spriteId;
            return this;
        }

        /// <summary>设置按钮颜色</summary>
        public ButtonConfig WithColor(Color color)
        {
            ButtonColor = color;
            return this;
        }

        /// <summary>设置按钮可见性</summary>
        public ButtonConfig WithVisible(bool visible)
        {
            IsVisible = visible;
            return this;
        }

        /// <summary>设置按钮可交互性</summary>
        public ButtonConfig WithInteractable(bool interactable)
        {
            IsInteractable = interactable;
            return this;
        }
    }
}
