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

        /// <summary>槽位配置</summary>
        public SlotConfig SlotConfig = new SlotConfig();

        /// <summary>面板配置</summary>
        public PanelConfig PanelConfig = new PanelConfig();

        /// <summary>关闭按钮配置</summary>
        public ButtonConfig CloseButtonConfig = new ButtonConfig();

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

        public InventoryConfig WithSlotConfig(SlotConfig slotConfig)
        {
            SlotConfig = slotConfig ?? new SlotConfig();
            return this;
        }

        public InventoryConfig WithPanelConfig(PanelConfig panelConfig)
        {
            PanelConfig = panelConfig ?? new PanelConfig();
            return this;
        }

        public InventoryConfig WithCloseButtonConfig(ButtonConfig buttonConfig)
        {
            CloseButtonConfig = buttonConfig ?? new ButtonConfig();
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

        #endregion

        public override string ToString() =>
            $"InventoryConfig[{ConfigId} {DisplayName} {PageCount}页x{SlotsPerPage}格]";
    }
}
