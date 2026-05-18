using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>奶牛 —— 被动动物，但 <see cref="TribeCreatureConfig.CanAttack"/>=true，
    /// 撞到玩家会造成接触伤害（"惊吓性"反击）。</summary>
    public static class Cow
    {
        public const string CharacterConfigId = "tribe_cow";
        private static bool _characterRegistered;

        /// <summary>幂等注册视觉到 <c>CharacterManager.CharacterService</c>。
        /// 4 行 × 4 列 spritesheet 约定：行 1 (frames 4-7) = 面朝左，行 2 (frames 8-11) = 面朝右。</summary>
        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "奶牛",
                idleResourcePath: "Tribe/Common/Entity/Cow_idle (20x16)",
                walkResourcePath: "Tribe/Common/Entity/Cow_walk (20x16)",
                frameRate: 1f / 0.15f,
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
                Id = "cow", DisplayName = "奶牛",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.45f, FreezePositionX = false,
                MaxHp = 20f, MoveSpeed = 1.2f, PatrolDistance = 4f,
                CanAttack = true, ContactDamage = 12f, DamageCooldown = 1.2f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 6f,
            };
        }
    }
}
