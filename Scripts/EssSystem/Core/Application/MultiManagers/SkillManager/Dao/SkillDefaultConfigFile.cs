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
        public int MaxJumps = 4;
        public float JumpRadius = 4f;
        public float FalloffPerJump = 0.8f;
        public float ImpactDelay = 1.2f;
        public float HealRatio = 0.5f;
        public float ReflectRatio = 0.5f;
        public string ReflectDamageType = "reflect";
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
        public float VisualScale = 1f;
        public float VisualRotationOffsetDegrees;
        public int SortingOrder = 260;
        public float ForwardOffset = 0.7f;
        public float HeightOffset = 0.7f;
        public bool IgnoreStaticTargets;

        public ISkillEffect Build()
        {
            switch ((EffectType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "damage":
                    return new DamageEffect(ResolveDamage(), DamagePerLevel, DamageType);
                case "heal":
                    return new HealEffect(Heal, HealPerLevel, ApplyToSelf);
                case "dash":
                    return new DashEffect(Speed, SpeedPerLevel, InvulnerableDuration, KeepVerticalVelocity, VerticalKick);
                case "teleport":
                    return new TeleportEffect(Distance, DistancePerLevel, UseAbsolutePosition);
                case "shield":
                    return new ShieldEffect(Reduction, Duration, ApplyToSelf);
                case "aoe":
                    return BuildAoe();
                case "knockback":
                    return new KnockbackEffect(Force, UpwardForce);
                case "slow":
                    return new SlowEffect(ResolveBuffId("slow"), Multiplier, Duration, ApplyToSelf);
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
                        castFlashColor: CastFlashColor);
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
                        FalloffPerJump, DamagePerLevel, DamageType);
                case "meteor":
                    return new MeteorEffect(ImpactDelay, Radius, ResolveDamage(),
                        DamagePerLevel, DamageType, IncludeSelf);
                case "lifedrain":
                case "life_drain":
                    return new LifeDrainEffect(ResolveDamage(), HealRatio, DamagePerLevel, DamageType);
                case "multishot":
                case "multi_shot":
                    return new MultiShotEffect(ProjectileCount, SpreadAngleDeg, Speed,
                        ResolveDamage(), DamagePerLevel, Pierce, DamageType)
                    {
                        Radius = Radius,
                        MaxLifetime = MaxLifetime,
                    };
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
