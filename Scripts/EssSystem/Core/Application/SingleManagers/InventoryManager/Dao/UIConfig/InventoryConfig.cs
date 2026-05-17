using System;
using EssSystem.Core.Presentation.UIManager.Dao.Specs;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao
{
    /// <summary>
    /// 背包容器配置 — 定义不同类型容器的 UI 显示参数
    /// <para>
    /// 用于配置如 PlayerBackPack、Chest 等不同容器类型的 UI 布局、背景、偏移等参数。<br/>
    /// 通用部分（面板/按钮/标题）走 <c>UIManager/Dao/Specs/*</c>，背包独有的（槽位网格、描述子面板）继续保留本地 Config。
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

        /// <summary>槽位配置（背包独有：网格布局 + 槽位背景）</summary>
        public SlotConfig SlotConfig = new SlotConfig();

        /// <summary>面板配置（通用 <see cref="UIPanelSpec"/>）</summary>
        public UIPanelSpec PanelConfig = new UIPanelSpec();

        /// <summary>关闭按钮配置（通用 <see cref="UIButtonSpec"/>）</summary>
        public UIButtonSpec CloseButtonConfig = new UIButtonSpec();

        /// <summary>是否在主面板上额外显示物品描述子面板（点击 slot 后填充当前物品的 Description）</summary>
        public bool ShowDescription = false;

        /// <summary>描述面板配置（背包独有复合 Config：Panel + 3 个文本子组件 + 图标）</summary>
        public DescriptionPanelConfig DescriptionPanelConfig = new DescriptionPanelConfig();

        /// <summary>是否显示容器标题（默认使用 <see cref="DisplayName"/>；可在 <see cref="TitleConfig"/>.Text 覆盖）</summary>
        public bool ShowTitle = true;

        /// <summary>标题配置（通用 <see cref="UITextSpec"/>，Text 字段非空时覆盖 DisplayName）</summary>
        public UITextSpec TitleConfig = new UITextSpec();

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

        public InventoryConfig WithPanelConfig(UIPanelSpec panel)
        {
            PanelConfig = panel ?? new UIPanelSpec();
            return this;
        }

        public InventoryConfig WithCloseButtonConfig(UIButtonSpec button)
        {
            CloseButtonConfig = button ?? new UIButtonSpec();
            return this;
        }

        public InventoryConfig WithShowDescription(bool show)
        {
            ShowDescription = show;
            return this;
        }

        public InventoryConfig WithDescriptionPanelConfig(DescriptionPanelConfig config)
        {
            DescriptionPanelConfig = config ?? new DescriptionPanelConfig();
            return this;
        }

        public InventoryConfig WithShowTitle(bool show)
        {
            ShowTitle = show;
            return this;
        }

        public InventoryConfig WithTitleConfig(UITextSpec title)
        {
            TitleConfig = title ?? new UITextSpec();
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
