using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>小猪 —— 被动动物，无攻击。</summary>
    public static class Pig
    {
        public const string CharacterConfigId = "tribe_pig";
        private static bool _characterRegistered;

        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "小猪",
                idleResourcePath: "Tribe/Common/Entity/Pig_idle (16x16)",
                walkResourcePath: "Tribe/Common/Entity/Pig_walk (16x16)",
                frameRate: 1f / 0.12f,
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
                Id = "pig", DisplayName = "小猪",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.35f, FreezePositionX = false,
                MaxHp = 5f, MoveSpeed = 1.4f, PatrolDistance = 3.5f,
                CanAttack = false,
            };
        }
    }
}
