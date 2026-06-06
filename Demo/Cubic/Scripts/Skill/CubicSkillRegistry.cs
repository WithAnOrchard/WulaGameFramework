using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using Demo.Cubic;

namespace Demo.Cubic.Skill
{
    /// <summary>
    /// Cubic 技能注册表 —— 把 8 职业共 12 个技能注册到框架 <see cref="SkillManager"/>。
    /// <para>
    /// 全部走框架的 <c>EVT_REGISTER_SKILL</c> 事件（bare-string §4.1）。技能效果链由框架 Effects 装配，
    /// 业务侧不实现 <c>ISkillEffect</c>。每个 SkillDefinition 内置 VFXId，供 <see cref="VFX.CubicVFXManager"/>
    /// 在施法命中时通过 <c>EVT_PLAY_VFX</c> 触发对应特效（业务侧在 Effect 链中按约定调用）。
    /// </para>
    /// <para>原 homegrown 字典式元数据桩已全部删除：技能"做什么"由 <see cref="SkillDefinition.Effects"/> 决定，
    /// "什么职业能学"由 <see cref="SkillsByClass"/> 决定，"施法时播哪个 VFX"由 <see cref="VfxIdOf"/> 决定。</para>
    /// </summary>
    public static class CubicSkillRegistry
    {
        // ─── 技能 ID（按业务方约定保持字符串稳定）──────────────────
        public const string ID_WARRIOR_SLASH     = "cubic_warrior_slash";
        public const string ID_WARRIOR_WAR_CRY   = "cubic_warrior_war_cry";
        public const string ID_WARRIOR_WHIRLWIND = "cubic_warrior_whirlwind";

        public const string ID_MAGE_FIREBALL        = "cubic_mage_fireball";
        public const string ID_MAGE_FROST_NOVA      = "cubic_mage_frost_nova";
        public const string ID_MAGE_CHAIN_LIGHTNING = "cubic_mage_chain_lightning";

        public const string ID_ARCHER_MULTISHOT = "cubic_archer_multishot";
        public const string ID_ARCHER_PIERCE    = "cubic_archer_pierce";
        public const string ID_ARCHER_DASH      = "cubic_archer_dash";

        public const string ID_PALADIN_HOLY_SLASH = "cubic_paladin_holy_slash";
        public const string ID_PALADIN_HAMMER    = "cubic_paladin_hammer";
        public const string ID_PALADIN_DEVOTION  = "cubic_paladin_devotion";

        // ─── VFX ID 映射（与 VFX.CubicVFXManager 注册的 id 一致）──
        public const string VFX_WARRIOR_SLASH     = "warrior_slash";
        public const string VFX_WARRIOR_WAR_CRY   = "warrior_shout";
        public const string VFX_WARRIOR_WHIRLWIND = "warrior_whirlwind";
        public const string VFX_MAGE_FIREBALL        = "mage_fireball";
        public const string VFX_MAGE_FROST_NOVA      = "mage_frost_nova";
        public const string VFX_MAGE_CHAIN_LIGHTNING = "mage_lightning";
        public const string VFX_ARCHER_MULTISHOT = "archer_arrow";
        public const string VFX_ARCHER_PIERCE    = "archer_pierce";
        public const string VFX_ARCHER_DASH      = "archer_dash";
        public const string VFX_PALADIN_HOLY_SLASH = "paladin_holy";
        public const string VFX_PALADIN_HAMMER    = "paladin_hammer";
        public const string VFX_PALADIN_DEVOTION  = "paladin_devotion";

        private static bool _initialized;

        /// <summary>vfxId → 触发该特效的 skillId（反查，用于 Effect 内部打点）。</summary>
        private static readonly Dictionary<string, string> _skillOfVfx = new();

        /// <summary>skillId → VFX 标识（业务 Effect 想播特效时取这个）。</summary>
        private static readonly Dictionary<string, string> _vfxOfSkill = new();

        /// <summary>jobClass → 该职业默认学习的 skillId 列表。</summary>
        private static readonly Dictionary<CubicCharacterClass, List<string>> _byClass = new();

        /// <summary>
        /// 注册全部默认技能到框架 <see cref="SkillManager"/>。
        /// 幂等，重复调用只生效一次。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            if (!EventProcessor.HasInstance)
            {
                Debug.LogWarning("[CubicSkillRegistry] EventProcessor 未就绪，跳过注册");
                return;
            }
            _initialized = true;

            var defs = BuildAllDefinitions();
            foreach (var d in defs)
            {
                // §4.1 bare-string：消费方不 using SkillManager 命名空间，靠 lint 校验事件名。
                EventProcessor.Instance.TriggerEventMethod(
                    "RegisterSkill", new List<object> { d });
            }

            Debug.Log($"[CubicSkillRegistry] 技能注册完成，共 {defs.Count} 个");
        }

        /// <summary>查询某职业的默认技能 ID 列表（无副作用 —— 仅供 CubicEntity 学习用）。</summary>
        public static List<string> GetClassSkills(CubicCharacterClass jobClass)
        {
            return _byClass.TryGetValue(jobClass, out var list) ? new List<string>(list) : new List<string>();
        }

