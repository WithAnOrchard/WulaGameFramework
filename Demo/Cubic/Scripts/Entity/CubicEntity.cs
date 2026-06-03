using UnityEngine;
using System.Collections.Generic;
using Demo.Cubic.Skill;
using Demo.Cubic.VFX;

namespace Demo.Cubic.Entity
{
    /// <summary>
    /// Cubic 实体基类
    /// 所有角色和敌人共享的基础组件
    /// </summary>
    public class CubicEntity : MonoBehaviour
    {
        [Header("基础属性")]
        public CubicCharacterClass JobClass = CubicCharacterClass.Warrior;
        
        [Header("战斗属性")]
        public float MaxHP = 100f;
        public float CurrentHP = 100f;
        public float MaxMP = 50f;
        public float CurrentMP = 50f;
        public float PhysicalAttack = 10f;
        public float MagicAttack = 5f;
        public float Defense = 5f;

        [Header("状态")]
        public bool IsDead => CurrentHP <= 0;
        public bool IsInvincible { get; private set; }
        public bool IsCasting { get; private set; }

        protected Rigidbody2D _rigidbody;
        protected SpriteRenderer _renderer;

        [Header("技能栏")]
        [SerializeField] protected List<string> _skillIds = new List<string>();

        protected float _lastSkillTime = 0f;
        protected const float SKILL_COOLDOWN = 0.5f;

        public virtual void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _renderer = GetComponent<SpriteRenderer>();
            
            ApplyJobColor();
        }

        /// <summary>
        /// 应用职业颜色
        /// </summary>
        public void ApplyJobColor()
        {
            if (_renderer != null)
            {
                _renderer.color = CubicClassColors.GetColor(JobClass);
            }
        }

        /// <summary>
        /// 设置职业
        /// </summary>
        public void SetJobClass(CubicCharacterClass jobClass)
        {
            JobClass = jobClass;
            ApplyJobColor();
            
            var stats = CubicJobStats.GetStats(jobClass);
            MaxHP = stats.MaxHP;
            CurrentHP = MaxHP;
            MaxMP = stats.MaxMP;
            CurrentMP = MaxMP;
            PhysicalAttack = stats.PhysicalAttack;
            MagicAttack = stats.MagicAttack;
            Defense = stats.Defense;
        }

        /// <summary>
        /// 移动
        /// </summary>
        public virtual void Move(float horizontalSpeed)
        {
            if (_rigidbody != null && !IsCasting)
            {
                _rigidbody.linearVelocity = new Vector2(horizontalSpeed, _rigidbody.linearVelocity.y);
            }
        }

        /// <summary>
        /// 造成伤害
        /// </summary>
        public virtual void TakeDamage(float damage, Vector3 fromPosition)
        {
            if (IsInvincible || IsDead) return;

            float actualDamage = Mathf.Max(0, damage - Defense * 0.5f);
            CurrentHP -= actualDamage;

            Debug.Log($"[{JobClass}] 受到 {actualDamage} 点伤害，剩余 HP: {CurrentHP}/{MaxHP}");

            CubicVFXManager.PlayScreenFlash(CubicVFXManager.ScreenFlashType.Damage);

            if (IsDead)
            {
                OnDeath();
            }
        }

        /// <summary>
        /// 治疗
        /// </summary>
        public virtual void Heal(float amount)
        {
            if (IsDead) return;

            float actualHeal = Mathf.Min(amount, MaxHP - CurrentHP);
            CurrentHP += actualHeal;

            Debug.Log($"[{JobClass}] 恢复 {actualHeal} 点 HP，当前: {CurrentHP}/{MaxHP}");

            CubicVFXManager.PlayScreenFlash(CubicVFXManager.ScreenFlashType.Heal);
        }

