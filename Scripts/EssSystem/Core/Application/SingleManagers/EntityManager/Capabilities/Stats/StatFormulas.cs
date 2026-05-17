using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 派生属性集中公式表 —— 调参一处改全局，业务侧切勿散落硬编码。
    /// <para>
    /// 默认参考值（设计 v1，#2 ToDo 条目）：
    /// <list type="bullet">
    /// <item>CarryCapacity = 10 + STR * 5</item>
    /// <item>MaxHp = 50 + CON * 10</item>
    /// <item>MaxMp = 20 + INT * 8</item>
    /// <item>HpRegen = CON * 0.05</item>
    /// <item>MpRegen = WIS * 0.05</item>
    /// <item>AttackPower = STR * 1.5</item>
    /// <item>AttackSpeed = 1.0 + DEX * 0.02</item>
    /// <item>DodgeChance = clamp(DEX * 0.005, 0, 0.5)</item>
    /// <item>CritChance = clamp(DEX * 0.003, 0, 1.0)</item>
    /// <item>ViewRange = 8 + WIS * 0.3</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class StatFormulas
    {
        /// <summary>
        /// 计算指定派生属性的基础值（不含修饰器）。
        /// 调用方在拿到结果后再叠加 <see cref="StatModifier"/>。
        /// </summary>
        public static float ComputeDerived(DerivedStat stat, AttributeSet primary)
        {
            if (primary == null) return 0f;
            int str = primary.GetPrimaryRaw(PrimaryStat.STR);
            int dex = primary.GetPrimaryRaw(PrimaryStat.DEX);
            int con = primary.GetPrimaryRaw(PrimaryStat.CON);
            int intl = primary.GetPrimaryRaw(PrimaryStat.INT);
            int wis = primary.GetPrimaryRaw(PrimaryStat.WIS);

            switch (stat)
            {
                case DerivedStat.CarryCapacity: return 10f + str * 5f;
                case DerivedStat.MaxHp:         return 50f + con * 10f;
                case DerivedStat.MaxMp:         return 20f + intl * 8f;
                case DerivedStat.HpRegen:       return con * 0.05f;
                case DerivedStat.MpRegen:       return wis * 0.05f;
                case DerivedStat.AttackPower:   return str * 1.5f;
                case DerivedStat.AttackSpeed:   return 1.0f + dex * 0.02f;
                case DerivedStat.DodgeChance:   return Mathf.Clamp(dex * 0.005f, 0f, 0.5f);
                case DerivedStat.CritChance:    return Mathf.Clamp(dex * 0.003f, 0f, 1.0f);
                case DerivedStat.ViewRange:     return 8f + wis * 0.3f;
                default:                        return 0f;
            }
        }
    }
}
