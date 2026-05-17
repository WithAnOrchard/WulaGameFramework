namespace EssSystem.Core.Application.SingleManagers.CraftingManager.Dao
{
    /// <summary>
    /// 制作品质 —— 影响输出 InventoryItem 上的运行时 Modifier 加成。
    /// 由 <c>StatFormulas</c> 风格的品质判定 roll 落入分布区间确定。
    /// </summary>
    public enum CraftQuality
    {
        /// <summary>粗糙（-10% 装备属性）。</summary>
        Crude = 0,
        /// <summary>普通（无加成）。</summary>
        Common = 1,
        /// <summary>精良（+10%）。</summary>
        Fine = 2,
        /// <summary>卓越（+25%）。</summary>
        Superior = 3,
        /// <summary>大师（+50% + 1 词缀）。</summary>
        Masterwork = 4,
        /// <summary>传奇（+100% + 2 词缀）。</summary>
        Legendary = 5,
    }
}
