using UnityEngine;
using System.Collections.Generic;

namespace Demo.Cubic.Skill
{
    /// <summary>
    /// Cubic 技能注册表
    /// 管理所有技能的注册和查询
    /// 基于 SkillManager 的事件系统
    /// </summary>
    public static class CubicSkillRegistry
    {
        private static bool _initialized = false;

        /// <summary>
        /// 所有注册的技能
        /// </summary>
        public static readonly Dictionary<string, SkillInfo> Skills = new Dictionary<string, SkillInfo>();

        /// <summary>
        /// 技能信息
        /// </summary>
        public class SkillInfo
        {
            public string Id;
            public string DisplayName;
            public string Description;
            public float ManaCost;
            public float Cooldown;
            public float CastTime;
            public float RecoveryTime;
            public CubicCharacterClass JobClass;
            public Color VFXColor;
            public string VFXId;
        }

        /// <summary>
        /// 初始化技能注册表
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            RegisterDefaultSkills();
            Debug.Log($"[CubicSkillRegistry] 技能系统初始化完成，共 {Skills.Count} 个技能");
        }

        /// <summary>
        /// 注册默认技能
        /// </summary>
        private static void RegisterDefaultSkills()
        {
            RegisterSkill("cubic_warrior_slash", "横扫斩", "对前方敌人造成伤害", 5, 4, 0, 0.2f, CubicCharacterClass.Warrior, Color.red, "warrior_slash");
            RegisterSkill("cubic_warrior_war_cry", "战吼", "提升攻击力", 15, 15, 0.5f, 0.5f, CubicCharacterClass.Warrior, Color.yellow, "warrior_shout");
            RegisterSkill("cubic_warrior_whirlwind", "旋风斩", "持续旋转攻击周围敌人", 20, 12, 0.3f, 0.5f, CubicCharacterClass.Warrior, Color.red, "warrior_whirlwind");

            RegisterSkill("cubic_mage_fireball", "火球术", "发射火球攻击敌人", 15, 6, 0.4f, 0.3f, CubicCharacterClass.Mage, new Color(1, 0.5f, 0), "mage_fireball");
            RegisterSkill("cubic_mage_frost_nova", "冰霜新星", "冰冻周围敌人", 25, 10, 0.5f, 0.4f, CubicCharacterClass.Mage, new Color(0.5f, 0.8f, 1), "mage_frost_nova");
            RegisterSkill("cubic_mage_chain_lightning", "闪电链", "链式攻击多个敌人", 20, 8, 0.3f, 0.3f, CubicCharacterClass.Mage, new Color(0.6f, 0.2f, 1), "mage_lightning");

            RegisterSkill("cubic_archer_multishot", "多重射击", "扇形射出多支箭", 12, 5, 0.3f, 0.3f, CubicCharacterClass.Archer, Color.green, "archer_arrow");
            RegisterSkill("cubic_archer_pierce", "穿刺箭", "穿透路径上所有敌人", 18, 8, 0.4f, 0.4f, CubicCharacterClass.Archer, Color.green, "archer_pierce");
            RegisterSkill("cubic_archer_dash", "疾风步", "快速位移", 10, 6, 0.1f, 0.2f, CubicCharacterClass.Archer, Color.green, "archer_dash");

            RegisterSkill("cubic_paladin_holy_slash", "圣光斩", "圣光攻击并治疗自己", 8, 4, 0.3f, 0.3f, CubicCharacterClass.Paladin, Color.yellow, "paladin_holy");
            RegisterSkill("cubic_paladin_hammer", "正义之锤", "锤击并眩晕敌人", 15, 8, 0.5f, 0.4f, CubicCharacterClass.Paladin, Color.yellow, "paladin_hammer");
            RegisterSkill("cubic_paladin_devotion", "奉献", "光环治疗周围队友", 25, 15, 0.6f, 0.5f, CubicCharacterClass.Paladin, Color.yellow, "paladin_devotion");
        }

        /// <summary>
        /// 注册技能
        /// </summary>
        private static void RegisterSkill(
            string id, string displayName, string description,
            float manaCost, float cooldown, float castTime, float recoveryTime,
            CubicCharacterClass jobClass, Color vfxColor, string vfxId)
        {
            var skill = new SkillInfo
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                ManaCost = manaCost,
                Cooldown = cooldown,
                CastTime = castTime,
                RecoveryTime = recoveryTime,
                JobClass = jobClass,
                VFXColor = vfxColor,
                VFXId = vfxId
            };

            Skills[id] = skill;
        }

        /// <summary>
        /// 获取技能信息
        /// </summary>
        public static SkillInfo GetSkill(string skillId)
        {
            return Skills.TryGetValue(skillId, out var skill) ? skill : null;
        }

        /// <summary>
        /// 获取职业的所有技能
        /// </summary>
        public static List<SkillInfo> GetClassSkills(CubicCharacterClass jobClass)
        {
            var result = new List<SkillInfo>();
            foreach (var kvp in Skills)
            {
                if (kvp.Value.JobClass == jobClass)
                {
                    result.Add(kvp.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// 为实体赋予职业默认技能
        /// </summary>
        public static void GrantDefaultSkillsToEntity(Entity.CubicEntity entity, CubicCharacterClass jobClass)
        {
            if (entity == null) return;

            var classSkills = GetClassSkills(jobClass);
            foreach (var skill in classSkills)
            {
                entity.AddSkill(skill.Id);
            }
        }
    }
}
