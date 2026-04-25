using System;
using UnityEngine;

namespace EssSystem.EssManager.InventoryManager.Dao
{
    /// <summary>
    /// 背包容器配置 — 定义不同类型容器的 UI 显示参数
    /// <para>
    /// 用于配置如 PlayerBackPack、Chest 等不同容器类型的 UI 布局、背景、偏移等参数。
    /// </para>
    /// </summary>
    [Serializable]
    public class InventoryConfig
    {
        #region Fields

        /// <summary>容器类型 ID（如 "PlayerBackPack", "Chest"）</summary>
        public string ConfigId;

        /// <summary>容器显示名称</summary>
        public string DisplayName;

        /// <summary>UI 页数</summary>
        public int PageCount = 1;

        /// <summary>每页格子数</summary>
        public int SlotsPerPage = 20;

        /// <summary>背包背景 Sprite</summary>
        public Sprite BackgroundSprite;

        /// <summary>格子背景 Sprite</summary>
        public Sprite SlotBackgroundSprite;

        /// <summary>格子宽度</summary>
        public float SlotWidth = 64f;

        /// <summary>格子高度</summary>
        public float SlotHeight = 64f;

        /// <summary>格子水平间距</summary>
        public float SlotSpacingX = 4f;

        /// <summary>格子垂直间距</summary>
        public float SlotSpacingY = 4f;

        /// <summary>起始 X 偏移（相对于容器左上角）</summary>
        public float StartOffsetX = 10f;

        /// <summary>起始 Y 偏移（相对于容器左上角）</summary>
        public float StartOffsetY = -10f;

        /// <summary>每行格子数（用于换行计算）</summary>
        public int SlotsPerRow = 5;

        #endregion

        #region Constructors

        /// <summary>反序列化用</summary>
        public InventoryConfig() { }

        /// <summary>创建配置</summary>
        public InventoryConfig(string configId, string displayName, int pageCount = 1, int slotsPerPage = 20)
        {
            ConfigId = configId;
            DisplayName = displayName ?? configId;
            PageCount = Mathf.Max(1, pageCount);
            SlotsPerPage = Mathf.Max(1, slotsPerPage);
        }

        #endregion

        #region Chain API

        public InventoryConfig WithPageCount(int count)
        {
            PageCount = Mathf.Max(1, count);
            return this;
        }

        public InventoryConfig WithSlotsPerPage(int count)
        {
            SlotsPerPage = Mathf.Max(1, count);
            return this;
        }

        public InventoryConfig WithBackground(Sprite sprite)
        {
            BackgroundSprite = sprite;
            return this;
        }

        public InventoryConfig WithSlotBackground(Sprite sprite)
        {
            SlotBackgroundSprite = sprite;
            return this;
        }

        public InventoryConfig WithSlotSize(float width, float height)
        {
            SlotWidth = Mathf.Max(1f, width);
            SlotHeight = Mathf.Max(1f, height);
            return this;
        }

        public InventoryConfig WithSlotSpacing(float x, float y)
        {
            SlotSpacingX = x;
            SlotSpacingY = y;
            return this;
        }

        public InventoryConfig WithStartOffset(float x, float y)
        {
            StartOffsetX = x;
            StartOffsetY = y;
            return this;
        }

        public InventoryConfig WithSlotsPerRow(int count)
        {
            SlotsPerRow = Mathf.Max(1, count);
            return this;
        }

        #endregion

        #region Calculated Properties

        /// <summary>总格子数</summary>
        public int TotalSlots => PageCount * SlotsPerPage;

        /// <summary>获取指定页的格子索引范围（包含）</summary>
        public (int startIndex, int endIndex) GetPageRange(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= PageCount)
                return (0, 0);

            int startIndex = pageIndex * SlotsPerPage;
            int endIndex = startIndex + SlotsPerPage - 1;
            return (startIndex, endIndex);
        }

        /// <summary>根据格子索引计算 UI 位置</summary>
        public Vector2 CalculateSlotPosition(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= TotalSlots)
                return Vector2.zero;

            int localIndex = slotIndex % SlotsPerPage;
            int row = localIndex / SlotsPerRow;
            int col = localIndex % SlotsPerRow;

            float x = StartOffsetX + col * (SlotWidth + SlotSpacingX);
            float y = StartOffsetY - row * (SlotHeight + SlotSpacingY);

            return new Vector2(x, y);
        }

        #endregion

        public override string ToString() =>
            $"InventoryConfig[{ConfigId} {DisplayName} {PageCount}页x{SlotsPerPage}格]";
    }
}
