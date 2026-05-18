using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>恶狼 —— 高速近战怪物，伤害较高。</summary>
    public static class Wolf
    {
        public const string CharacterConfigId = "tribe_wolf";
        private static bool _characterRegistered;

        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "恶狼",
                idleResourcePath: "Tribe/Common/Entity/Wolf 01_idle (16x16)",
                walkResourcePath: "Tribe/Common/Entity/Wolf 01_walk (16x16)",
                frameRate: 1f / 0.08f,
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
                Id = "wolf", DisplayName = "恶狼",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.45f, FreezePositionX = false,
                MaxHp = 12f, MoveSpeed = 3.5f, PatrolDistance = 5f,
                CanAttack = true, ContactDamage = 10f, DamageCooldown = 0.8f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 18f,
            };
        }
    }
}
