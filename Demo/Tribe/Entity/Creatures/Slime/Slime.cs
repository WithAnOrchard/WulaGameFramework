using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager.Dao;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects;

namespace Demo.Tribe.Entities
{
    /// <summary>
    /// 史莱姆 —— 跳跃式怪物，含两个专属技能：
    /// <list type="bullet">
    /// <item><b>大蹦</b>（<see cref="SKILL_BIG_HOP"/>）：玩家进入侦测范围时朝玩家方向一跃，命中接触伤害。</item>
    /// <item><b>巨大化</b>（<see cref="SKILL_GIANT"/>）：自我增益 Buff，体积 × 生命 × 减伤 × 跳跃强度全方位提升。</item>
    /// </list>
    /// <para>大蹦走框架 <see cref="DashEffect"/>；巨大化走框架 <see cref="BuffEffect"/> + 本类
    /// <see cref="BuildGiantBuff"/> 闭包 → 闭包内 Apply <see cref="GiantSlimeState"/> + 约定 Revert。</para>
    /// <para>专属 MB：<see cref="TribeSlimeHopBehavior"/>（小蹦自驱 / 大蹦+巨大化触发）、
    /// <see cref="GiantSlimeState"/>（巨大化运行时状态）。</para>
    /// </summary>
    public static class Slime
    {
        // ─── 角色 + 技能 ID 常量 ──────────────────────────────
        public const string CharacterConfigId = "tribe_slime";
        public const string SKILL_BIG_HOP = "slime_big_hop";
        public const string SKILL_GIANT = "slime_giant";
        public const string BUFF_GIANT = "slime_giant_buff";

        /// <summary>大蹦侦测范围（玩家在该距离内才尝试 cast；与 SkillDefinition.Range 一致）。</summary>
        public const float BigHopRange = 7f;

        private static bool _characterRegistered;
        private static bool _skillsRegistered;

