using System;

namespace EssSystem.Core.Application.SingleManagers.CraftingManager.Dao
{
    /// <summary>
    /// 配方技能 / 属性门槛 —— 玩家需达到全部门槛才能制作。
    /// 与 <c>EntityManager.Capabilities.IStats</c> 协同（Int / Str 字段对应 PrimaryStat）。
    /// </summary>
    [Serializable]
    public class RecipeSkillRequirement
    {
        /// <summary>该配方品类（Smithing / Carpentry / ...）的 CraftSkill 等级最低值。</summary>
        public int CraftSkillMin;

        /// <summary>智力门槛（炼金 / 卷轴类常用）。</summary>
        public int IntelligenceMin;

        /// <summary>力量门槛（重型武器 / 装甲类常用）。</summary>
        public int StrengthMin;
    }
}