        /// <summary>
        /// 消耗魔法值
        /// </summary>
        public bool ConsumeMP(float amount)
        {
            if (CurrentMP >= amount)
            {
                CurrentMP -= amount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 恢复魔法值
        /// </summary>
        public void RestoreMP(float amount)
        {
            CurrentMP = Mathf.Min(CurrentMP + amount, MaxMP);
        }

        /// <summary>
        /// 播放技能
        /// </summary>
        public virtual void CastSkill(string skillId)
        {
            if (IsDead || IsCasting) return;

            var skill = CubicSkillRegistry.GetSkill(skillId);
            if (skill == null)
            {
                Debug.LogWarning($"[CubicEntity] 技能不存在: {skillId}");
                return;
            }

            if (!ConsumeMP(skill.ManaCost))
            {
                Debug.Log($"[{JobClass}] 魔法值不足，无法释放技能: {skill.DisplayName}");
                return;
            }

            if (Time.time - _lastSkillTime < SKILL_COOLDOWN)
            {
                return;
            }

            IsCasting = true;
            _lastSkillTime = Time.time;

            Debug.Log($"[{JobClass}] 释放技能: {skill.DisplayName}");

            CubicVFXManager.PlaySkillVFX($"{JobClass.ToString().ToLower()}_skill", transform.position);

            Invoke(nameof(FinishCasting), skill.CastTime + skill.RecoveryTime);
        }

        /// <summary>
        /// 完成施法
        /// </summary>
        protected virtual void FinishCasting()
        {
            IsCasting = false;
        }

        /// <summary>
        /// 获取技能栏中的技能ID
        /// </summary>
        public string GetSkillId(int slot)
        {
            if (slot >= 0 && slot < _skillIds.Count)
            {
                return _skillIds[slot];
            }
            return null;
        }

        /// <summary>
        /// 添加技能到技能栏
        /// </summary>
        public void AddSkill(string skillId)
        {
            if (!_skillIds.Contains(skillId))
            {
                _skillIds.Add(skillId);
            }
        }

        /// <summary>
        /// 死亡处理
        /// </summary>
        protected virtual void OnDeath()
        {
            Debug.Log($"[{JobClass}] 死亡！");
            enabled = false;
        }

        /// <summary>
        /// 获取职业名称
        /// </summary>
        public string GetJobName()
        {
            return CubicClassColors.GetClassName(JobClass);
        }
    }

    /// <summary>
    /// 职业统计数据
    /// </summary>
    public static class CubicJobStats
    {
        public struct JobStats
        {
            public float MaxHP;
            public float MaxMP;
            public float PhysicalAttack;
            public float MagicAttack;
            public float Defense;
        }

        public static JobStats GetStats(CubicCharacterClass jobClass)
        {
            return jobClass switch
            {
                CubicCharacterClass.Warrior => new JobStats
                {
                    MaxHP = 120, MaxMP = 30, PhysicalAttack = 15, MagicAttack = 0, Defense = 10
                },
                CubicCharacterClass.Mage => new JobStats
                {
                    MaxHP = 70, MaxMP = 100, PhysicalAttack = 5, MagicAttack = 20, Defense = 3
                },
                CubicCharacterClass.Archer => new JobStats
                {
                    MaxHP = 90, MaxMP = 60, PhysicalAttack = 12, MagicAttack = 0, Defense = 5
                },
                CubicCharacterClass.Paladin => new JobStats
                {
                    MaxHP = 110, MaxMP = 70, PhysicalAttack = 10, MagicAttack = 8, Defense = 8
                },
                CubicCharacterClass.Assassin => new JobStats
                {
                    MaxHP = 65, MaxMP = 55, PhysicalAttack = 18, MagicAttack = 0, Defense = 3
                },
                CubicCharacterClass.Engineer => new JobStats
                {
                    MaxHP = 85, MaxMP = 80, PhysicalAttack = 10, MagicAttack = 5, Defense = 5
                },
                CubicCharacterClass.Necromancer => new JobStats
                {
                    MaxHP = 60, MaxMP = 120, PhysicalAttack = 5, MagicAttack = 18, Defense = 2
                },
                CubicCharacterClass.Cleric => new JobStats
                {
                    MaxHP = 80, MaxMP = 90, PhysicalAttack = 5, MagicAttack = 12, Defense = 5
                },
                _ => new JobStats()
            };
        }
    }
}