        // ═══════════════════════════════════════════════════════════
        //  视觉 —— 注册到 CharacterManager（4×4 sheet：行 1=左，行 2=右）
        // ═══════════════════════════════════════════════════════════
        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "史莱姆",
                idleResourcePath: "Tribe/Common/Entity/Slime 01_idle (16x16)",
                walkResourcePath: "Tribe/Common/Entity/Slime 01_walk (16x16)",
                frameRate: 1f / 0.12f,
                leftFrameIndices: new[] { 4, 5, 6, 7 },
                rightFrameIndices: new[] { 8, 9, 10, 11 },
                visualScale: 8f, visualYOffset: 0f);
            _characterRegistered = true;
        }

        // ═══════════════════════════════════════════════════════════
        //  Preset —— 配置工厂（自动注册视觉）
        // ═══════════════════════════════════════════════════════════
        public static TribeCreatureConfig Preset()
        {
            EnsureCharacterRegistered();
            return new TribeCreatureConfig
            {
                Id = "slime", DisplayName = "史莱姆",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.35f, FreezePositionX = false,
                MaxHp = 6f, MoveSpeed = 1.6f, PatrolDistance = 3f,   // PatrolDistance 仅 Brain 兼容；Hop 用 ActivityRadius
                CanAttack = true, ContactDamage = 5f, DamageCooldown = 1.2f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 10f,
                // 跳跃式移动：平时小蹦节奏 ~0.5s 一次，大蹦由 SkillManager 控制（参 SKILL_BIG_HOP）
                UseHopMovement = true,
                // 小蹦 = 大蹦的 1/4 距离（等比缩放：H、V 各 ×1/2 → 距离 (1/2)² = 1/4，弧形相似）
                SmallHopHorizontal = 4.5f, SmallHopVertical = 5.5f,
                HopCooldown = 0.8f,
                ActivityRadius = 6f,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Skills 注册 —— 幂等，所有史莱姆 BindEntity 时调用一次
        // ═══════════════════════════════════════════════════════════
        /// <summary>幂等注册史莱姆专属技能定义（已注册则跳过）。
        /// 走框架通用 effect，业务侧不实现 <c>ISkillEffect</c>。</summary>
        public static void EnsureSkillsRegistered()
        {
            if (_skillsRegistered) return;
            if (!EventProcessor.HasInstance) return;

            // ─── 大蹦：通用 DashEffect（水平冲量 + 替换竖直速度） ─────────
            var bigHopDef = new SkillDefinition
            {
                Id = SKILL_BIG_HOP,
                DisplayName = "史莱姆冲撞",
                Description = "凶猛地向目标方向一跃，撞击玩家。",
                Cooldown = 6f,
                CastTime = 0f,
                RecoveryTime = 0f,
                TargetMode = SkillTargetMode.Directional,
                Range = BigHopRange,                          // 侦测半径
                Effects = new List<ISkillEffect>
                {
                    new DashEffect(
                        speed: 9f,                            // 水平 9
                        keepVerticalVelocity: false,
                        verticalKick: 11f),                   // 竖直 11
                },
                MaxLevel = 1,
            };
            EventProcessor.Instance.TriggerEventMethod(
                SkillManager.EVT_REGISTER_SKILL, new List<object> { bigHopDef });

            // ─── 巨大化：通用 BuffEffect + BuffFactory 闭包 ─────────────
            // Tribe 专属"应用 / 撤销"逻辑封装在 BuildGiantBuff 内：
            //   1) AddComponent<GiantSlimeState> + Apply()  ← 视觉 / 碰撞 / 生命 / 跳跃倍率
            //   2) 返回 BuffInstance.OnExpire = state.Revert() + Destroy
            var giantDef = new SkillDefinition
            {
                Id = SKILL_GIANT,
                DisplayName = "巨大化",
                Description = "短时间内巨大化：体积 ×2、生命 ×3、伤害减免 50%、跳跃强化。",
                Cooldown = 30f,
                CastTime = 0f,
                RecoveryTime = 0f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new BuffEffect(
                        buffId: BUFF_GIANT,
                        duration: 12f,
                        factory: (ctx, duration) => BuildGiantBuff(ctx, duration,
                            scale: 2.0f, hp: 3.0f, dmgRed: 0.5f, hop: 1.5f),
                        applyToSelf: true),
                },
                MaxLevel = 1,
            };
            EventProcessor.Instance.TriggerEventMethod(
                SkillManager.EVT_REGISTER_SKILL, new List<object> { giantDef });

            _skillsRegistered = true;
        }

        // ═══════════════════════════════════════════════════════════
        //  巨大化 BuffFactory —— Apply state + 约定 Revert 全在闭包内
        // ═══════════════════════════════════════════════════════════
        private static BuffInstance BuildGiantBuff(SkillEffectContext ctx, float duration,
            float scale, float hp, float dmgRed, float hop)
        {
            var go = SkillEntityProxy.Root(ctx?.CasterId)?.gameObject;
            if (go == null) return null;

            // 同一实体重复 cast：复用现有 GiantSlimeState（Apply 已幂等），仅当 state 不存在时挂新组件
            var state = go.GetComponent<GiantSlimeState>();
            if (state == null)
            {
                state = go.AddComponent<GiantSlimeState>();
                state.ScaleMultiplier = scale;
                state.HpMultiplier = hp;
                state.DamageReduction = dmgRed;
                state.HopMultiplier = hop;
                state.Apply();
            }

            return new BuffInstance
            {
                BuffId = BUFF_GIANT,
                SourceId = ctx.CasterId,
                TargetId = ctx.CasterId,
                Duration = duration,
                OnExpire = _ =>
                {
                    if (state != null) state.Revert();
                    if (go != null && go.TryGetComponent<GiantSlimeState>(out var s))
                        Object.Destroy(s);
                },
            };
        }
    }
}
