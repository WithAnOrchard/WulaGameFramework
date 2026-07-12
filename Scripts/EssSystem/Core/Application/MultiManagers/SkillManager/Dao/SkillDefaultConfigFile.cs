using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao
{
    [Serializable]
    public class SkillDefaultConfigFile
    {
        public List<SkillDefinitionSpec> Skills = new();
    }

    [Serializable]
    public class SkillDefinitionSpec
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string IconPath;
        public string Category;
        public string SkillStatus;
        public float ManaCost;
        public float HpCost;
        public float Cooldown = 1f;
        public float CastTime;
        public float RecoveryTime;
        public float ChannelTime;
        public float ChannelTickInterval = 0.5f;
        public SkillTargetMode TargetMode = SkillTargetMode.None;
        public float Range = 5f;
        public int MaxLevel = 1;
        public List<SkillEffectSpec> Effects = new();

        public SkillDefinition ToDefinition()
        {
            var definition = new SkillDefinition
            {
                Id = Id,
                DisplayName = DisplayName,
                Description = Description,
                IconPath = IconPath,
                Category = Category,
                SkillStatus = SkillStatus,
                ManaCost = ManaCost,
                HpCost = HpCost,
                Cooldown = Cooldown,
                CastTime = CastTime,
                RecoveryTime = RecoveryTime,
                ChannelTime = ChannelTime,
                ChannelTickInterval = ChannelTickInterval,
                TargetMode = TargetMode,
                Range = Range,
                MaxLevel = MaxLevel,
                Effects = new List<ISkillEffect>(),
            };

            if (Effects == null) return definition;
            foreach (var effect in Effects)
            {
                var built = effect?.Build();
                if (built != null) definition.Effects.Add(built);
            }

            return definition;
        }
    }

    [Serializable]
    public class SkillEffectSpec
    {
        public string EffectType;

        public string BuffId;
        public List<string> BuffIds = new();
        public List<SkillEffectSpec> SubEffects = new();
        public bool ApplyToSelf;
        public bool IncludeSelf;

        public float BaseDamage;
        public float Speed = 12f;
        public float SpeedPerLevel;
        public float Damage = 8f;
        public float DamagePerLevel;
        public string DamageType = "skill";
        public float Heal = 8f;
        public float HealPerLevel;
        public float Duration = 3f;
        public float TickInterval = 1f;
        public float DamagePerTick = 5f;
        public float DamagePerLevelPerTick;
        public float HealPerTick = 5f;
        public float HealPerLevelPerTick;
        public float Reduction = 0.5f;
        public float Multiplier = 0.5f;
        public float Distance = 5f;
        public float DistancePerLevel;
        public bool UseAbsolutePosition;
        public bool KeepVerticalVelocity = true;
        public float VerticalKick;
        public float InvulnerableDuration;
        public float DashDuration = 0.18f;
        public float JumpUp = 9f;
        public float JumpForward = 4f;
        public float AirTime = 0.45f;
        public float SlamDownVelocity = -16f;
        public float ImpactRadius = 2.5f;
        public float Force = 6f;
        public float UpwardForce = 1.5f;
        public float EffectRange = 3f;
        public float HalfAngleDeg = 45f;
        public float Radius = 0.3f;
        public float RadiusPerLevel;
        public float MaxLifetime = 4f;
        public bool Pierce;
        public int ProjectileCount = 3;
        public float SpreadAngleDeg = 30f;
        public float SearchRadius = 8f;
        public float HomingTurnSpeed = 900f;
        public float SpawnSpread = 0.22f;
        public float OrbitSpawnRadius = 0.55f;
        public float InitialArcStrength = 0.55f;
        public float HomingDelay = 0.14f;
        public int MaxJumps = 4;
        public float JumpRadius = 4f;
        public float FalloffPerJump = 0.8f;
        public float VisualDuration = 0.16f;
        public float VisualWidth = 0.07f;
        public float ImpactDelay = 1.2f;
        public float HealRatio = 0.5f;
        public float ReflectRatio = 0.5f;
        public string ReflectDamageType = "reflect";
        public string BuffDisplayName;
        public string BuffDescription;
        public string SpriteId;
        public string ImpactSpriteId;
        public string ImpactCharacterConfigId;
        public string ImpactActionName = "Special";
        public float ImpactScale = 1f;
        public float ImpactLifetime = 0.35f;
        public float AreaDamageRadius;
        public float AreaDamageMultiplier = 1f;
        public string ImpactSfxId;
        public float ImpactSfxVolume = 1f;
        public bool SuppressTargetHitSfx;
        public string CastCharacterConfigId;
        public string CastActionName = "Special";
        public float CastScale = 1f;
        public float CastLifetime = 0.25f;
        public float CastForwardOffset = 0.2f;
        public float CastHeightOffset = 0.35f;
        public string CastSfxId;
        public float CastSfxVolume = 1f;
        public string CastFlashPartId;
        public float CastFlashDuration = 0.16f;
        public Color CastFlashColor = Color.white;
        public string SfxId;
        public float SfxVolume = 1f;
        public Color CueColor = new(0.75f, 0.95f, 1f, 1f);
        public float CueDuration = 0.45f;
        public float CueRadius = 1.2f;
        public float CueHeightOffset = 0.35f;
        public int CueBurstCount = 42;
        public bool CueAtTarget;
        public bool CueAtPosition;
        public bool CueOnCastStart = true;
        public bool CueOnApply;
        public float ArmTime = 0.35f;
        public float TriggerRadius = 1.25f;
        public bool DetonateOnExpire = true;
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public float ForwardOffset = 0.7f;
        public float HeightOffset = 0.7f;
        public bool IgnoreStaticTargets;
        public string ConfigId;
        public int Count = 1;
        public int CountPerLevel;
        public float YOffset;
        public string InstanceIdPrefix = "summon";

        public ISkillEffect Build()
        {
            switch ((EffectType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "damage":
                    return new DamageEffect(ResolveDamage(), DamagePerLevel, DamageType);
                case "heal":
                    return new HealEffect(Heal, HealPerLevel, ApplyToSelf);
                case "cue":
                case "skillcue":
                case "skill_cue":
                case "sfx":
                case "vfx":
                    return new SkillCueEffect
                    {
                        SfxId = string.IsNullOrEmpty(SfxId) ? CastSfxId : SfxId,
                        SfxVolume = SfxVolume > 0f ? SfxVolume : CastSfxVolume,
                        Color = CueColor,
                        Duration = CueDuration,
                        Radius = CueRadius,
                        HeightOffset = CueHeightOffset,
                        BurstCount = CueBurstCount,
                        CueAtTarget = CueAtTarget,
                        CueAtPosition = CueAtPosition,
                        PlayOnCastStart = CueOnCastStart,
                        PlayOnApply = CueOnApply,
                    };
                case "dash":
                    return new DashEffect(Speed, SpeedPerLevel, InvulnerableDuration,
                        KeepVerticalVelocity, VerticalKick, DashDuration);
                case "teleport":
                    return new TeleportEffect(Distance, DistancePerLevel, UseAbsolutePosition);
                case "jumpslash":
                case "jump_slash":
                    return new JumpSlashEffect(JumpUp, JumpForward, AirTime, SlamDownVelocity,
                        ImpactRadius, ResolveDamage(), DamagePerLevel, DamageType);
                case "shield":
                    return new ShieldEffect(Reduction, Duration, ApplyToSelf);
                case "aoe":
                    return BuildAoe();
                case "zone":
                case "field":
                    return BuildZone();
                case "mine":
                case "trap":
                    return BuildMine();
                case "summon":
                case "summonentity":
                case "summon_entity":
                    return new SummonEntityEffect(ConfigId, Count, Radius,
                        CountPerLevel, YOffset, InstanceIdPrefix);
                case "knockback":
                    return new KnockbackEffect(Force, UpwardForce);
                case "slow":
                    return new SlowEffect(ResolveBuffId("slow"), Multiplier, Duration, ApplyToSelf);
                case "freeze":
                    return new FreezeEffect(ResolveBuffId("freeze"), Duration,
                        ResolveDamage(), DamagePerLevel, DamageType);
                case "stun":
                    return new StunEffect(ResolveBuffId("stun"), Duration);
                case "silence":
                    return new SilenceEffect(ResolveBuffId("silence"), Duration);
                case "dot":
                    return new DotEffect(ResolveBuffId("dot"), Duration, TickInterval,
                        DamagePerTick, DamagePerLevelPerTick, DamageType);
                case "hot":
                    return new HotEffect(ResolveBuffId("hot"), Duration, TickInterval,
                        HealPerTick, HealPerLevelPerTick, ApplyToSelf);
                case "projectile":
                    return new ProjectileEffect(
                        speed: Speed,
                        damage: ResolveDamage(),
                        damagePerLevel: DamagePerLevel,
                        damageType: DamageType,
                        radius: Radius,
                        maxLifetime: MaxLifetime,
                        pierce: Pierce,
                        spriteId: SpriteId,
                        impactSpriteId: ImpactSpriteId,
                        visualScale: VisualScale,
                        sortingOrder: SortingOrder,
                        forwardOffset: ForwardOffset,
                        heightOffset: HeightOffset,
                        impactCharacterConfigId: ImpactCharacterConfigId,
                        impactActionName: ImpactActionName,
                        impactScale: ImpactScale,
                        impactLifetime: ImpactLifetime,
                        visualRotationOffsetDegrees: VisualRotationOffsetDegrees,
                        ignoreStaticTargets: IgnoreStaticTargets,
                        areaDamageRadius: AreaDamageRadius,
                        areaDamageMultiplier: AreaDamageMultiplier,
                        impactSfxId: ImpactSfxId,
                        impactSfxVolume: ImpactSfxVolume,
                        suppressTargetHitSfx: SuppressTargetHitSfx,
                        castCharacterConfigId: CastCharacterConfigId,
                        castActionName: CastActionName,
                        castScale: CastScale,
                        castLifetime: CastLifetime,
                        castForwardOffset: CastForwardOffset,
                        castHeightOffset: CastHeightOffset,
                        castSfxId: CastSfxId,
                        castSfxVolume: CastSfxVolume,
                        castFlashPartId: CastFlashPartId,
                        castFlashDuration: CastFlashDuration,
                        castFlashColor: CastFlashColor,
                        hitEffects: BuildSubEffects());
                case "cleave":
                    return BuildCleave();
                case "whirlwind":
                    return new WhirlwindEffect(Duration, TickInterval, Radius,
                        DamagePerTick, DamagePerLevelPerTick, DamageType)
                    {
                        IncludeSelf = IncludeSelf,
                    };
                case "chainlightning":
                case "chain_lightning":
                    return new ChainLightningEffect(ResolveDamage(), MaxJumps, JumpRadius,
                        FalloffPerJump, DamagePerLevel, DamageType)
                    {
                        VisualDuration = VisualDuration,
                        VisualWidth = VisualWidth,
                    };
                case "meteor":
                    return new MeteorEffect(ImpactDelay, Radius, ResolveDamage(),
                        DamagePerLevel, DamageType, IncludeSelf);
                case "lifedrain":
                case "life_drain":
                    return new LifeDrainEffect(ResolveDamage(), HealRatio, DamagePerLevel, DamageType);
                case "lifestealbuff":
                case "life_steal_buff":
                case "bloodthirst":
                    return new LifeStealBuffEffect(ResolveBuffId("bloodthirst"), Duration, HealRatio,
                        DamageType, SpriteId, BuffDisplayName, BuffDescription);
                case "multishot":
                case "multi_shot":
                    return new MultiShotEffect(ProjectileCount, SpreadAngleDeg, Speed,
                        ResolveDamage(), DamagePerLevel, Pierce, DamageType)
                    {
                        Radius = Radius,
                        MaxLifetime = MaxLifetime,
                        SpriteId = SpriteId,
                        ImpactSpriteId = ImpactSpriteId,
                        ImpactCharacterConfigId = ImpactCharacterConfigId,
                        ImpactActionName = ImpactActionName,
                        ImpactScale = ImpactScale,
                        ImpactLifetime = ImpactLifetime,
                        AreaDamageRadius = AreaDamageRadius,
                        AreaDamageMultiplier = AreaDamageMultiplier,
                        ImpactSfxId = ImpactSfxId,
                        ImpactSfxVolume = ImpactSfxVolume,
                        SuppressTargetHitSfx = SuppressTargetHitSfx,
                        HitEffects = BuildSubEffects(),
                        VisualScale = VisualScale,
                        VisualRotationOffsetDegrees = VisualRotationOffsetDegrees,
                        SortingOrder = SortingOrder,
                        IgnoreStaticTargets = IgnoreStaticTargets,
                    };
                case "homingmultiprojectile":
                case "homing_multi_projectile":
                case "homingmultishot":
                case "homing_multi_shot":
                    return BuildHomingMultiProjectile();
                case "cleanse":
                    return new CleanseEffect(BuffIds, ApplyToSelf);
                case "damagereflect":
                case "damage_reflect":
                    return new DamageReflectEffect(ReflectRatio, Duration, ApplyToSelf,
                        ResolveBuffId("damage_reflect"), ReflectDamageType);
                default:
                    return null;
            }
        }

        private AoeEffect BuildAoe()
        {
            var effect = new AoeEffect(Radius, RadiusPerLevel, IncludeSelf);
            AddSubEffects(effect.SubEffects);
            return effect;
        }

        private ZoneEffect BuildZone()
        {
            var effect = new ZoneEffect
            {
                Duration = Duration,
                TickInterval = TickInterval,
                Radius = Radius,
                RadiusPerLevel = RadiusPerLevel,
                IncludeSelf = IncludeSelf,
                ForwardOffset = ForwardOffset,
                HeightOffset = HeightOffset,
                Color = CueColor,
            };
            AddSubEffects(effect.SubEffects);
            return effect;
        }

        private MineEffect BuildMine()
        {
            var effect = new MineEffect
            {
                Duration = Duration,
                ArmTime = ArmTime,
                TriggerRadius = TriggerRadius,
                Radius = Radius,
                IncludeSelf = IncludeSelf,
                DetonateOnExpire = DetonateOnExpire,
                ForwardOffset = ForwardOffset,
                HeightOffset = HeightOffset,
                Color = CueColor,
            };
            AddSubEffects(effect.SubEffects);
            return effect;
        }

        private HomingMultiProjectileEffect BuildHomingMultiProjectile()
        {
            return new HomingMultiProjectileEffect
            {
                ProjectileCount = ProjectileCount,
                SearchRadius = SearchRadius,
                HomingTurnSpeed = HomingTurnSpeed,
                SpawnSpread = SpawnSpread,
                OrbitSpawnRadius = OrbitSpawnRadius,
                InitialArcStrength = InitialArcStrength,
                HomingDelay = HomingDelay,
                Speed = Speed,
                Damage = ResolveDamage(),
                DamagePerLevel = DamagePerLevel,
                DamageType = DamageType,
                Radius = Radius,
                MaxLifetime = MaxLifetime,
                Pierce = Pierce,
                SpriteId = SpriteId,
                ImpactSpriteId = ImpactSpriteId,
                ImpactCharacterConfigId = ImpactCharacterConfigId,
                ImpactActionName = ImpactActionName,
                ImpactScale = ImpactScale,
                ImpactLifetime = ImpactLifetime,
                AreaDamageRadius = AreaDamageRadius,
                AreaDamageMultiplier = AreaDamageMultiplier,
                ImpactSfxId = ImpactSfxId,
                ImpactSfxVolume = ImpactSfxVolume,
                SuppressTargetHitSfx = SuppressTargetHitSfx,
                CastCharacterConfigId = CastCharacterConfigId,
                CastActionName = CastActionName,
                CastScale = CastScale,
                CastLifetime = CastLifetime,
                CastForwardOffset = CastForwardOffset,
                CastHeightOffset = CastHeightOffset,
                CastSfxId = CastSfxId,
                CastSfxVolume = CastSfxVolume,
                CastFlashPartId = CastFlashPartId,
                CastFlashDuration = CastFlashDuration,
                CastFlashColor = CastFlashColor,
                HitEffects = BuildSubEffects(),
                VisualScale = VisualScale,
                VisualRotationOffsetDegrees = VisualRotationOffsetDegrees,
                SortingOrder = SortingOrder,
                ForwardOffset = ForwardOffset,
                HeightOffset = HeightOffset,
                IgnoreStaticTargets = IgnoreStaticTargets,
            };
        }

        private CleaveEffect BuildCleave()
        {
            var effect = new CleaveEffect(EffectRange, HalfAngleDeg, IncludeSelf);
            AddSubEffects(effect.SubEffects);
            return effect;
        }

        private void AddSubEffects(List<ISkillEffect> target)
        {
            if (target == null || SubEffects == null) return;
            for (var i = 0; i < SubEffects.Count; i++)
            {
                var built = SubEffects[i]?.Build();
                if (built != null) target.Add(built);
            }
        }

        private List<ISkillEffect> BuildSubEffects()
        {
            var result = new List<ISkillEffect>();
            AddSubEffects(result);
            return result;
        }

        private float ResolveDamage()
        {
            return BaseDamage > 0f ? BaseDamage : Damage;
        }

        private string ResolveBuffId(string fallback)
        {
            return string.IsNullOrEmpty(BuffId) ? fallback : BuffId;
        }
    }
}
