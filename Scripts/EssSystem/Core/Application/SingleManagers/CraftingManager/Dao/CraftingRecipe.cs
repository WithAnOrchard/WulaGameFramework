using System;

namespace EssSystem.Core.Application.SingleManagers.CraftingManager.Dao
{
    /// <summary>
    /// 配方 —— 输入材料 + 输出物品 + 工作台门槛 + 技能要求 + 蓝图关联。
    /// </summary>
    [Serializable]
    public class CraftingRecipe
    {
        /// <summary>唯一 Id（"recipe_iron_sword"）。</summary>
        public string Id;

        /// <summary>显示名。</summary>
        public string DisplayName;

        /// <summary>说明文本。</summary>
        public string Description;

        /// <summary>消耗材料表。</summary>
        public CraftIngredient[] Ingredients;

        /// <summary>输出物品（通常 1 个；多 entry 表示多产出 / 副产物）。</summary>
        public CraftOutput[] Outputs;

        /// <summary>不消耗的"催化剂"（如锤子 / 蒸馏器需要持有但不损耗）。</summary>
        public CraftIngredient[] CatalystKeep;

        /// <summary>需要的工作台 Id（""=手搓）。</summary>
        public string WorkstationId;

        /// <summary>等级（1~5），约束 WorkstationDefinition.Tier ≥ 此值。</summary>
        public int Tier = 1;

        /// <summary>基础制作时长（秒）。</summary>
        public float CraftSeconds = 5f;

        /// <summary>技能 / 属性门槛。</summary>
        public RecipeSkillRequirement Skill = new RecipeSkillRequirement();

        /// <summary>是否默认已学（false = 需消耗蓝图物品才能解锁）。</summary>
        public bool LearnedByDefault;

        /// <summary>关联的蓝图物品 Id（约定：blueprint_X → recipe_X）。</summary>
        public string BlueprintItemId;

        /// <summary>类别（"weapon" / "armor" / "consumable" / "tool" / ...）；用于 UI 分页与 CraftSkill 轴对应。</summary>
        public string CategoryId;
    }
}
