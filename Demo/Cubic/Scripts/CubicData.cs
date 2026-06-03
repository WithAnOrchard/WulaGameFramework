using UnityEngine;

namespace Demo.Cubic
{
    /// <summary>
    /// 角色职业枚举 - 颜色对应职业
    /// </summary>
    public enum CubicCharacterClass
    {
        Warrior = 1,      // 战士 - 铁灰色
        Mage = 2,         // 魔法师 - 紫色
        Archer = 3,       // 弓箭手 - 绿色
        Paladin = 4,      // 圣骑士 - 蓝色
        Assassin = 5,      // 刺客 - 黑色
        Engineer = 6,      // 工程师 - 橙色
        Necromancer = 7,  // 死灵法师 - 深紫色
        Cleric = 8        // 圣职者 - 金色
    }

    /// <summary>
    /// 职业颜色配置
    /// </summary>
    public static class CubicClassColors
    {
        public static Color GetColor(CubicCharacterClass jobClass)
        {
            return jobClass switch
            {
                CubicCharacterClass.Warrior => new Color(0.44f, 0.5f, 0.56f),      // #708090 铁灰色
                CubicCharacterClass.Mage => new Color(0.6f, 0.2f, 0.8f),           // #9932CC 紫色
                CubicCharacterClass.Archer => new Color(0.2f, 0.8f, 0.2f),         // #32CD32 绿色
                CubicCharacterClass.Paladin => new Color(0.25f, 0.41f, 0.88f),    // #4169E1 蓝色
                CubicCharacterClass.Assassin => new Color(0.11f, 0.11f, 0.11f),    // #1C1C1C 黑色
                CubicCharacterClass.Engineer => new Color(1f, 0.55f, 0f),          // #FF8C00 橙色
                CubicCharacterClass.Necromancer => new Color(0.29f, 0f, 0.51f),    // #4B0082 深紫色
                CubicCharacterClass.Cleric => new Color(1f, 0.84f, 0f),           // #FFD700 金色
                _ => Color.white
            };
        }

        public static string GetClassName(CubicCharacterClass jobClass)
        {
            return jobClass switch
            {
                CubicCharacterClass.Warrior => "战士",
                CubicCharacterClass.Mage => "魔法师",
                CubicCharacterClass.Archer => "弓箭手",
                CubicCharacterClass.Paladin => "圣骑士",
                CubicCharacterClass.Assassin => "刺客",
                CubicCharacterClass.Engineer => "工程师",
                CubicCharacterClass.Necromancer => "死灵法师",
                CubicCharacterClass.Cleric => "圣职者",
                _ => "未知"
            };
        }
    }
}
