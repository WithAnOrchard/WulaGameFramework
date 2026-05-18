using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>食人魔 —— 大体型精英，慢速高血厚伤。</summary>
    public static class Ogre
    {
        public const string CharacterConfigId = "tribe_ogre";
        private static bool _characterRegistered;

        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "食人魔",
                idleResourcePath: "Tribe/Common/Entity/Ogre 03_idle (32x32)",
                walkResourcePath: "Tribe/Common/Entity/Ogre 03_walk (32x32)",
                frameRate: 1f / 0.12f,
                leftFrameIndices: new[] { 4, 5, 6, 7 },
                rightFrameIndices: new[] { 8, 9, 10, 11 },
                visualScale: 10f, visualYOffset: 0.76f);
            _characterRegistered = true;
        }

        public static TribeCreatureConfig Preset()
        {
            EnsureCharacterRegistered();
            return new TribeCreatureConfig
            {
                Id = "ogre", DisplayName = "食人魔",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.55f, FreezePositionX = false,
                MaxHp = 25f, MoveSpeed = 1.8f, PatrolDistance = 4f,
                CanAttack = true, ContactDamage = 15f, DamageCooldown = 1.5f,
                EnableFlash = true, FlashDuration = 0.2f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 4f,
            };
        }
    }
}