        /// <summary>查询某 skillId 对应的 VFX id（无 → null）。</summary>
        public static string VfxIdOf(string skillId)
            => _vfxOfSkill.TryGetValue(skillId, out var v) ? v : null;

        // ════════════════════════════════════════════════════════════
        //  技能定义构造（12 个）
        // ════════════════════════════════════════════════════════════

        private static List<SkillDefinition> BuildAllDefinitions()
        {
            var list = new List<SkillDefinition>();

            // ─── 战士 ────────────────────────────────────────────
            Register(list, ID_WARRIOR_SLASH, "横扫斩", "对前方扇形范围造成物理伤害", VFX_WARRIOR_SLASH,
                manaCost: 5, cooldown: 4, castTime: 0.2f, recoveryTime: 0.2f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new CleaveEffect(range: 2.2f, halfAngleDeg: 60f)
                    {
                        SubEffects = new List<ISkillEffect> { new DamageEffect(12, damageType: "physical") },
                    },
                });
            Register(list, ID_WARRIOR_WAR_CRY, "战吼", "提升自身攻击力 30% 持续 5 秒", VFX_WARRIOR_WAR_CRY,
                manaCost: 15, cooldown: 15, castTime: 0.5f, recoveryTime: 0.5f,
                targetMode: SkillTargetMode.None,
                effects: new ISkillEffect[]
                {
                    new BuffEffect("war_cry", duration: 5f, factory: BuildWarCryBuff, applyToSelf: true),
                });
            Register(list, ID_WARRIOR_WHIRLWIND, "旋风斩", "3 秒内持续对周围造成伤害", VFX_WARRIOR_WHIRLWIND,
                manaCost: 20, cooldown: 12, castTime: 0.3f, recoveryTime: 0.5f,
                targetMode: SkillTargetMode.None,
                effects: new ISkillEffect[]
                {
                    new WhirlwindEffect(
                        duration: 3f, tickInterval: 0.4f,
                        radius: 2.5f, damagePerTick: 6, damagePerLevelPerTick: 1.5f,
                        damageType: "physical"),
                });

