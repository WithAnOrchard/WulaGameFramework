using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Skills
{
    /// <summary>
    /// 通用技能定义工厂 —— 提供常见技能的标准 <see cref="SkillDefinition"/> 构造器。
    /// <para>
    /// <b>不自动注册</b>：调用方决定何时 + 是否注册到 <see cref="SkillService"/>。
    /// 可用 <see cref="EnsureRegistered"/>（注册全部默认值）或自行 <c>BuildXxx()</c> 后改 ID/参数再 Register。
    /// </para>
    /// <para>
    /// 所有方法返回新建实例，不共享 Effects 列表 —— 调用方安全地修改返回值字段。
    /// </para>
    /// </summary>
    public static class CommonSkills
    {
        // ─── 技能 ID 常量 ─────────────────────────────────────
        public const string SKILL_DASH = "common_dash";
        public const string SKILL_JUMP_SLASH = "common_jump_slash";
        public const string SKILL_SUMMON = "common_summon";
        public const string SKILL_TELEPORT = "common_teleport";
        public const string SKILL_SHIELD = "common_shield";
        public const string SKILL_WHIRLWIND = "common_whirlwind";
        public const string SKILL_FIREBALL = "common_fireball";   // ProjectileEffect + Burn DoT
        public const string SKILL_HEAL_OVER_TIME = "common_regen";
        public const string SKILL_BURN = "common_burn";           // 单纯 DoT 投射示例
        public const string SKILL_SHOCKWAVE = "common_shockwave"; // 击退 + 范围伤
        public const string SKILL_CHAIN_LIGHTNING = "common_chain_lightning";
        public const string SKILL_METEOR = "common_meteor";
        public const string SKILL_LIFE_DRAIN = "common_life_drain";
        public const string SKILL_CLEAVE = "common_cleave";
        public const string SKILL_MULTISHOT = "common_multishot";
        public const string SKILL_CLEANSE = "common_cleanse";
        public const string SKILL_FROST_NOVA = "common_frost_nova"; // AOE + 减速
        public const string SKILL_HASTE = "common_haste";            // 自身加速 Buff
        public const string SKILL_STUN = "common_stun";
        public const string SKILL_SILENCE = "common_silence";
        public const string SKILL_REFLECT_SHIELD = "common_reflect_shield";
        public const string SKILL_CHANNEL_DRAIN = "common_channel_drain"; // 引导吸血

        private static bool _defaultsRegistered;

        /// <summary>
        /// 一次性注册所有"默认参数"通用技能。幂等，重复调用只生效一次。
        /// 注意：默认 <see cref="SKILL_SUMMON"/> 不指定 ConfigId（业务侧自行 RegisterDefinition 自定义版本）。
        /// </summary>
        public static void EnsureRegistered()
        {
            if (_defaultsRegistered) return;
            if (!SkillService.HasInstance) return;
            _defaultsRegistered = true;

            RegisterDefaults(
                BuildDash(), BuildJumpSlash(), BuildTeleport(), BuildShield(), BuildWhirlwind(), BuildFireball(),
                BuildBurn(), BuildHealOverTime(), BuildShockwave(), BuildChainLightning(), BuildMeteor(), BuildLifeDrain(),
                BuildCleave(), BuildMultiShot(), BuildCleanse(), BuildFrostNova(), BuildHaste(), BuildStun(),
                BuildSilence(), BuildReflectShield(), BuildChannelDrain());
            // SKILL_SUMMON 不带默认 ConfigId（必须由业务侧补完才能用），这里只占位生成空 def 也无意义 → 跳过。
        }

        private static void RegisterDefaults(params SkillDefinition[] definitions)
        {
            foreach (var definition in definitions)
                SkillService.Instance.RegisterDefinition(definition);
        }

        // ═══════════════════════════════════════════════════════════
        //  Dash —— 短距冲刺 + 0.2s 无敌帧
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildDash(
            float speed = 14f, float invulDuration = 0.2f, float cooldown = 4f)
        {
            return new SkillDefinition
            {
                Id = SKILL_DASH,
                DisplayName = "冲刺",
                Description = "快速向面朝方向冲刺一段距离，途中无敌。",
                Cooldown = cooldown,
                CastTime = 0f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.Directional,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new DashEffect(speed: speed, invulDuration: invulDuration),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Jump Slash —— 跳起 → 滞空 → 砸地 AOE
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildJumpSlash(
            float jumpUp = 9f, float jumpForward = 4f, float airTime = 0.45f,
            float impactRadius = 2.5f, float baseDamage = 20f, float cooldown = 8f)
        {
            return new SkillDefinition
            {
                Id = SKILL_JUMP_SLASH,
                DisplayName = "跳斩",
                Description = "腾空后向下砸地，对落点周围敌人造成范围伤害。",
                Cooldown = cooldown,
                CastTime = 0.1f,
                RecoveryTime = 0.3f,
                TargetMode = SkillTargetMode.Directional,
                Range = jumpForward,
                Effects = new List<ISkillEffect>
                {
                    new JumpSlashEffect(
                        jumpUp: jumpUp, jumpForward: jumpForward, airTime: airTime,
                        slamDownVelocity: -16f, impactRadius: impactRadius,
                        baseDamage: baseDamage, damagePerLevel: baseDamage * 0.25f,
                        damageType: "jump_slash"),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Summon —— 在施法者周围环形召唤指定 EntityConfig
        // ═══════════════════════════════════════════════════════════
        /// <summary>
        /// 构造一个召唤技能定义 —— 业务必须传入要召唤的 <paramref name="configId"/>（已注册的 EntityConfig Id）。
        /// 可通过 <paramref name="id"/> 自定义技能 ID（默认 = <see cref="SKILL_SUMMON"/>）。
        /// </summary>
        public static SkillDefinition BuildSummon(
            string configId, int count = 3, float radius = 2f,
            float cooldown = 12f, string id = null, string displayName = "召唤")
        {
            return new SkillDefinition
            {
                Id = string.IsNullOrEmpty(id) ? SKILL_SUMMON : id,
                DisplayName = displayName,
                Description = $"在周围召唤 {count} 个 {configId}。",
                Cooldown = cooldown,
                CastTime = 0.3f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new SummonEntityEffect(
                        configId: configId, count: count, radius: radius,
                        countPerLevel: 1, yOffset: 0f,
                        instanceIdPrefix: $"summon_{configId}"),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Teleport —— 朝向方向瞬移 5 单位
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildTeleport(float distance = 5f, float cooldown = 6f)
        {
            return new SkillDefinition
            {
                Id = SKILL_TELEPORT,
                DisplayName = "瞬移",
                Description = "立即向面朝方向位移一段距离。",
                Cooldown = cooldown,
                CastTime = 0f,
                RecoveryTime = 0.05f,
                TargetMode = SkillTargetMode.Directional,
                Range = distance,
                Effects = new List<ISkillEffect> { new TeleportEffect(distance: distance) },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Shield —— 自身护盾，5 秒内入伤 -50%
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildShield(float reduction = 0.5f, float duration = 5f, float cooldown = 15f)
        {
            return new SkillDefinition
            {
                Id = SKILL_SHIELD,
                DisplayName = "护盾",
                Description = $"获得 {duration:0}s 的伤害减免（-{reduction * 100f:0}%）。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect> { new ShieldEffect(reduction, duration, applyToSelf: true) },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Whirlwind —— 旋风斩，3 秒内每 0.4s 一次范围伤
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildWhirlwind(
            float duration = 3f, float tickInterval = 0.4f, float radius = 2.5f,
            float damagePerTick = 6f, float cooldown = 12f)
        {
            return new SkillDefinition
            {
                Id = SKILL_WHIRLWIND,
                DisplayName = "旋风斩",
                Description = "原地旋转，每 0.4 秒对周围敌人造成伤害，持续 3 秒。",
                Cooldown = cooldown,
                CastTime = 0.1f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.None,
                Range = radius,
                Effects = new List<ISkillEffect>
                {
                    new WhirlwindEffect(duration, tickInterval, radius, damagePerTick,
                        damagePerLevelPerTick: damagePerTick * 0.2f, damageType: "whirlwind"),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Fireball —— 投射物 + Burn DoT
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildFireball(
            float speed = 14f, float damage = 15f, float burnDamage = 3f, float burnDuration = 4f, float cooldown = 6f)
        {
            return new SkillDefinition
            {
                Id = SKILL_FIREBALL,
                DisplayName = "火球术",
                Description = "向面朝方向发射火球，命中后造成火焰伤害。",
                IconPath = "Common/Skills/Icons/fireball",
                ManaCost = 12f,
                Cooldown = cooldown,
                CastTime = 0.30f,
                RecoveryTime = 0.12f,
                TargetMode = SkillTargetMode.Directional,
                Range = 12f,
                Effects = new List<ISkillEffect>
                {
                    new ProjectileEffect(speed: speed, damage: damage,
                        damagePerLevel: damage * 0.3f, damageType: "fire", radius: 0.45f,
                        maxLifetime: 1.4f,
                        spriteId: "Common/Skills/Projectiles/projectile_fireball",
                        visualScale: 1.35f, sortingOrder: 260, forwardOffset: 0.7f, heightOffset: 0.35f,
                        impactCharacterConfigId: "CommonFireballImpact",
                        impactActionName: "Special", impactScale: 8f, impactLifetime: 0.38f,
                        visualRotationOffsetDegrees: -45f, ignoreStaticTargets: true,
                        areaDamageRadius: 1.65f, areaDamageMultiplier: 0.75f,
                        impactSfxId: "Sound/fireball_explode_light", impactSfxVolume: 1.2f,
                        suppressTargetHitSfx: true,
                        castSfxId: "Sound/fireball_cast_a", castSfxVolume: 0.95f,
                        castFlashPartId: "Weapon", castFlashDuration: 0.30f,
                        castFlashColor: new Color(1f, 0.18f, 0.08f, 1f)),
                    // 命中后触发的"持续燃烧" —— 注意 ProjectileEffect 是在飞行物命中时直接 TryDamage，不会自动叠 DoT；
                    // 这里把 DoT 单独在 Apply 阶段注册到 ctx.Target？ ctx.Target 在 Directional 下通常为 null。
                    // 折中：火球的 DoT 不在此处直接绑（ProjectileEffect 没有"命中回调"）。
                    // 业务可在 SkillProjectile 上自行追加击中事件，或单独投个 DoT 技能。
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Burn —— 给目标贴一个燃烧 DoT（需 Targeted 选定）
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildBurn(
            float duration = 5f, float tickInterval = 1f, float damagePerTick = 4f, float cooldown = 5f)
        {
            return new SkillDefinition
            {
                Id = SKILL_BURN,
                DisplayName = "灼烧",
                Description = $"使目标燃烧 {duration:0}s，每 {tickInterval:0.0}s 受到 {damagePerTick:0} 伤害。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.Targeted,
                Range = 8f,
                Effects = new List<ISkillEffect>
                {
                    new DotEffect("burn", duration, tickInterval, damagePerTick,
                        damagePerLevelPerTick: damagePerTick * 0.25f, damageType: "fire"),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Heal Over Time —— 自身再生
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildHealOverTime(
            float duration = 6f, float tickInterval = 1f, float healPerTick = 5f, float cooldown = 15f)
        {
            return new SkillDefinition
            {
                Id = SKILL_HEAL_OVER_TIME,
                DisplayName = "再生",
                Description = $"{duration:0} 秒内每秒回复 {healPerTick:0} 生命。",
                Cooldown = cooldown,
                CastTime = 0.3f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new HotEffect("regen", duration, tickInterval, healPerTick,
                        healPerLevelPerTick: healPerTick * 0.3f, applyToSelf: true),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Shockwave —— 周身震波：范围伤 + 击退
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildShockwave(
            float radius = 3f, float damage = 10f, float knockbackForce = 12f, float cooldown = 10f)
        {
            return new SkillDefinition
            {
                Id = SKILL_SHOCKWAVE,
                DisplayName = "震波",
                Description = "对周围敌人造成伤害并击退。",
                Cooldown = cooldown,
                CastTime = 0.15f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.None,
                Range = radius,
                Effects = new List<ISkillEffect>
                {
                    new AoeEffect(radius)
                    {
                        SubEffects = new List<ISkillEffect>
                        {
                            new DamageEffect(damage, damagePerLevel: damage * 0.25f, damageType: "shockwave"),
                            new KnockbackEffect(knockbackForce, upKick: 4f),
                        },
                    },
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Chain Lightning —— 4 跳 链式闪电（每跳衰减 20%）
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildChainLightning(
            float baseDamage = 15f, int maxJumps = 4, float jumpRadius = 4f, float cooldown = 8f)
        {
            return new SkillDefinition
            {
                Id = SKILL_CHAIN_LIGHTNING,
                DisplayName = "链式闪电",
                Description = $"对目标释放闪电，最多跳跃 {maxJumps} 次，每跳伤害衰减 20%。",
                Cooldown = cooldown,
                CastTime = 0.3f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.Targeted,
                Range = jumpRadius * 1.5f,
                Effects = new List<ISkillEffect>
                {
                    new ChainLightningEffect(baseDamage, maxJumps, jumpRadius,
                        falloffPerJump: 0.8f, damagePerLevel: baseDamage * 0.3f),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Meteor —— 延迟范围爆炸（PointTarget）
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildMeteor(
            float impactDelay = 1.2f, float radius = 3f, float damage = 35f, float cooldown = 12f)
        {
            return new SkillDefinition
            {
                Id = SKILL_METEOR,
                DisplayName = "陨石术",
                Description = $"在目标位置 {impactDelay:0.0}s 后落下陨石，对 {radius:0} 半径内敌人造成伤害。",
                Cooldown = cooldown,
                CastTime = 0.5f,
                RecoveryTime = 0.3f,
                TargetMode = SkillTargetMode.PointTarget,
                Range = 10f,
                Effects = new List<ISkillEffect>
                {
                    new MeteorEffect(impactDelay, radius, damage,
                        damagePerLevel: damage * 0.3f, damageType: "fire"),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Life Drain —— 吸血单体
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildLifeDrain(
            float damage = 14f, float healRatio = 0.5f, float cooldown = 6f)
        {
            return new SkillDefinition
            {
                Id = SKILL_LIFE_DRAIN,
                DisplayName = "生命汲取",
                Description = $"对目标造成 {damage:0} 伤害，并将 {healRatio * 100f:0}% 实际伤害化作生命回复。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.15f,
                TargetMode = SkillTargetMode.Targeted,
                Range = 6f,
                Effects = new List<ISkillEffect>
                {
                    new LifeDrainEffect(damage, healRatio, damagePerLevel: damage * 0.25f),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Cleave —— 锥形挥砍（90° 锥角 + 范围伤）
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildCleave(
            float range = 3f, float halfAngle = 45f, float damage = 12f, float cooldown = 4f)
        {
            return new SkillDefinition
            {
                Id = SKILL_CLEAVE,
                DisplayName = "横扫",
                Description = "对面前锥形范围内所有敌人造成伤害。",
                Cooldown = cooldown,
                CastTime = 0.15f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.Directional,
                Range = range,
                Effects = new List<ISkillEffect>
                {
                    new CleaveEffect(range, halfAngle)
                    {
                        SubEffects = new List<ISkillEffect>
                        {
                            new DamageEffect(damage, damagePerLevel: damage * 0.25f, damageType: "physical"),
                        },
                    },
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Multishot —— 扇形 3 连射
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildMultiShot(
            int count = 3, float spreadAngle = 30f, float speed = 14f, float damage = 7f, float cooldown = 5f)
        {
            return new SkillDefinition
            {
                Id = SKILL_MULTISHOT,
                DisplayName = "多重射击",
                Description = $"向面朝方向呈扇形发射 {count} 枚投射物。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.Directional,
                Range = 10f,
                Effects = new List<ISkillEffect>
                {
                    new MultiShotEffect(count, spreadAngle, speed, damage,
                        damagePerLevel: damage * 0.3f, damageType: "projectile"),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Cleanse —— 净化（移除自身所有 Buff，或指定 ID）
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildCleanse(
            List<string> buffIds = null, float cooldown = 20f)
        {
            return new SkillDefinition
            {
                Id = SKILL_CLEANSE,
                DisplayName = "净化",
                Description = buffIds == null || buffIds.Count == 0
                    ? "移除自身所有负面效果。"
                    : $"移除自身指定效果：{string.Join(",", buffIds)}。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new CleanseEffect(buffIds, applyToSelf: true),
                },
                MaxLevel = 1,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Frost Nova —— 周围 AOE 伤害 + 减速
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildFrostNova(
            float radius = 3.5f, float damage = 8f, float slowMultiplier = 0.4f,
            float slowDuration = 3f, float cooldown = 10f)
        {
            return new SkillDefinition
            {
                Id = SKILL_FROST_NOVA,
                DisplayName = "冰霜新星",
                Description = $"对周围 {radius:0} 半径敌人造成伤害并减速 {(1 - slowMultiplier) * 100f:0}%。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.2f,
                TargetMode = SkillTargetMode.None,
                Range = radius,
                Effects = new List<ISkillEffect>
                {
                    new AoeEffect(radius)
                    {
                        SubEffects = new List<ISkillEffect>
                        {
                            new DamageEffect(damage, damagePerLevel: damage * 0.25f, damageType: "frost"),
                            new SlowEffect("frost", slowMultiplier, slowDuration, applyToSelf: false),
                        },
                    },
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Haste —— 自身加速 Buff
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildHaste(
            float multiplier = 1.6f, float duration = 5f, float cooldown = 18f)
        {
            return new SkillDefinition
            {
                Id = SKILL_HASTE,
                DisplayName = "疾跑",
                Description = $"{duration:0} 秒内移动速度提升 {(multiplier - 1f) * 100f:0}%。",
                Cooldown = cooldown,
                CastTime = 0f,
                RecoveryTime = 0.05f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new SlowEffect("haste", multiplier, duration, applyToSelf: true),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Stun —— 单体眩晕
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildStun(float duration = 1.5f, float damage = 6f, float cooldown = 8f)
        {
            return new SkillDefinition
            {
                Id = SKILL_STUN,
                DisplayName = "击晕",
                Description = $"对目标造成 {damage:0} 伤害并眩晕 {duration:0.0}s。",
                Cooldown = cooldown,
                CastTime = 0.1f,
                RecoveryTime = 0.15f,
                TargetMode = SkillTargetMode.Targeted,
                Range = 3f,
                Effects = new List<ISkillEffect>
                {
                    new DamageEffect(damage, damagePerLevel: damage * 0.25f, damageType: "physical"),
                    new StunEffect("stun", duration),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Silence —— 单体沉默
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildSilence(float duration = 3f, float cooldown = 12f)
        {
            return new SkillDefinition
            {
                Id = SKILL_SILENCE,
                DisplayName = "沉默",
                Description = $"使目标 {duration:0}s 内无法施法。",
                Cooldown = cooldown,
                CastTime = 0.3f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.Targeted,
                Range = 6f,
                Effects = new List<ISkillEffect>
                {
                    new SilenceEffect("silence", duration),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Reflect Shield —— 反伤护盾
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildReflectShield(
            float reflectRatio = 0.5f, float duration = 6f, float cooldown = 20f)
        {
            return new SkillDefinition
            {
                Id = SKILL_REFLECT_SHIELD,
                DisplayName = "荆棘护甲",
                Description = $"{duration:0}s 内将受到的 {reflectRatio * 100f:0}% 伤害反射给攻击者。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.1f,
                TargetMode = SkillTargetMode.None,
                Range = 0f,
                Effects = new List<ISkillEffect>
                {
                    new DamageReflectEffect(reflectRatio, duration, applyToSelf: true),
                },
                MaxLevel = 3,
            };
        }

        // ═══════════════════════════════════════════════════════════
        //  Channel Drain —— 引导吸血（每 0.5s 一次 LifeDrain，持续 3s）
        // ═══════════════════════════════════════════════════════════
        public static SkillDefinition BuildChannelDrain(
            float channelTime = 3f, float tickInterval = 0.5f, float damagePerTick = 6f,
            float healRatio = 0.5f, float cooldown = 10f)
        {
            return new SkillDefinition
            {
                Id = SKILL_CHANNEL_DRAIN,
                DisplayName = "暗影抽取",
                Description = $"引导 {channelTime:0}s，每 {tickInterval:0.0}s 对目标造成 {damagePerTick:0} 伤害并回血。",
                Cooldown = cooldown,
                CastTime = 0.2f,
                RecoveryTime = 0.2f,
                ChannelTime = channelTime,
                ChannelTickInterval = tickInterval,
                TargetMode = SkillTargetMode.Targeted,
                Range = 7f,
                Effects = new List<ISkillEffect>
                {
                    new LifeDrainEffect(damagePerTick, healRatio,
                        damagePerLevel: damagePerTick * 0.25f, damageType: "drain"),
                },
                MaxLevel = 3,
            };
        }
    }
}

