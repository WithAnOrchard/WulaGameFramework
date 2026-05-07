using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.InventoryManager.Dao
{
    /// <summary>
    /// 槽位配置 - 定义背包槽位的UI参数
    /// </summary>
    [Serializable]
    public class SlotConfig
    {
        /// <summary>格子宽度</summary>
        public float SlotWidth = 64f;

        /// <summary>格子高度</summary>
        public float SlotHeight = 64f;

        /// <summary>格子水平间距</summary>
        public float SlotSpacingX = 4f;

        /// <summary>格子垂直间距</summary>
        public float SlotSpacingY = 4f;

        /// <summary>每行格子数（用于换行计算）</summary>
        public int SlotsPerRow = 5;

        /// <summary>格子背景 Sprite ID</summary>
        public string SlotBackgroundSpriteId;

        /// <summary>起始 X 偏移（相对于容器左上角）</summary>
        public float StartOffsetX = 10f;

        /// <summary>起始 Y 偏移（相对于容器左上角）</summary>
        public float StartOffsetY = -10f;

        /// <summary>反序列化用</summary>
        public SlotConfig() { }

        /// <summary>创建槽位配置</summary>
        public SlotConfig(float width = 64f, float height = 64f, int slotsPerRow = 5)
        {
            SlotWidth = Mathf.Max(1f, width);
            SlotHeight = Mathf.Max(1f, height);
            SlotsPerRow = Mathf.Max(1, slotsPerRow);
        }

        /// <summary>设置槽位大小</summary>
        public SlotConfig WithSlotSize(float width, float height)
        {
            SlotWidth = Mathf.Max(1f, width);
            SlotHeight = Mathf.Max(1f, height);
            return this;
        }

        /// <summary>设置槽位间距</summary>
        public SlotConfig WithSlotSpacing(float x, float y)
        {
            SlotSpacingX = x;
            SlotSpacingY = y;
            return this;
        }

        /// <summary>设置每行格子数</summary>
        public SlotConfig WithSlotsPerRow(int count)
        {
            SlotsPerRow = Mathf.Max(1, count);
            return this;
        }

        /// <summary>设置起始偏移</summary>
        public SlotConfig WithStartOffset(float x, float y)
        {
            StartOffsetX = x;
            StartOffsetY = y;
            return this;
        }

        /// <summary>设置槽位背景Sprite ID</summary>
        public SlotConfig WithSlotBackgroundId(string spriteId)
        {
            SlotBackgroundSpriteId = spriteId;
            return this;
        }
    }
}
