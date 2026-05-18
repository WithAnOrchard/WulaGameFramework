using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>骷髅 —— 标准近战怪物。</summary>
    public static class Skeleton
    {
        public const string CharacterConfigId = "tribe_skeleton";
        private static bool _characterRegistered;

        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "骷髅",
                idleResourcePath: "Tribe/Common/Entity/Skeleton 01_idle (16x16)",
                walkResourcePath: "Tribe/Common/Entity/Skeleton 01_walk (16x16)",
                frameRate: 1f / 0.1f,
                leftFrameIndices: new[] { 4, 5, 6, 7 },
                rightFrameIndices: new[] { 8, 9, 10, 11 },
                visualScale: 10f, visualYOffset: 0f);
            _characterRegistered = true;
        }

        public static TribeCreatureConfig Preset()
        {
            EnsureCharacterRegistered();
            return new TribeCreatureConfig
            {
                Id = "skeleton", DisplayName = "骷髅",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.45f, FreezePositionX = false,
                MaxHp = 10f, MoveSpeed = 2.5f, PatrolDistance = 3.5f,
                CanAttack = true, ContactDamage = 8f, DamageCooldown = 1f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 15f,
            };
        }
    }
}
