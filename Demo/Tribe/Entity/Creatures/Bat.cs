using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>蝙蝠 —— 飞行单位（<c>UseGravity</c>=false），低血量但移动快。</summary>
    public static class Bat
    {
        public const string CharacterConfigId = "tribe_bat";
        private static bool _characterRegistered;

        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "蝙蝠",
                idleResourcePath: "Tribe/Common/Entity/Bat 01_idle (16x16)",
                walkResourcePath: "Tribe/Common/Entity/Bat 01_fly (16x16)",
                frameRate: 1f / 0.08f,
                leftFrameIndices: new[] { 4, 5, 6, 7 },
                rightFrameIndices: new[] { 8, 9, 10, 11 },
                visualScale: 8f, visualYOffset: 0f);
            _characterRegistered = true;
        }

        public static TribeCreatureConfig Preset()
        {
            EnsureCharacterRegistered();
            return new TribeCreatureConfig
            {
                Id = "bat", DisplayName = "蝙蝠",
                CharacterConfigId = CharacterConfigId,
                UseGravity = false, GravityScale = 0f,
                ColliderRadius = 0.3f, FreezePositionX = false,
                MaxHp = 4f, MoveSpeed = 3f, PatrolDistance = 4f,
                CanAttack = true, ContactDamage = 6f, DamageCooldown = 1f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 8f,
            };
        }
    }
}
