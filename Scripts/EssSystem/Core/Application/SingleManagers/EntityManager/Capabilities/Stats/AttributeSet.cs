using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 属性数据载体 —— 持有 6 个 Primary 基值 + 修饰器列表 + 派生缓存。
    /// <para>
    /// <b>骨架阶段</b>：仅承载数据；最终求值（含 Modifier 合成）由
    /// <see cref="StatsComponent"/> 在读取时计算。
    /// </para>
    /// </summary>
    [Serializable]
    public class AttributeSet
    {
        // ─── Primary 基础值（不含 Modifier）──────────────────
        public int BaseStr = 10;
        public int BaseDex = 10;
        public int BaseCon = 10;
        public int BaseInt = 10;
        public int BaseWis = 10;
        public int BaseCha = 10;

        /// <summary>已挂的全部 Modifier（装备 / Buff / 状态）。</summary>
        public readonly List<StatModifier> Modifiers = new List<StatModifier>();

        public AttributeSet() { }

        public AttributeSet(int str, int dex, int con, int intl, int wis, int cha)
        {
            BaseStr = str; BaseDex = dex; BaseCon = con;
            BaseInt = intl; BaseWis = wis; BaseCha = cha;
        }

        /// <summary>读取 Primary 基础值（不应用 Modifier；公式计算时使用）。</summary>
        public int GetPrimaryRaw(PrimaryStat stat)
        {
            switch (stat)
            {
                case PrimaryStat.STR: return BaseStr;
                case PrimaryStat.DEX: return BaseDex;
                case PrimaryStat.CON: return BaseCon;
                case PrimaryStat.INT: return BaseInt;
                case PrimaryStat.WIS: return BaseWis;
                case PrimaryStat.CHA: return BaseCha;
                default: return 0;
            }
        }

        /// <summary>设置 Primary 基础值（升级 / 编辑器调试）。</summary>
        public void SetPrimaryRaw(PrimaryStat stat, int value)
        {
            switch (stat)
            {
                case PrimaryStat.STR: BaseStr = value; break;
                case PrimaryStat.DEX: BaseDex = value; break;
                case PrimaryStat.CON: BaseCon = value; break;
                case PrimaryStat.INT: BaseInt = value; break;
                case PrimaryStat.WIS: BaseWis = value; break;
                case PrimaryStat.CHA: BaseCha = value; break;
            }
        }
    }
}