            // ─── 魔法师 ──────────────────────────────────────────
            Register(list, ID_MAGE_FIREBALL, "火球术", "向面朝方向射出火球", VFX_MAGE_FIREBALL,
                manaCost: 15, cooldown: 6, castTime: 0.4f, recoveryTime: 0.3f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new ProjectileEffect(speed: 14f, damage: 15, damageType: "fire", radius: 0.3f, maxLifetime: 2.5f),
                });
            Register(list, ID_MAGE_FROST_NOVA, "冰霜新星", "周围 3.5 半径造成伤害并减速 60% 持续 3 秒", VFX_MAGE_FROST_NOVA,
                manaCost: 25, cooldown: 10, castTime: 0.5f, recoveryTime: 0.4f,
                targetMode: SkillTargetMode.None,
                effects: new ISkillEffect[]
                {
                    new AoeEffect(radius: 3.5f, includeSelf: false)
                    {
                        SubEffects = new List<ISkillEffect>
                        {
                            new DamageEffect(8, damageType: "frost"),
                            new SlowEffect("frost_slow", multiplier: 0.4f, duration: 3f),
                        },
                    },
                });
            Register(list, ID_MAGE_CHAIN_LIGHTNING, "闪电链", "链式攻击最多 4 个目标（每跳衰减 20%）", VFX_MAGE_CHAIN_LIGHTNING,
                manaCost: 20, cooldown: 8, castTime: 0.3f, recoveryTime: 0.3f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new ChainLightningEffect(baseDamage: 10, maxJumps: 4, jumpRadius: 5f, falloffPerJump: 0.8f,
                        damageType: "lightning"),
                });

            // ─── 弓箭手 ──────────────────────────────────────────
            Register(list, ID_ARCHER_MULTISHOT, "多重射击", "扇形射出 3 支箭", VFX_ARCHER_MULTISHOT,
                manaCost: 12, cooldown: 5, castTime: 0.3f, recoveryTime: 0.3f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new MultiShotEffect(projectileCount: 3, spreadAngleDeg: 30f,
                        speed: 16f, damage: 7, damageType: "physical"),
                });
            Register(list, ID_ARCHER_PIERCE, "穿刺箭", "射出穿透箭矢", VFX_ARCHER_PIERCE,
                manaCost: 18, cooldown: 8, castTime: 0.4f, recoveryTime: 0.4f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new ProjectileEffect(speed: 20f, damage: 18, damageType: "physical",
                        radius: 0.3f, maxLifetime: 1.5f, pierce: true),
                });
            Register(list, ID_ARCHER_DASH, "疾风步", "向面朝方向冲刺 0.2 秒无敌", VFX_ARCHER_DASH,
                manaCost: 10, cooldown: 6, castTime: 0.1f, recoveryTime: 0.2f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new DashEffect(speed: 16f, invulDuration: 0.2f),
                });

            // ─── 圣骑士 ──────────────────────────────────────────
            Register(list, ID_PALADIN_HOLY_SLASH, "圣光斩", "锥形范围伤害 + 治疗自身", VFX_PALADIN_HOLY_SLASH,
                manaCost: 8, cooldown: 4, castTime: 0.3f, recoveryTime: 0.3f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new CleaveEffect(range: 2.2f, halfAngleDeg: 60f)
                    {
                        SubEffects = new List<ISkillEffect> { new DamageEffect(8, damageType: "holy") },
                    },
                    new HealEffect(baseHeal: 5, healSelf: true),
                });
            Register(list, ID_PALADIN_HAMMER, "正义之锤", "锥形范围伤害 + 眩晕 1.5 秒", VFX_PALADIN_HAMMER,
                manaCost: 15, cooldown: 8, castTime: 0.5f, recoveryTime: 0.4f,
                targetMode: SkillTargetMode.Directional,
                effects: new ISkillEffect[]
                {
                    new CleaveEffect(range: 2.5f, halfAngleDeg: 70f)
                    {
                        SubEffects = new List<ISkillEffect> { new DamageEffect(10, damageType: "holy") },
                    },
                    new StunEffect("paladin_hammer_stun", duration: 1.5f),
                });
            Register(list, ID_PALADIN_DEVOTION, "奉献", "自身持续回血 6 秒", VFX_PALADIN_DEVOTION,
                manaCost: 25, cooldown: 15, castTime: 0.6f, recoveryTime: 0.5f,
                targetMode: SkillTargetMode.None,
                effects: new ISkillEffect[]
                {
                    new HotEffect(buffId: "devotion_hot", duration: 6f, tickInterval: 1f,
                        healPerTick: 4, healPerLevelPerTick: 1f, applyToSelf: true),
                });

            // ─── 职业 → 技能映射 ─────────────────────────────────
            _byClass[CubicCharacterClass.Warrior]  = new List<string> { ID_WARRIOR_SLASH, ID_WARRIOR_WAR_CRY, ID_WARRIOR_WHIRLWIND };
            _byClass[CubicCharacterClass.Mage]     = new List<string> { ID_MAGE_FIREBALL, ID_MAGE_FROST_NOVA, ID_MAGE_CHAIN_LIGHTNING };
            _byClass[CubicCharacterClass.Archer]   = new List<string> { ID_ARCHER_MULTISHOT, ID_ARCHER_PIERCE, ID_ARCHER_DASH };
            _byClass[CubicCharacterClass.Paladin]  = new List<string> { ID_PALADIN_HOLY_SLASH, ID_PALADIN_HAMMER, ID_PALADIN_DEVOTION };
            // Phase 2 暂未注册；给空列表占位，避免后续 GetClassSkills 抛 KeyNotFound。
            _byClass[CubicCharacterClass.Assassin]     = new List<string>();
            _byClass[CubicCharacterClass.Engineer]     = new List<string>();
            _byClass[CubicCharacterClass.Necromancer]  = new List<string>();
            _byClass[CubicCharacterClass.Cleric]       = new List<string>();

            return list;
        }

        private static void Register(
            List<SkillDefinition> bag, string id, string displayName, string description, string vfxId,
            float manaCost, float cooldown, float castTime, float recoveryTime,
            SkillTargetMode targetMode, ISkillEffect[] effects)
        {
            bag.Add(new SkillDefinition
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                ManaCost = manaCost,
                Cooldown = cooldown,
                CastTime = castTime,
                RecoveryTime = recoveryTime,
                TargetMode = targetMode,
                Effects = new List<ISkillEffect>(effects),
                MaxLevel = 1,
            });
            _vfxOfSkill[id] = vfxId;
            _skillOfVfx[vfxId] = id;
        }

        // ════════════════════════════════════════════════════════════
        //  Buff 工厂 —— 业务侧用闭包注入特定 Buff 行为
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// 战吼 Buff：5 秒内施法者获得"战吼"状态（占位日志）。
        /// <para>
        /// 真实业务可在 Demo 内加一个 <c>WarCryComponent</c>（挂在施法者上），
        /// 然后用 <c>OnAttach</c> + <c>OnExpire</c> 改 AttackPower / 攻速等。
        /// 当前框架 <c>BuffInstance</c> 没有 <c>OnAttach</c> 钩子，所以只在首 tick 与
        /// 过期时打日志 —— 关键是把"Buff 挂上 → Tick 推进 → 过期清理"这条 BuffInstance
        /// 生命周期走通。
        /// </para>
        /// </summary>
        private static BuffInstance BuildWarCryBuff(SkillEffectContext ctx, float duration)
        {
            bool attached = false;
            return new BuffInstance
            {
                BuffId = "war_cry",
                SourceId = ctx.CasterId,
                TargetId = ctx.CasterId,
                Duration = duration,
                OnTick = (b, _) =>
                {
                    if (!attached && !string.IsNullOrEmpty(b.TargetId))
                    {
                        attached = true;
                        Debug.Log($"[CubicSkillRegistry] 战吼 Buff 挂上 → target={b.TargetId} duration={duration}s");
                    }
                },
                OnExpire = b => Debug.Log($"[CubicSkillRegistry] 战吼 Buff 结束 → target={b.TargetId}"),
            };
        }
    }
}
