using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>母鸡 —— 纯被动动物，无攻击。</summary>
    public static class Hen
    {
        public const string CharacterConfigId = "tribe_hen";
        private static bool _characterRegistered;

        public static void EnsureCharacterRegistered()
        {
            if (_characterRegistered) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: CharacterConfigId, displayName: "母鸡",
                idleResourcePath: "Tribe/Common/Entity/Hen_idle (16x16)",
                walkResourcePath: "Tribe/Common/Entity/Hen_walk (16x16)",
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
                Id = "hen", DisplayName = "母鸡",
                CharacterConfigId = CharacterConfigId,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.3f, FreezePositionX = false,
                MaxHp = 3f, MoveSpeed = 1.6f, PatrolDistance = 3f,
                CanAttack = false,
            };
        }
    }
}
