namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 派生属性（Derived Stats）— 由 <see cref="PrimaryStat"/> 经
    /// <see cref="StatFormulas"/> 公式自动算出，受 <see cref="StatModifier"/> 进一步修饰。
    /// </summary>
    public enum DerivedStat
    {
        /// <summary>负重上限（kg）。供 InventoryManager 计算 MaxWeight。</summary>
        CarryCapacity = 0,
        /// <summary>最大生命值。</summary>
        MaxHp = 1,
        /// <summary>最大法力值。</summary>
        MaxMp = 2,
        /// <summary>每秒生命回复。</summary>
        HpRegen = 3,
        /// <summary>每秒法力回复。</summary>
        MpRegen = 4,
        /// <summary>近战 / 物理攻击力（不含武器加成）。</summary>
        AttackPower = 5,
        /// <summary>攻击速度倍率（基础 1.0）。</summary>
        AttackSpeed = 6,
        /// <summary>闪避概率（0~1，由公式 cap）。</summary>
        DodgeChance = 7,
        /// <summary>暴击概率（0~1）。</summary>
        CritChance = 8,
        /// <summary>视野半径（tile 数）。</summary>
        ViewRange = 9,
    }
}
